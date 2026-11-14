using System;

namespace DesktopCompanion.Systems
{
    /// <summary>계절. 시트 조건 칸에 이름 문자열로 적으므로 이름을 바꾸지 않는다.</summary>
    public enum Season
    {
        Spring = 0,
        Summer,
        Autumn,
        Winter,
    }

    /// <summary>게임 시계의 전진 조건. 시트(TimeSettingData.m_flowMode)에 이름 문자열로 적는다.</summary>
    public enum TimeFlowMode
    {
        Offline,     // 게임을 꺼도 흐른다
        Unfocused,   // 실행 중이면 포커스와 무관하게 흐른다
        Focused,     // 포커스된 동안에만 흐른다
    }

    /// <summary>게임 내 날짜. Year와 Day는 1부터 센다.</summary>
    public readonly struct GameDate : IEquatable<GameDate>
    {
        public readonly int Year;
        public readonly Season Season;
        public readonly int Day;

        public GameDate(int year, Season season, int day)
        {
            Year = year;
            Season = season;
            Day = day;
        }

        public bool Equals(GameDate other) => Year == other.Year && Season == other.Season && Day == other.Day;

        public override bool Equals(object obj) => obj is GameDate other && Equals(other);

        public override int GetHashCode() => (Year * 397) ^ ((int)Season * 31) ^ Day;

        /// <summary>시트 조건 칸과 같은 표기. 예: 1.Summer.10</summary>
        public override string ToString() => $"{Year}.{Season}.{Day}";
    }

    /// <summary>날짜 변경 내역. 여러 날을 건너뛰어도 한 번만 발행하며 건너뛴 일수를 담는다.</summary>
    public readonly struct DateChange
    {
        public readonly GameDate Previous;
        public readonly GameDate Current;
        public readonly long ElapsedDays;

        public DateChange(GameDate previous, GameDate current, long elapsedDays)
        {
            Previous = previous;
            Current = current;
            ElapsedDays = elapsedDays;
        }
    }

    /// <summary>
    /// 조건 구독 하나의 핸들. TimeSystem.Subscribe가 반환한다.
    /// IsActive는 TimeSystem이 경계에서 재판정할 때 갱신한다.
    /// </summary>
    public sealed class TimeConditionSubscription
    {
        public TimeCondition Condition { get; }
        public bool IsActive { get; internal set; }

        internal Action<bool> OnChanged { get; }
        internal int Order { get; }           // 등록 순서. 일시 조건의 알림 순서에 쓴다
        internal bool IsRemoved { get; set; }

        internal TimeConditionSubscription(TimeCondition condition, Action<bool> onChanged, int order)
        {
            Condition = condition;
            OnChanged = onChanged;
            Order = order;
        }
    }
}
