using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>EquipmentOptionPool 시트의 한 행. ID 참조와 값은 EquipmentRule에서 검증한다.</summary>
    public sealed class EquipmentOptionPool : GameData
    {
        [SerializeField] private int m_groupId;
        [SerializeField] private int m_tier;
        [SerializeField] private ItemRarity m_rarity;
        [SerializeField] private PlayerStat_New m_stat;
        [SerializeField] private ModifierOperation_New m_operation;
        [SerializeField] private int m_weight;
        [SerializeField] private int m_valueRuleId;

        public int GroupId => m_groupId;
        public int Tier => m_tier;
        public ItemRarity Rarity => m_rarity;
        public PlayerStat_New Stat => m_stat;
        public ModifierOperation_New Operation => m_operation;
        public int Weight => m_weight;
        public int ValueRuleId => m_valueRuleId;
    }
}
