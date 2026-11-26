using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>EquipmentFixedProfile 시트의 한 행. ID 참조와 값은 EquipmentRule에서 검증한다.</summary>
    public sealed class EquipmentFixedProfile : GameData
    {
        [SerializeField] private int m_equipmentId;
        [SerializeField] private ItemRarity m_rarity;
        [SerializeField] private float? m_mainValue;

        public int EquipmentId => m_equipmentId;
        public ItemRarity Rarity => m_rarity;
        public float? MainValue => m_mainValue;
    }
}
