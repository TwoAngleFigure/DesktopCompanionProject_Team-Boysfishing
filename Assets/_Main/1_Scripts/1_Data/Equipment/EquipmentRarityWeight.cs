using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>EquipmentRarityWeight 시트의 한 행. ID 참조와 값은 EquipmentRule에서 검증한다.</summary>
    public sealed class EquipmentRarityWeight : GameData
    {
        [SerializeField] private int m_groupId;
        [SerializeField] private int m_tier;
        [SerializeField] private ItemRarity m_rarity;
        [SerializeField] private int m_weight;

        public int GroupId => m_groupId;
        public int Tier => m_tier;
        public ItemRarity Rarity => m_rarity;
        public int Weight => m_weight;
    }
}
