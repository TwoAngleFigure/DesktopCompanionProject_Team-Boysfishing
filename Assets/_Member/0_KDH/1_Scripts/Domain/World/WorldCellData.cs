namespace DesktopCompanion.Domain.World
{
    /// <summary>
    /// 수계 환경 분류 (강, 바다, 항구 등)
    /// </summary>
    public enum EnvironmentType : byte
    {
        Ocean = 0, // 대양 / 깊은 바다
        Coast = 1, // 연안 / 앞바다
        River = 2, // 강 / 하천
        Lake  = 3, // 호수
        Port  = 4  // 항구 / 정박 시설
    }

    /// <summary>
    /// 월드맵을 구성하는 최소 불변 단위 (Grid Cell 1칸의 고유 정보)
    /// 유니티 의존성이 배제된 순수 도메인 데이터 구조체
    /// </summary>
    [System.Serializable]
    public struct WorldCellData
    {
        public short X;                  // 그리드 정수 X 좌표
        public short Y;                  // 그리드 정수 Y 좌표
        public bool IsNavigable;         // 배가 항해 가능한 물 타일인지 여부 (false면 육지/장애물)
        public int AreaId;               // 소속 지역(영역) 고유 식별자 (예: 101: 영일만, 102: 포항 앞바다)
        public EnvironmentType EnvType;  // 수계 환경 분류 (배경/음향/어종 분기용)
        public int PortId;               // 정박소 식별자 (0이면 일반 수역, 1 이상이면 지정 항구 노드)

        public WorldCellData(short x, short y, bool isNavigable, int areaId, EnvironmentType envType, int portId = 0)
        {
            X = x;
            Y = y;
            IsNavigable = isNavigable;
            AreaId = areaId;
            EnvType = envType;
            PortId = portId;
        }

        public override string ToString()
        {
            return $"Cell({X}, {Y}) | Nav:{IsNavigable} | Area:{AreaId} | Env:{EnvType} | Port:{PortId}";
        }
    }
}
