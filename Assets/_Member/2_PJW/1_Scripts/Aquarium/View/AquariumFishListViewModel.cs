using System.Collections.Generic;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 물고기 목록 창의 ViewModel — 인벤토리 물고기(넣기)와 수족관 물고기(빼기)를 '개체 단위'로 제공한다.
    /// 구조 변경(배치·회수·인벤토리 변동·자동판매 설정)에만 재빌드하고, 1초 정산에는 반응하지 않는다
    /// (분당 포인트는 정적값이라 초 단위 갱신이 불필요 — 계획 27 R-4).
    /// </summary>
    public class AquariumFishListViewModel : UIViewModelBase
    {
        private AquariumSystem m_aquarium;
        private InventorySystem m_inventory;

        public readonly BindableProperty<List<AquariumFishVD>> Inventory = new(new List<AquariumFishVD>());
        public readonly BindableProperty<List<AquariumFishVD>> Placed = new(new List<AquariumFishVD>());
        public readonly BindableProperty<string> CapacityText = new(string.Empty);

        public RelayCommand<EntityHandle> AddCommand { get; private set; }
        public RelayCommand<EntityHandle> RemoveCommand { get; private set; }

        // 인벤토리·수족관 목록은 같은 항목을 비교하므로 정렬 기준/방향을 하나로 공유한다.
        private AquariumFishSortKey m_sortKey;
        private SortDirection m_sortDirection;

        public AquariumFishSortKey SortKey => m_sortKey;
        public SortDirection SortDirection => m_sortDirection;

        public override void Bind()
        {
            m_aquarium = SystemManager.GetSystem<AquariumSystem>();
            m_inventory = SystemManager.GetSystem<InventorySystem>();

            (m_sortKey, m_sortDirection) = AquariumUiSettings.LoadFishSort(AquariumUiSettings.ListFish);

            AddCommand = new RelayCommand<EntityHandle>(h => m_aquarium?.AddFish(h));
            RemoveCommand = new RelayCommand<EntityHandle>(h => m_aquarium?.RemoveFish(h));

            if (m_aquarium != null)
            {
                m_aquarium.OnAquariumChanged += Refresh;
            }
            if (m_inventory != null)
            {
                m_inventory.OnInventoryChanged += Refresh;         // 보유 물고기·창고 만석 변동
                m_inventory.OnAutoSellFilterChanged += Refresh;    // 자동판매 설정 변동 → [빼기] 활성 갱신
            }
            Refresh();
        }

        public override void Unbind()
        {
            if (m_aquarium != null)
            {
                m_aquarium.OnAquariumChanged -= Refresh;
            }
            if (m_inventory != null)
            {
                m_inventory.OnInventoryChanged -= Refresh;
                m_inventory.OnAutoSellFilterChanged -= Refresh;
            }
            m_aquarium = null;
            m_inventory = null;
        }

        /// <summary>두 목록(인벤토리·수족관)에 함께 적용되는 정렬을 바꾼다.</summary>
        public void SetSort(AquariumFishSortKey key, SortDirection direction)
        {
            m_sortKey = key;
            m_sortDirection = direction;
            AquariumUiSettings.SaveFishSort(AquariumUiSettings.ListFish, key, direction);
            Refresh();
        }

        private void Refresh()
        {
            if (m_aquarium == null)
            {
                return;
            }

            // 인벤토리 — 슬롯을 개체 단위로 펼친다(종별 집계 없음).
            var inventory = new List<AquariumFishVD>();
            if (m_inventory != null)
            {
                EntityHandle[] slots = m_inventory.GetSlots(ItemType.Fish);
                for (int i = 0; i < slots.Length; i++)
                {
                    if (EntityManager.Get<Entity_Fish>(slots[i]) == null) continue;   // 빈/유실 슬롯

                    var vd = AquariumFishVD.From(m_aquarium.BuildFishInfo(slots[i]), m_aquarium);
                    vd.CanAct = m_aquarium.CanPlaceFish(slots[i], out string placeReason);
                    vd.BlockReason = placeReason;
                    inventory.Add(vd);
                }
            }
            AquariumSort.Apply(inventory, m_sortKey, m_sortDirection);
            Inventory.Value = inventory;

            // 수족관 — 회수가 곧 판매가 되는 상황은 버튼 단계에서 차단한다(계획 27 P6).
            var placed = new List<AquariumFishVD>();
            foreach (AquariumSystem.AquariumFishInfo info in m_aquarium.GetPlacedFishInfos())
            {
                var vd = AquariumFishVD.From(info, m_aquarium);
                vd.CanAct = m_aquarium.CanRetrieveFish(info.Handle, out string retrieveReason);
                vd.BlockReason = retrieveReason;
                placed.Add(vd);
            }
            AquariumSort.Apply(placed, m_sortKey, m_sortDirection);
            Placed.Value = placed;

            CapacityText.Value = $"수용량 {m_aquarium.UsedCapacity} / {m_aquarium.MaxCapacity}";
        }
    }
}
