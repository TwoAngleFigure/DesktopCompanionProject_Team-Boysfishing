using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DesktopCompanion.Core;
using DesktopCompanion.Systems;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// 플레이 모드에서 TimeSystem의 시계를 직접 전진시키고 알림·조건 판정을 확인하는 디버그 창.
    /// 하루가 현실 수십 분이므로 날짜·계절·년차 전환과 건너뛴 시간의 처리를 기다리지 않고 검증하기 위한 도구다.
    /// </summary>
    public class TimeDebugWindow : EditorWindow
    {
        private const int MaxLogLines = 40;
        private const double BoundaryEpsilonDays = 1e-9;   // 경계 직전에 멈추는 부동소수 오차를 넘기기 위한 여유

        private TimeSystem m_timeSystem;
        private float m_advanceHours = 1f;
        private float m_advanceDays = 1f;

        private string m_conditionText = "Season=Summer;Time=Night";
        private TimeConditionSubscription m_subscription;
        private string m_subscribedText;

        private readonly List<string> m_logs = new();
        private Vector2 m_logScroll;

        [MenuItem("Tools/DesktopCompanion/시간 디버그")]
        public static void Open() => GetWindow<TimeDebugWindow>("시간 디버그");

        private void OnEnable() => EditorApplication.playModeStateChanged += HandlePlayModeChanged;

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            Unbind();
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            if (EditorApplication.isPlaying == false)
            {
                EditorGUILayout.HelpBox("Play 모드에서만 사용할 수 있다.", MessageType.Info);
                return;
            }

            BindIfNeeded();
            if (m_timeSystem == null || m_timeSystem.Converter == null)
            {
                EditorGUILayout.HelpBox("TimeSystem을 찾을 수 없다. 부팅이 끝났는지, GameManager에 등록됐는지 확인한다.", MessageType.Warning);
                return;
            }

            DrawStateSection();
            EditorGUILayout.Space();
            DrawAdvanceSection();
            EditorGUILayout.Space();
            DrawFlowSection();
            EditorGUILayout.Space();
            DrawConditionSection();
            EditorGUILayout.Space();
            DrawLogSection();
        }

        // ── 현재 상태 ──

        private void DrawStateSection()
        {
            TimeConverter converter = m_timeSystem.Converter;
            GameDate date = m_timeSystem.Date;
            double hour = m_timeSystem.Hour;
            double secondsToBoundary = (converter.NextBoundary(m_timeSystem.ElapsedDays) - m_timeSystem.ElapsedDays)
                * converter.DayLengthSeconds;

            EditorGUILayout.LabelField("현재 상태", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("날짜", $"{date.Year}년차 {date.Season} {date.Day}일");
            EditorGUILayout.LabelField("시각", $"{(int)hour:00}:{(int)((hour - (int)hour) * 60.0):00}  (하루 진행도 {m_timeSystem.DayProgress:0.000})");
            EditorGUILayout.LabelField("시간대", m_timeSystem.Phase ?? "(시간대 없음)");
            EditorGUILayout.LabelField("경과 일수", m_timeSystem.ElapsedDays.ToString("0.0000"));
            EditorGUILayout.LabelField("다음 경계까지", $"{secondsToBoundary:0.0}초 (현실 시간)");
            EditorGUILayout.LabelField("다음 날까지", $"{m_timeSystem.SecondsUntilNextDay:0.0}초 (현실 시간)");
            EditorGUILayout.LabelField("설정", $"하루 {converter.DayLengthSeconds / 60.0:0.##}분 / 계절당 {converter.DaysPerSeason}일 / 시간대 {string.Join(", ", converter.PhaseKeys)}");
        }

        // ── 전진 ──

        private void DrawAdvanceSection()
        {
            EditorGUILayout.LabelField("전진 (게임 시간)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                m_advanceHours = EditorGUILayout.FloatField("시간", m_advanceHours);
                if (GUILayout.Button("전진", GUILayout.Width(60)))
                {
                    Advance(m_advanceHours / (float)TimeConverter.HoursPerDay, $"{m_advanceHours:0.##}시간");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                m_advanceDays = EditorGUILayout.FloatField("일", m_advanceDays);
                if (GUILayout.Button("전진", GUILayout.Width(60)))
                {
                    Advance(m_advanceDays, $"{m_advanceDays:0.##}일");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("다음 날 0시로"))
                {
                    Advance(1.0 - m_timeSystem.DayProgress + BoundaryEpsilonDays, "다음 날 0시까지");
                }

                if (GUILayout.Button("다음 계절 1일로"))
                {
                    int daysLeft = m_timeSystem.Converter.DaysPerSeason - m_timeSystem.Date.Day;
                    Advance(daysLeft + 1.0 - m_timeSystem.DayProgress + BoundaryEpsilonDays, "다음 계절 1일까지");
                }
            }

            if (m_timeSystem.IsPaused)
            {
                EditorGUILayout.HelpBox("일시정지 중에는 전진하지 않는다.", MessageType.Info);
            }
        }

        private void Advance(double gameDays, string label)
        {
            if (gameDays <= 0.0)
            {
                AddLog($"전진량이 0 이하라 전진하지 않는다 ({label})");
                return;
            }

            AddLog($"── 전진 {label} ──");
            m_timeSystem.DebugAdvance(gameDays);
        }

        // ── 흐름 방식·일시정지 ──

        private void DrawFlowSection()
        {
            EditorGUILayout.LabelField("흐름 방식·일시정지", EditorStyles.boldLabel);

            var mode = (TimeFlowMode)EditorGUILayout.EnumPopup("흐름 방식", m_timeSystem.FlowMode);
            if (mode != m_timeSystem.FlowMode)
            {
                m_timeSystem.SetFlowMode(mode);
                AddLog($"흐름 방식 → {mode}");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("일시정지", m_timeSystem.IsPaused ? "정지 중" : "흐르는 중");

                // 이 창 자신을 요청자로 쓴다. 다른 요청자가 남아 있으면 해제해도 정지가 유지된다.
                if (GUILayout.Button("정지 요청"))
                {
                    m_timeSystem.RequestPause(this);
                }

                if (GUILayout.Button("요청 해제"))
                {
                    m_timeSystem.ReleasePause(this);
                }
            }
        }

        // ── 조건 구독 ──

        private void DrawConditionSection()
        {
            EditorGUILayout.LabelField("시간 조건", EditorStyles.boldLabel);
            m_conditionText = EditorGUILayout.TextField("조건", m_conditionText);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 판정"))
                {
                    TimeCondition condition = TimeConditionRule.Parse(m_conditionText, m_timeSystem.Converter, "시간 디버그");
                    string result = condition.IsInvalid ? "무효" : m_timeSystem.IsActive(condition).ToString();
                    AddLog($"판정 '{m_conditionText}' → {result}{(condition.IsPermanent ? " (영구 조건)" : string.Empty)}");
                }

                if (GUILayout.Button("구독 시작"))
                {
                    StartSubscription();
                }

                if (GUILayout.Button("구독 해제"))
                {
                    StopSubscription();
                    AddLog("구독 해제");
                }
            }

            if (m_subscription != null)
            {
                EditorGUILayout.LabelField("구독 중", $"{m_subscribedText}  (현재 {m_subscription.IsActive})");
            }
        }

        private void StartSubscription()
        {
            StopSubscription();

            string text = m_conditionText;
            TimeCondition condition = TimeConditionRule.Parse(text, m_timeSystem.Converter, "시간 디버그");
            m_subscription = m_timeSystem.Subscribe(condition, isActive => AddLog($"★ {(isActive ? "진입" : "이탈")}: '{text}'"));
            m_subscribedText = text;
            AddLog($"구독 시작 '{text}' (현재 {m_subscription.IsActive}{(condition.IsInvalid ? ", 무효" : string.Empty)})");
        }

        private void StopSubscription()
        {
            if (m_subscription != null && m_timeSystem != null)
            {
                m_timeSystem.Unsubscribe(m_subscription);
            }

            m_subscription = null;
        }

        // ── 이벤트 로그 ──

        private void DrawLogSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("이벤트 로그", EditorStyles.boldLabel);
                if (GUILayout.Button("지우기", GUILayout.Width(60)))
                {
                    m_logs.Clear();
                }
            }

            m_logScroll = EditorGUILayout.BeginScrollView(m_logScroll, GUILayout.Height(220));
            for (int i = m_logs.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.LabelField(m_logs[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void AddLog(string line)
        {
            m_logs.Add(line);
            if (m_logs.Count > MaxLogLines)
            {
                m_logs.RemoveAt(0);
            }

            Repaint();
        }

        // ── 바인딩 ──

        // 플레이 세션마다 TimeSystem 인스턴스가 새로 생기므로 바뀌었으면 다시 구독한다.
        private void BindIfNeeded()
        {
            TimeSystem found = FindTimeSystem();
            if (found == m_timeSystem)
            {
                return;
            }

            Unbind();
            m_timeSystem = found;
            if (m_timeSystem == null)
            {
                return;
            }

            m_timeSystem.OnDateChanged += HandleDateChanged;
            m_timeSystem.OnSeasonChanged += HandleSeasonChanged;
            m_timeSystem.OnYearChanged += HandleYearChanged;
            m_timeSystem.OnPhaseChanged += HandlePhaseChanged;
            m_timeSystem.OnPauseChanged += HandlePauseChanged;
        }

        private void Unbind()
        {
            if (m_timeSystem != null)
            {
                StopSubscription();
                m_timeSystem.ReleasePause(this);
                m_timeSystem.OnDateChanged -= HandleDateChanged;
                m_timeSystem.OnSeasonChanged -= HandleSeasonChanged;
                m_timeSystem.OnYearChanged -= HandleYearChanged;
                m_timeSystem.OnPhaseChanged -= HandlePhaseChanged;
                m_timeSystem.OnPauseChanged -= HandlePauseChanged;
            }

            m_timeSystem = null;
            m_subscription = null;
        }

        private void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                Unbind();
            }
        }

        private void HandleDateChanged(DateChange change)
            => AddLog($"OnDateChanged: {change.Previous} → {change.Current} ({change.ElapsedDays}일 경과)");

        private void HandleSeasonChanged(DateChange change)
            => AddLog($"OnSeasonChanged: {change.Previous.Season} → {change.Current.Season}");

        private void HandleYearChanged(DateChange change)
            => AddLog($"OnYearChanged: {change.Previous.Year} → {change.Current.Year}");

        private void HandlePhaseChanged(string previous, string current)
            => AddLog($"OnPhaseChanged: {previous} → {current}");

        private void HandlePauseChanged(bool isPaused)
            => AddLog($"OnPauseChanged: {(isPaused ? "정지" : "재개")}");

        // GameManager.m_systemManager(비공개)를 리플렉션으로 얻는다(RecordDebugWindow와 같은 방식).
        private static TimeSystem FindTimeSystem()
        {
            GameManager gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager == null)
            {
                return null;
            }

            FieldInfo field = typeof(GameManager).GetField("m_systemManager", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(gameManager) is SystemManager systemManager
                ? systemManager.GetSystem<TimeSystem>()
                : null;
        }
    }
}
