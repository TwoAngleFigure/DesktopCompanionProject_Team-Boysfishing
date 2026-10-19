namespace DesktopCompanion.Views
{
    /// <summary>
    /// 버튼의 표시 상태. 우선순위는 Active > Hover > Normal이다
    /// (선택된 탭이 hover에 반응하지 않는 동작이 이 규칙이다).
    ///
    /// 상태를 더 늘리지 않는다 — 늘리는 순간 아티스트가 채워야 할 칸이 효과마다 하나씩 는다.
    /// </summary>
    public enum ButtonVisualState
    {
        /// <summary>대기</summary>
        Normal,

        /// <summary>포인터가 올라와 있음</summary>
        Hover,

        /// <summary>켜짐·선택됨. 이 판정의 근거가 되는 논리 상태는 바깥이 소유한다</summary>
        Active,
    }
}
