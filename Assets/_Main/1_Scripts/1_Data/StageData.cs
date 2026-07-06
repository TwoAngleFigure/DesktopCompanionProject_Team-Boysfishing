using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>등장 물고기 + 가중치.</summary>
    [System.Serializable]
    public class FishPoolEntry
    {
        [SerializeField] private BattleFishData m_fish;
        [SerializeField] private float m_weight;

        public BattleFishData Fish => m_fish;
        public float Weight => m_weight;
    }

    /// <summary>
    /// 지역 내 티어별 물고기 풀. 라이센스 개방에 따라 동일 지역의 상위 풀이 열린다(G5).
    /// </summary>
    [System.Serializable]
    public class TierPool
    {
        [SerializeField] private int m_tier;
        [SerializeField] private int m_requiredLicense;    // 이 풀 개방에 필요한 라이센스
        [SerializeField] private FishPoolEntry[] m_entries;

        public int Tier => m_tier;
        public int RequiredLicense => m_requiredLicense;
        public FishPoolEntry[] Entries => m_entries;
    }

    /// <summary>
    /// 지역(스테이지) 정의. 이동시간 = 거리(mapPosition) ÷ 플레이어 이동속도 (StageSystem 계산).
    /// </summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Stage")]
    public class StageData : GameData
    {
        [SerializeField] private Vector2 m_mapPosition;      // 지도 위치
        [SerializeField] private int m_requiredLicense;      // 지역 해금 라이센스
        [SerializeField] private TierPool[] m_tierPools;     // 티어별 풀(G5)
        [SerializeField] private BattleFishData m_boss;

        public Vector2 MapPosition => m_mapPosition;
        public int RequiredLicense => m_requiredLicense;
        public TierPool[] TierPools => m_tierPools;
        public BattleFishData Boss => m_boss;
    }
}
