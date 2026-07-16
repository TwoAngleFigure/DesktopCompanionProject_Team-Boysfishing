using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>
    /// 아쿠아리움 수용량 업그레이드의 레벨별 정의. m_id = 업그레이드 레벨(1부터).
    /// 비용 구조는 장비 강화(EquipmentUpgrades / <see cref="UpgradeStep"/>)와 동형이다.
    /// 레벨 N의 비용은 "N-1 → N 업그레이드에 드는 비용"으로 해석한다(장비 GetNextUpgradeStep과 동일 관례).
    /// Entity화하지 않는 설정 데이터라 EntityManager 팩토리 등록은 불필요하다(System이 직접 조회).
    /// </summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Aquarium/Upgrade")]
    public class AquariumUpgradeData : GameData
    {
        [SerializeField] private int m_maxCapacity;               // 이 레벨에서의 최대 수용량
        [SerializeField] private int m_upgradeCost;              // 이 레벨 도달 골드 비용
        [SerializeField] private MaterialCost[] m_materialCosts; // 이 레벨 도달 재료 비용

        public int MaxCapacity => m_maxCapacity;
        public int UpgradeCost => m_upgradeCost;
        public MaterialCost[] MaterialCosts => m_materialCosts;
    }
}
