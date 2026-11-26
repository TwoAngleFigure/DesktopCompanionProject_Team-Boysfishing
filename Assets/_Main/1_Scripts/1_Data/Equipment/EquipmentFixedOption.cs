using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>EquipmentFixedOption 시트의 한 행. ID 참조와 값은 EquipmentRule에서 검증한다.</summary>
    public sealed class EquipmentFixedOption : GameData
    {
        [SerializeField] private int m_profileId;
        [SerializeField] private int m_index;
        [SerializeField] private PlayerStat_New m_stat;
        [SerializeField] private ModifierOperation_New m_operation;
        [SerializeField] private float m_value;

        public int ProfileId => m_profileId;
        public int Index => m_index;
        public PlayerStat_New Stat => m_stat;
        public ModifierOperation_New Operation => m_operation;
        public float Value => m_value;
    }
}
