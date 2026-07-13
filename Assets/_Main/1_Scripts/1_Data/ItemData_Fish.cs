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
        [SerializeField] private int m_aquariumCapacity;                   // 아쿠아리움 수용량 비용
        [SerializeField] private int m_aquariumProduceAmount;              // 주기 1회당 적립 생산 포인트

        [Header("Spec")]
        // ※ 아쿠아리움용 종(種) 정의값. 포획 개체(Entity_Fish)의 롤값과는 독립이며, 아쿠아리움은 이 값을 읽는다.
        [SerializeField] private ItemQuality m_star = ItemQuality.OneStar; // 성급(생산 주기 감소 계산)
        [SerializeField] private float m_size;                            // 크기(표시/후속 확장용)

        public ItemRarity Rarity => m_rarity;
        public ItemData_Materials AquariumMaterial => m_aquariumMaterial;
        public float AquariumProduceTime => m_aquariumProduceTime;
        public int AquariumDecreaseCount => m_aquariumDecreaseCount;
        public int AquariumCapacity => m_aquariumCapacity;
        public int AquariumProduceAmount => m_aquariumProduceAmount;
        public ItemQuality Star => m_star;
        public float Size => m_size;
    }
}
