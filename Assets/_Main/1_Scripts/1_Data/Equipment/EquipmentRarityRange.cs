using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>EquipmentRarityRange 시트의 한 행. ID 참조와 값은 EquipmentRule에서 검증한다.</summary>
    public sealed class EquipmentRarityRange : GameData
    {
        [SerializeField] private int m_tier;
        [SerializeField] private ItemRarity m_rarity;
        [SerializeField] private float m_minInclusive;
        [SerializeField] private float m_maxExclusive;

        public int Tier => m_tier;
        public ItemRarity Rarity => m_rarity;
        public float MinInclusive => m_minInclusive;
        public float MaxExclusive => m_maxExclusive;
    }
}
