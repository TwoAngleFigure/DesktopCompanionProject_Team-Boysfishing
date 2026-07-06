using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>장비의 정적 정의(부위·기본 효과·강화 테이블).</summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Item/Equipment")]
    public class ItemData_Equipment : ItemData
    {
        // ※ 현재 강화 단계는 개체별 상태 → Entity_Equipment.UpgradeLevel 소관.
        [SerializeField] private EquipmentMountingArea m_mountingArea;
        [SerializeField] private StatModifier[] m_baseModifiers;   // 0강(기본) 효과
        [SerializeField] private UpgradeStep[] m_upgradeSteps;     // [0]=1강 … [n-1]=n강

        public EquipmentMountingArea MountingArea => m_mountingArea;
        public int MaxUpgradeLevel => m_upgradeSteps != null ? m_upgradeSteps.Length : 0;

        /// <summary>해당 강화 단계의 효과(누적치 정의). 0강=기본 효과.</summary>
        public StatModifier[] GetModifiers(int upgradeLevel)
        {
            if (upgradeLevel <= 0 || m_upgradeSteps == null || m_upgradeSteps.Length == 0)
            {
                return m_baseModifiers;
            }
            int index = Mathf.Clamp(upgradeLevel, 1, m_upgradeSteps.Length) - 1;
            return m_upgradeSteps[index].Modifiers;
        }

        /// <summary>다음 단계(currentLevel+1)로 올리는 비용/효과. 최대 단계면 null.</summary>
        public UpgradeStep GetNextUpgradeStep(int currentLevel)
        {
            if (m_upgradeSteps == null || currentLevel >= m_upgradeSteps.Length)
            {
                return null;
            }
            return m_upgradeSteps[currentLevel];
        }
    }
}
