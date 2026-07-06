using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>
    /// 보스 등 처치 시 확률 드롭 항목. 재료뿐 아니라 장비 등 모든 아이템 가능(G8).
    /// </summary>
    [System.Serializable]
    public class ItemDrop
    {
        [SerializeField] private ItemData m_item;       // ItemData 베이스 참조 — Materials/Equipment 등 전부 가능
        [SerializeField] private int m_count;
        [Range(0f, 1f)]
        [SerializeField] private float m_probability;

        public ItemData Item => m_item;
        public int Count => m_count;
        public float Probability => m_probability;
    }

    /// <summary>
    /// 전투(낚시) 대상 물고기의 정의.
    /// 크기 롤 흐름(G6): 입질 시 System이 Entity_BattleFish 생성하며 min~max 내에서
    /// probabilityAtFishSize 반영해 크기 롤 → qualityThresholds로 성급 산출 →
    /// 포획 성공 시 그 Size/Quality를 Entity_Fish(아이템)로 복사 생성.
    /// </summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Battle/Fish")]
    public class BattleFishData : GameData
    {
        [SerializeField] private ItemData_Fish m_itemFish;      // 포획 성공 시 생성될 아이템 종

        [Header("Battle")]
        [SerializeField] private int m_maxHp;
        [SerializeField] private int m_battleTimeVariable;      // PlayerData 값과 비교해 제한시간 결정

        [Header("Size Roll & Quality")]
        [SerializeField] private float m_minSize;
        [SerializeField] private float m_maxSize;
        [Tooltip("크기→성급 경계값 4개(오름차순). size < [0] → 1성 … size >= [3] → 5성")]
        [SerializeField] private float[] m_qualityThresholds;

        [Header("Boss")]
        [SerializeField] private bool m_isBoss;
        [SerializeField] private ItemDrop[] m_bossDrops;        // 보스 획득 시 확률 드롭(G8)

        public ItemData_Fish ItemFish => m_itemFish;
        public int MaxHp => m_maxHp;
        public int BattleTimeVariable => m_battleTimeVariable;
        public float MinSize => m_minSize;
        public float MaxSize => m_maxSize;
        public bool IsBoss => m_isBoss;
        public ItemDrop[] BossDrops => m_bossDrops;

        /// <summary>크기 → 성급 산출(G6). 경계값 미설정 시 1성.</summary>
        public ItemQuality GetQuality(float size)
        {
            if (m_qualityThresholds == null || m_qualityThresholds.Length == 0)
            {
                return ItemQuality.OneStar;
            }
            int star = 1;
            foreach (float threshold in m_qualityThresholds)
            {
                if (size >= threshold)
                {
                    star++;
                }
            }
            return (ItemQuality)Mathf.Clamp(star, 1, 5);
        }
    }
}
