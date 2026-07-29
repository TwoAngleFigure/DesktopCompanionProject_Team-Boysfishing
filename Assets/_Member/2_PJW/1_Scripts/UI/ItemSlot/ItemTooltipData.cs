using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 툴팁 팝업 1회분의 상세 스냅샷. 문자열을 조립하지 않고 값 그대로 담으며, 문장 구성은 패널이 담당한다.
    /// <see cref="Kind"/>에 해당하는 섹션 하나만 채워진다.
    /// </summary>
    public class ItemTooltipData
    {
        public TooltipItemKind Kind = TooltipItemKind.Unknown;
        public int DataId;
        public string Name;
        public string IconKey;
        public int Tier;

        // 재료는 전용 섹션이 없다(헤더만 표시).
        public FishSection Fish;
        public EquipmentSection Equipment;
        public ConsumableSection Consumable;

        /// <summary>물고기 상세. 성급·크기는 개체 롤값이고 나머지는 종 정의값이다.</summary>
        public class FishSection
        {
            public ItemRarity Rarity;
            public int Star;
            public float Size;
            public int Capacity;              // 아쿠아리움 수용량 비용
            public string MaterialName;       // 생산 재료
            public string MaterialIconKey;
            public float CycleSeconds;        // 개체 성급이 반영된 생산 주기
            public float PointsPerMinute;

            /// <summary>생산 지표가 채워졌는지. false면 패널이 생산 구역을 감춘다.</summary>
            public bool HasAquariumInfo;
        }

        public class EquipmentSection
        {
            public EquipmentMountingArea MountingArea;
            public int UpgradeLevel;
            public int MaxUpgradeLevel;
            public StatModifier[] Modifiers;  // 현재 강화 단계의 효과
            public UpgradeStep NextStep;      // 다음 강화 비용/효과(최대면 null)
        }

        public class ConsumableSection
        {
            public EquipmentMountingArea Category;
            public StatModifier[] Modifiers;
            public int Quantity;
            public MaterialCost[] CraftMaterials;
            public int CraftGoldCost;
        }
    }

    /// <summary>
    /// 슬롯이 hover된 순간에 상세 데이터를 조립해 주는 공급자. 슬롯을 보유한 창이 구현한다.
    /// </summary>
    public interface IItemTooltipSource
    {
        ItemTooltipData BuildTooltip(ItemSlotVD vd);
    }
}
