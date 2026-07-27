using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 툴팁 팝업 1회분 상세 스냅샷(계획 28). 문자열을 미리 조립하지 않고 <b>값 그대로</b> 담는다 —
    /// 문장 만들기는 종류별 패널이 각자 한다(패널마다 표현이 다르므로).
    /// <see cref="Kind"/>에 해당하는 섹션 하나만 채워진다.
    /// </summary>
    public class ItemTooltipData
    {
        public TooltipItemKind Kind = TooltipItemKind.Unknown;
        public int DataId;
        public string Name;
        public string IconKey;
        public int Tier;

        // ※ 재료는 별도 섹션이 없다 — 아쿠아리움 진행 상황은 재료 게이지 행이 이미 보여주므로
        //    툴팁에서 반복하지 않는다(헤더의 아이콘·이름·티어만 표시).
        public FishSection Fish;
        public EquipmentSection Equipment;
        public ConsumableSection Consumable;

        /// <summary>물고기 상세. 성급·크기는 개체 롤값(계획 27 P5).</summary>
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

            /// <summary>AquariumSystem 없이 만들면 false → 패널이 생산 구역을 감춘다.</summary>
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
    /// 슬롯이 hover된 순간에 상세를 만들어 주는 공급자. 창이 구현한다.
    /// 목록을 갱신할 때마다 모든 행의 상세를 미리 만들면 낭비이므로, 슬롯은 이 참조만 들고 있다가
    /// 마우스가 올라온 순간에 1건만 조립한다.
    /// </summary>
    public interface IItemTooltipSource
    {
        ItemTooltipData BuildTooltip(ItemSlotVD vd);
    }
}
