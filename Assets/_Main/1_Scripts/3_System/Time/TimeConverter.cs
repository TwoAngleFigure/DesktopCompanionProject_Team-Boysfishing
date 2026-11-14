using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 시간 변환 규칙. 게임 시각(경과 일수)을 날짜·시각·시간대로 바꾸고 다음 경계를 계산한다.
    /// 설정값으로 만드는 불변 객체이며 게임 시각 자체는 보관하지 않는다.
    /// 런타임(TimeSystem)과 변환 검증기가 같은 객체 종류를 쓴다.
    ///
    /// 경과 일수의 정수부가 일 번호(0부터), 소수부가 하루 진행도다.
    /// </summary>
    public sealed class TimeConverter
    {
        public const int SeasonCount = 4;
        public const double HoursPerDay = 24.0;

        public readonly double DayLengthSeconds;
        public readonly int DaysPerSeason;

        private readonly string[] m_phaseKeys;          // 시작 시각 오름차순
        private readonly double[] m_phaseStartHours;

        public int DaysPerYear => DaysPerSeason * SeasonCount;
        public IReadOnlyList<string> PhaseKeys => m_phaseKeys;

        public TimeConverter(double dayLengthSeconds, int daysPerSeason, IEnumerable<(string key, double startHour)> phases)
        {
            DayLengthSeconds = dayLengthSeconds;
            DaysPerSeason = daysPerSeason;

            var sorted = new List<(string key, double startHour)>(phases);
            sorted.Sort((a, b) => a.startHour.CompareTo(b.startHour));

            m_phaseKeys = new string[sorted.Count];
            m_phaseStartHours = new double[sorted.Count];
            for (int i = 0; i < sorted.Count; i++)
            {
                m_phaseKeys[i] = sorted[i].key;
                m_phaseStartHours[i] = sorted[i].startHour;
            }
        }

        public static long DayIndexOf(double elapsedDays) => (long)Math.Floor(elapsedDays);

        public static double HourOf(double elapsedDays) => (elapsedDays - Math.Floor(elapsedDays)) * HoursPerDay;

        public GameDate ToDate(long dayIndex)
        {
            int year = (int)(dayIndex / DaysPerYear) + 1;
            int dayOfYear = (int)(dayIndex % DaysPerYear);
            return new GameDate(year, (Season)(dayOfYear / DaysPerSeason), dayOfYear % DaysPerSeason + 1);
        }

        public long ToDayIndex(GameDate date)
            => (long)(date.Year - 1) * DaysPerYear + (int)date.Season * DaysPerSeason + (date.Day - 1);

        public bool HasPhase(string key) => Array.IndexOf(m_phaseKeys, key) >= 0;

        /// <summary>
        /// 그 시각(0~24)의 시간대 키. 시간대가 하나도 없으면 null이다.
        /// 첫 시간대의 시작 전이면 마지막 시간대다 — 마지막 시간대는 자정을 넘어 이어진다.
        /// </summary>
        public string PhaseAt(double hour)
        {
            if (m_phaseKeys.Length == 0)
            {
                return null;
            }

            for (int i = m_phaseKeys.Length - 1; i >= 0; i--)
            {
                if (m_phaseStartHours[i] <= hour)
                {
                    return m_phaseKeys[i];
                }
            }

            return m_phaseKeys[m_phaseKeys.Length - 1];
        }

        /// <summary>
        /// 다음 경계의 경과 일수. 경계는 다음 날짜의 시작과 다음 시간대의 시작 중 빠른 쪽이다.
        /// 시간 조건의 참·거짓은 경계에서만 바뀌므로 TimeSystem은 이 값을 넘었을 때만 재판정한다.
        /// </summary>
        public double NextBoundary(double elapsedDays)
        {
            double dayStart = Math.Floor(elapsedDays);
            double hour = (elapsedDays - dayStart) * HoursPerDay;
            double next = dayStart + 1.0;

            for (int i = 0; i < m_phaseStartHours.Length; i++)
            {
                if (m_phaseStartHours[i] > hour)
                {
                    next = Math.Min(next, dayStart + m_phaseStartHours[i] / HoursPerDay);
                    break;   // 오름차순이므로 첫 번째가 가장 가깝다
                }
            }

            return next;
        }
    }
}
