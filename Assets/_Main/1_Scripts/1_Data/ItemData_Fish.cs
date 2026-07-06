using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>물고기 종(species)의 정적 정의.</summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Item/Fish")]
    public class ItemData_Fish : ItemData
    {
        // ※ 크기/성급은 낚을 때 개체별로 롤되는 값 → Entity_Fish 소관(G3). 여기엔 종 정의만.
        [SerializeField] private ItemRarity m_rarity;   // 종별 고정 등급(G1)

        [Header("Aquarium")]
        [SerializeField] private ItemData_Materials m_aquariumMaterial;    // 배치 시 생산 재료
        [SerializeField] private float m_aquariumProduceTime;              // 기본 생산 주기(초)
        [SerializeField] private int m_aquariumDecreaseCount;              // 성급당 주기 감소량

        public ItemRarity Rarity => m_rarity;
        public ItemData_Materials AquariumMaterial => m_aquariumMaterial;
        public float AquariumProduceTime => m_aquariumProduceTime;
        public int AquariumDecreaseCount => m_aquariumDecreaseCount;
    }
}
