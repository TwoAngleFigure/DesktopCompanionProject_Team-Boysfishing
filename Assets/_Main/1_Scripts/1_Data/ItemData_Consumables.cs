using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>소모품의 정적 정의(슬롯 장착형, G4). 스택 수량은 Entity_Consumables.Quantity 소관.</summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Item/Consumables")]
    public class ItemData_Consumables : ItemData
    {
        [SerializeField] private EquipmentMountingArea m_mountingArea;   // Bait/Groundbait
        [SerializeField] private StatModifier[] m_modifiers;             // 장착 중 효과
        [SerializeField] private BattleFishData m_summonTarget;          // 보스 소환용이면 대상(null=일반)

        [Header("Crafting")]
        [SerializeField] private MaterialCost[] m_craftMaterials;        // 제작 레시피
        [SerializeField] private int m_craftGoldCost;

        public EquipmentMountingArea MountingArea => m_mountingArea;
        public StatModifier[] Modifiers => m_modifiers;
        public BattleFishData SummonTarget => m_summonTarget;
        public MaterialCost[] CraftMaterials => m_craftMaterials;
        public int CraftGoldCost => m_craftGoldCost;
    }
}
