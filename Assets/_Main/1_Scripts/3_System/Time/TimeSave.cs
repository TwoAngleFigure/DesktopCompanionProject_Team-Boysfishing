using System;

namespace DesktopCompanion.Systems
{
    /// <summary>시간 시스템 영구 상태 DTO. 날짜·계절·시간대는 저장하지 않는다(경과 일수의 파생값).</summary>
    [Serializable]
    public class TimeSave
    {
        public double elapsedDays;   // 게임 시작부터 경과한 게임 일수
        public string lastUtc;       // 마지막 갱신의 현실 시각. DateTime.UtcNow.ToString("o")
    }
}
