using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>EquipmentValueRule 시트의 한 행. ID 참조와 값은 EquipmentRule에서 검증한다.</summary>
    public sealed class EquipmentValueRule : GameData
    {
        [SerializeField] private EquipmentValueDistribution m_distribution;
        [SerializeField] private float m_min;
        [SerializeField] private float m_max;
        [SerializeField] private float m_step;

        public EquipmentValueDistribution Distribution => m_distribution;
        public float Min => m_min;
        public float Max => m_max;
        public float Step => m_step;
    }
}
