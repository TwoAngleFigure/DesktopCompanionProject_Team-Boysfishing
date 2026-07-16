namespace DesktopCompanion.Views
{
    /// <summary>아쿠아리움 창의 물고기 상태 행(남은 시간/포인트).</summary>
    public class AquariumFishVD
    {
        public int Index;
        public string Name;
        public string MaterialName;
        public float RemainingSeconds;   // 다음 생산까지
        public int RemainingPoints;      // 이 물고기의 재료가 다음 1개까지 남은 포인트(required-current)
        public float PointsPerHour;
    }

    /// <summary>아쿠아리움 창의 총 생산 재료 행(누적/요구 포인트, 보류).</summary>
    public class AquariumMaterialVD
    {
        public string MaterialName;
        public string IconKey;
        public int Points;
        public int Required;
        public int Pending;
    }

    /// <summary>인벤토리 미러의 물고기 행/상세(이름·성급·크기·생산재료·시간당 포인트).</summary>
    public class AquariumMirrorFishVD
    {
        public int DataId;
        public string Name;
        public int Star;
        public float Size;
        public string MaterialName;
        public float PointsPerHour;
        public string IconKey;
        public int Index;   // 배치 목록에서의 인덱스(회수용). 인벤토리 항목이면 -1.
        public int Count;   // 인벤토리 집계 수량. 배치 항목이면 0.
    }
}
