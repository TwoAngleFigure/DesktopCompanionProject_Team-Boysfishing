using System;
using System.Collections.Generic;
using System.Globalization;
using DesktopCompanion.Data;
using DesktopCompanion.Save;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 게임 시계의 단일 진입점. 게임 시각(경과 일수) 하나를 보유하고 전진·조회·알림·조건 구독·저장을 맡는다.
    /// 날짜·계절·년차·시각·시간대는 모두 경과 일수에서 계산하는 파생값이다(TimeConverter).
    /// 다른 System을 참조하지 않는 기반 시스템이다.
    ///
    /// 흐름 방식: Offline은 현실 시각 차이만큼, Unfocused는 프레임 경과 시간만큼,
    /// Focused는 포커스된 동안의 프레임 경과 시간만큼 전진한다. 일시정지 요청이 하나라도 있으면 전진하지 않는다.
    ///
    /// 재판정 규칙: 다음 경계(날짜 시작·시간대 시작)를 넘었을 때만 구독을 재판정하고,
    /// 마지막 판정과 결과가 달라진 구독만 알린다. 건너뛴 구간 안에서 시작하고 끝난 조건은 알리지 않는다.
    /// 발행 순서: 시각 전진 → OnDateChanged → OnSeasonChanged → OnYearChanged → OnPhaseChanged → 구독 알림.
    /// </summary>
    public class TimeSystem : SystemBase, ISaveable, ITickable
    {
        private const int SettingId = 1;
        private const float DefaultDayLengthMinutes = 24f;
        private const int DefaultDaysPerSeason = 28;
        private const float DefaultStartHour = 6f;
        private const TimeFlowMode DefaultFlowMode = TimeFlowMode.Unfocused;

        private TimeConverter m_converter;
        private TimeFlowMode m_flowMode;

        private double m_elapsedDays;   // 게임 시각. 유일한 시간 상태다
        private DateTime m_lastUtc;     // 마지막 갱신의 현실 시각. Offline 방식의 전진량 계산에 쓴다
        private double m_nextBoundary;  // 다음 재판정 시점(경과 일수)

        private readonly HashSet<object> m_pauseOwners = new();
        private readonly List<TimeConditionSubscription> m_subscriptions = new();
        private readonly List<TimeConditionSubscription> m_changed = new();
        private int m_subscriptionOrder;

        /// <summary>일 번호가 바뀌었을 때 1회. 여러 날을 건너뛰어도 한 번이며 건너뛴 일수를 담는다.</summary>
        public event Action<DateChange> OnDateChanged;

        /// <summary>계절이 바뀌었을 때 1회.</summary>
        public event Action<DateChange> OnSeasonChanged;

        /// <summary>년차가 바뀌었을 때 1회.</summary>
        public event Action<DateChange> OnYearChanged;

        /// <summary>시간대가 바뀌었을 때 1회. (이전 키, 현재 키)</summary>
        public event Action<string, string> OnPhaseChanged;

        /// <summary>정지 ↔ 재개가 바뀔 때. true = 정지.</summary>
        public event Action<bool> OnPauseChanged;

        // ── 조회 ──

        public TimeConverter Converter => m_converter;
        public TimeFlowMode FlowMode => m_flowMode;
        public bool IsPaused => m_pauseOwners.Count > 0;

        public GameDate Date => m_converter.ToDate(TimeConverter.DayIndexOf(m_elapsedDays));

        /// <summary>게임 내 시각(0~24).</summary>
        public double Hour => TimeConverter.HourOf(m_elapsedDays);

        /// <summary>하루 진행도(0~1).</summary>
        public double DayProgress => m_elapsedDays - Math.Floor(m_elapsedDays);

        /// <summary>현재 시간대의 키(DayPhaseData.m_key). 시간대가 없으면 null이다.</summary>
        public string Phase => m_converter.PhaseAt(Hour);

        /// <summary>다음 날까지 남은 현실 시간(초). 현재 흐름이 유지된다고 가정한 값이다.</summary>
        public double SecondsUntilNextDay => (1.0 - DayProgress) * m_converter.DayLengthSeconds;

        /// <summary>게임 시작부터 경과한 게임 일수. 디버그·표시용이다.</summary>
        public double ElapsedDays => m_elapsedDays;

        // ── 생명주기 ──

        // GameData만 읽는다. 소비자가 Phase 2에서 등록 순서와 무관하게 Converter·Subscribe를 쓸 수 있게 Phase 1에서 만든다.
        public override void Initialize()
        {
            TimeSettingData setting = DataManager.GetData<TimeSettingData>(SettingId);
            if (setting == null)
            {
                Debug.LogWarning($"[TimeSystem] TimeSettingData(ID={SettingId})가 없어 기본값으로 동작한다");
            }

            m_converter = new TimeConverter(
                ReadDayLengthMinutes(setting) * 60.0,
                ReadDaysPerSeason(setting),
                ReadPhases());
            m_flowMode = setting != null ? setting.FlowMode : DefaultFlowMode;

            // 원시 기본값: 1년차 봄 1일, 시작 시각. 저장이 있으면 RestoreState가 덮어쓴다.
            m_elapsedDays = ReadStartHour(setting) / TimeConverter.HoursPerDay;
            m_lastUtc = DateTime.UtcNow;

            m_pauseOwners.Clear();
            m_subscriptions.Clear();
            m_subscriptionOrder = 0;
        }

        // 복원된 게임 시각 기준으로 다음 경계를 계산한다(파생). 오프라인 경과분은 여기서 반영하지 않는다 —
        // 모든 소비자가 Phase 2에서 구독을 마친 뒤 첫 Tick에서 반영해야 그 알림을 놓치지 않는다.
        public override void PostInitialize() => m_nextBoundary = m_converter.NextBoundary(m_elapsedDays);

        // ── 흐름 ──

        public void Tick(float deltaTime)
        {
            DateTime now = DateTime.UtcNow;
            double wallSeconds = Math.Max(0.0, (now - m_lastUtc).TotalSeconds);   // 시스템 시계를 과거로 돌린 경우 0
            m_lastUtc = now;   // 흐름 방식과 무관하게 갱신한다. 런타임에 Offline으로 전환해도 날짜가 건너뛰지 않는다

            switch (m_flowMode)
            {
                case TimeFlowMode.Offline:
                    // 재실행 후 첫 Tick에서는 저장된 m_lastUtc와의 차이가 곧 오프라인 경과분이다.
                    Advance(wallSeconds);
                    break;

                case TimeFlowMode.Unfocused:
                    Advance(deltaTime);
                    break;

                case TimeFlowMode.Focused:
                    if (Application.isFocused)
                    {
                        Advance(deltaTime);
                    }

                    break;
            }
        }

        /// <summary>
        /// 흐름 방식을 전환한다. 세이브에 저장하지 않으므로 다음 실행에서는 설정 데이터의 기본값으로 돌아간다.
        /// </summary>
        public void SetFlowMode(TimeFlowMode mode) => m_flowMode = mode;

        /// <summary>
        /// 에디터 디버그 전용. 게임 일수만큼 시계를 전진시킨다. 알림은 일반 전진과 같은 경로로 발생한다.
        /// 알림 핸들러 안에서 호출하지 않는다.
        /// </summary>
        public void DebugAdvance(double gameDays) => Advance(gameDays * m_converter.DayLengthSeconds);

        private void Advance(double realSeconds)
        {
            if (realSeconds <= 0.0 || IsPaused)
            {
                return;
            }

            double before = m_elapsedDays;
            m_elapsedDays = before + realSeconds / m_converter.DayLengthSeconds;
            if (m_elapsedDays < m_nextBoundary)
            {
                return;   // 프레임 대부분은 여기서 끝난다
            }

            m_nextBoundary = m_converter.NextBoundary(m_elapsedDays);
            NotifyClockChanges(before);
            NotifySubscriptions();
        }

        private void NotifyClockChanges(double before)
        {
            long previousDay = TimeConverter.DayIndexOf(before);
            long currentDay = TimeConverter.DayIndexOf(m_elapsedDays);
            if (currentDay != previousDay)
            {
                GameDate previous = m_converter.ToDate(previousDay);
                GameDate current = m_converter.ToDate(currentDay);
                var change = new DateChange(previous, current, currentDay - previousDay);

                OnDateChanged?.Invoke(change);

                bool isYearChanged = previous.Year != current.Year;
                if (isYearChanged || previous.Season != current.Season)
                {
                    OnSeasonChanged?.Invoke(change);
                }

                if (isYearChanged)
                {
                    OnYearChanged?.Invoke(change);
                }
            }

            string previousPhase = m_converter.PhaseAt(TimeConverter.HourOf(before));
            string currentPhase = Phase;
            if (previousPhase != currentPhase)
            {
                OnPhaseChanged?.Invoke(previousPhase, currentPhase);
            }
        }

        // ── 일시정지 ──

        /// <summary>
        /// 시계 정지를 요청한다. 요청이 하나라도 남아 있으면 시계가 전진하지 않는다.
        /// 같은 owner의 중복 요청은 한 번으로 센다. 정지 중에 흐른 시간은 버린다.
        /// </summary>
        public void RequestPause(object owner)
        {
            if (owner == null)
            {
                return;
            }

            bool wasPaused = IsPaused;
            if (m_pauseOwners.Add(owner) && wasPaused == false)
            {
                OnPauseChanged?.Invoke(true);
            }
        }

        public void ReleasePause(object owner)
        {
            if (owner == null)
            {
                return;
            }

            if (m_pauseOwners.Remove(owner) && IsPaused == false)
            {
                OnPauseChanged?.Invoke(false);
            }
        }

        // ── 조건 구독 ──

        /// <summary>
        /// 조건을 구독한다. 이후 조건의 참·거짓이 바뀔 때 onChanged(진입 = true, 이탈 = false)를 호출한다.
        /// 반환된 핸들의 IsActive가 구독 시점의 참 여부다. 구독 자체로는 알림이 발생하지 않는다.
        /// </summary>
        public TimeConditionSubscription Subscribe(TimeCondition condition, Action<bool> onChanged)
        {
            var subscription = new TimeConditionSubscription(condition, onChanged, m_subscriptionOrder++)
            {
                IsActive = TimeConditionRule.IsActive(condition, m_elapsedDays, m_converter),
            };
            m_subscriptions.Add(subscription);
            return subscription;
        }

        public void Unsubscribe(TimeConditionSubscription subscription)
        {
            if (subscription != null && m_subscriptions.Remove(subscription))
            {
                subscription.IsRemoved = true;
            }
        }

        /// <summary>구독 없이 현재 참 여부만 판정한다.</summary>
        public bool IsActive(TimeCondition condition) => TimeConditionRule.IsActive(condition, m_elapsedDays, m_converter);

        private void NotifySubscriptions()
        {
            // 달라진 구독을 먼저 확정한 뒤 알린다. 핸들러가 Subscribe·Unsubscribe를 호출해도 진행 중인 목록이 바뀌지 않는다.
            m_changed.Clear();
            for (int i = 0; i < m_subscriptions.Count; i++)
            {
                TimeConditionSubscription subscription = m_subscriptions[i];
                bool isActive = TimeConditionRule.IsActive(subscription.Condition, m_elapsedDays, m_converter);
                if (isActive != subscription.IsActive)
                {
                    subscription.IsActive = isActive;
                    m_changed.Add(subscription);
                }
            }

            if (m_changed.Count == 0)
            {
                return;
            }

            // 영구 조건을 Date>= 날짜가 이른 순으로 먼저, 그다음 일시 조건을 등록 순으로 알린다.
            m_changed.Sort(CompareNotifyOrder);

            // 핸들러가 시계를 다시 전진시키는 경우(디버그 전진)에도 순회가 깨지지 않게 복사본으로 알린다.
            TimeConditionSubscription[] snapshot = m_changed.ToArray();
            foreach (TimeConditionSubscription subscription in snapshot)
            {
                if (subscription.IsRemoved == false)
                {
                    subscription.OnChanged?.Invoke(subscription.IsActive);
                }
            }
        }

        private int CompareNotifyOrder(TimeConditionSubscription a, TimeConditionSubscription b)
        {
            bool isPermanentA = a.Condition.IsPermanent;
            bool isPermanentB = b.Condition.IsPermanent;
            if (isPermanentA != isPermanentB)
            {
                return isPermanentA ? -1 : 1;
            }

            if (isPermanentA)
            {
                int bySince = m_converter.ToDayIndex(a.Condition.Since).CompareTo(m_converter.ToDayIndex(b.Condition.Since));
                if (bySince != 0)
                {
                    return bySince;
                }
            }

            return a.Order.CompareTo(b.Order);
        }

        // ── 설정 읽기 ──

        private static float ReadDayLengthMinutes(TimeSettingData setting)
        {
            if (setting == null)
            {
                return DefaultDayLengthMinutes;
            }

            if (setting.DayLengthMinutes <= 0f)
            {
                Debug.LogError($"[TimeSystem] m_dayLengthMinutes는 0보다 커야 한다({setting.DayLengthMinutes}). 기본값 {DefaultDayLengthMinutes}으로 대체한다");
                return DefaultDayLengthMinutes;
            }

            return setting.DayLengthMinutes;
        }

        private static int ReadDaysPerSeason(TimeSettingData setting)
        {
            if (setting == null)
            {
                return DefaultDaysPerSeason;
            }

            if (setting.DaysPerSeason < 1)
            {
                Debug.LogError($"[TimeSystem] m_daysPerSeason은 1 이상이어야 한다({setting.DaysPerSeason}). 기본값 {DefaultDaysPerSeason}으로 대체한다");
                return DefaultDaysPerSeason;
            }

            return setting.DaysPerSeason;
        }

        private static float ReadStartHour(TimeSettingData setting)
        {
            if (setting == null)
            {
                return DefaultStartHour;
            }

            if (setting.StartHour < 0f || setting.StartHour >= (float)TimeConverter.HoursPerDay)
            {
                Debug.LogError($"[TimeSystem] m_startHour는 0 이상 24 미만이어야 한다({setting.StartHour}). 기본값 {DefaultStartHour}으로 대체한다");
                return DefaultStartHour;
            }

            return setting.StartHour;
        }

        // 형식이 틀린 시간대는 건너뛴다. 그 키를 참조하는 조건은 '정의되지 않은 시간대'로 무효가 된다.
        private List<(string key, double startHour)> ReadPhases()
        {
            var phases = new List<(string key, double startHour)>();
            foreach (DayPhaseData phase in DataManager.GetAll<DayPhaseData>())
            {
                if (string.IsNullOrWhiteSpace(phase.Key) || phase.StartHour < 0f || phase.StartHour >= (float)TimeConverter.HoursPerDay)
                {
                    Debug.LogError($"[TimeSystem] DayPhaseData {phase.ID}: m_key가 비었거나 m_startHour가 0 이상 24 미만이 아니다. 이 시간대를 건너뛴다");
                    continue;
                }

                phases.Add((phase.Key.Trim(), phase.StartHour));
            }

            return phases;
        }

        // ── 저장 ──

        public string SaveId => "time";
        public Type StateType => typeof(TimeSave);

        public object CaptureState() => new TimeSave
        {
            elapsedDays = m_elapsedDays,
            lastUtc = m_lastUtc.ToString("o"),
        };

        // 원시 상태 두 값만 채운다. 다음 경계(파생)는 PostInitialize가 계산한다.
        public void RestoreState(object state)
        {
            if (state is not TimeSave save)
            {
                return;
            }

            m_elapsedDays = Math.Max(0.0, save.elapsedDays);
            if (DateTime.TryParse(save.lastUtc, null, DateTimeStyles.RoundtripKind, out m_lastUtc) == false)
            {
                m_lastUtc = DateTime.UtcNow;   // 파싱 실패 시 오프라인 경과 없음으로 처리
            }
        }
    }
}
