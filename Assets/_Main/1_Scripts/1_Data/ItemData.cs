using UnityEngine;

namespace DesktopCompanion.Data
{
    public enum ItemType
    {
        Fish,           // 물고기
        Materials,      // 재료
        Consumables,    // 소모품
        Equipment,      // 장비
    }

    public enum ItemRarity
    {
        Normal = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Boss = 5,       // 티어별 보스 물고기. 일반 등급 위의 별도 축이라 자동판매 범위(≤Legendary)에서 제외된다
    }

    public enum ItemQuality
    {
        OneStar = 1,
        TwoStar = 2,
        ThreeStar = 3,
        FourStar = 4,
        FiveStar = 5,
    }

    /// <summary>장비 부위. 미끼/떡밥은 슬롯 장착형 소모품(G4).</summary>
    public enum EquipmentMountingArea
    {
        FishingRod,     // 낚싯대
        FishingLine,    // 낚싯줄
        Reel,           // 릴
        Lure,           // 루어
        Hat,            // 모자
        Uniform,        // 한벌옷
        Gloves,         // 장갑
        Engine,         // 배 엔진
        Storage,        // 물고기 창고
        GPS,            // GPS (판매 골드 배율)
        Bait,           // 미끼 - 슬롯 장착형 소모품
        Groundbait,     // 떡밥 - 슬롯 장착형 소모품
    }

    /// <summary>강화/제작 "비용"으로 소모되는 재료. (드롭은 ItemDrop — 비용은 재료 전용, G8)</summary>
    [System.Serializable]
    public class MaterialCost
    {
        [SerializeField] private ItemData_Materials m_material;
        [SerializeField] private int m_count;

        public ItemData_Materials Material => m_material;
        public int Count => m_count;
    }

    /// <summary>장비 강화 단계 하나의 정의(도달 시 효과 + 도달 비용).</summary>
    [System.Serializable]
    public class UpgradeStep
    {
        [Tooltip("이 단계 도달 시의 효과(누적치 정의)")]
        [SerializeField] private StatModifier[] m_modifiers;
        [Tooltip("이 단계로 올리는 골드 비용")]
        [SerializeField] private int m_goldCost;
        [SerializeField] private MaterialCost[] m_materialCosts;

        public StatModifier[] Modifiers => m_modifiers;
        public int GoldCost => m_goldCost;
        public MaterialCost[] MaterialCosts => m_materialCosts;
    }

    /// <summary>
    /// 아이템 계열 Data의 베이스. 인벤토리에서 다뤄지는 아이템의 정적 정의.
    /// 티어(아이템 수준)와 기준 판매가는 전 아이템 공통.
    /// ※ 구체 SO 클래스는 Unity 규칙상 "클래스명 = 파일명"으로 각자 파일에 분리되어 있음.
    /// </summary>
    public abstract class ItemData : GameData
    {
        [SerializeField] private ItemType m_type;
        [SerializeField] private int m_tier;        // 아이템 수준(기획서 '아이템의 티어')
        [SerializeField] private int m_basePrice;   // 기준 판매가(0=판매 불가)

        [TextArea]
        [Tooltip("표시용 설명 문장. 수치로 보여줄 수 없는 효과(보스 소환 대상 등)를 이 칸에 적는다")]
        [SerializeField] private string m_description;

        public ItemType Type => m_type;
        public int Tier => m_tier;
        public int BasePrice => m_basePrice;

        /// <summary>표시용 설명 문장. 비어 있으면 표시 계층이 수치 등 다른 표현으로 대체한다.</summary>
        public string Description => m_description;
    }
}
