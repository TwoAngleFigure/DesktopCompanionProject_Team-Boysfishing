using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>재료의 정적 정의. 티어·기준가는 베이스에서 상속, 스택 수량은 Entity_Materials.Quantity 소관.</summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Item/Materials")]
    public class ItemData_Materials : ItemData
    {
        [Header("Aquarium")]
        [SerializeField] private int m_aquariumRequiredPoints = 1;   // 재료 1개 생산에 필요한 누적 포인트

        public int AquariumRequiredPoints => Mathf.Max(1, m_aquariumRequiredPoints);   // 0/음수 방지(나눗셈 안전)
    }
}
