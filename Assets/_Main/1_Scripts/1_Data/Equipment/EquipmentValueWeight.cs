using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>EquipmentValueWeight 시트의 한 행. ID 참조와 값은 EquipmentRule에서 검증한다.</summary>
    public sealed class EquipmentValueWeight : GameData
    {
        [SerializeField] private int m_ruleId;
        [SerializeField] private float m_value;
        [SerializeField] private int m_weight;

        public int RuleId => m_ruleId;
        public float Value => m_value;
        public int Weight => m_weight;
    }
}
