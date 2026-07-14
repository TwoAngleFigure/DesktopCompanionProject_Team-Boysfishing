using System.Collections.Generic;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 인벤토리 미러(배치/회수 + 상세)의 ViewModel.
    /// Deployable=인벤토리 물고기(종별 집계, 배치용), Placed=아쿠아리움 배치 목록(회수용), Selected=상세.
    /// </summary>
    public class AquariumMirrorViewModel : UIViewModelBase
    {
        private AquariumSystem m_aquarium;
        private InventorySystem m_inventory;

        public readonly BindableProperty<List<AquariumMirrorFishVD>> Deployable = new(new List<AquariumMirrorFishVD>());
        public readonly BindableProperty<List<AquariumMirrorFishVD>> Placed = new(new List<AquariumMirrorFishVD>());
        public readonly BindableProperty<AquariumMirrorFishVD> Selected = new(null);

        public RelayCommand<int> AddFishCommand { get; private set; }      // dataId
        public RelayCommand<int> RemoveFishCommand { get; private set; }   // index
        public RelayCommand<int> SelectFishCommand { get; private set; }   // dataId

        public override void Bind()
        {
            m_aquarium = SystemManager.GetSystem<AquariumSystem>();
            m_inventory = SystemManager.GetSystem<InventorySystem>();

            AddFishCommand = new RelayCommand<int>(id => m_aquarium?.AddFish(id));
            RemoveFishCommand = new RelayCommand<int>(i => m_aquarium?.RemoveFishAt(i));
            SelectFishCommand = new RelayCommand<int>(id => Selected.Value = BuildDetail(id));

            if (m_aquarium != null) m_aquarium.OnAquariumChanged += Refresh;
            if (m_inventory != null) m_inventory.OnInventoryChanged += Refresh;
            Refresh();
        }

        public override void Unbind()
        {
            if (m_aquarium != null) m_aquarium.OnAquariumChanged -= Refresh;
            if (m_inventory != null) m_inventory.OnInventoryChanged -= Refresh;
            m_aquarium = null;
            m_inventory = null;
        }

        private void Refresh()
        {
            // Deployable — 인벤토리 fish 슬롯 종별 집계(InventoryViewModel과 동형)
            var deploy = new List<AquariumMirrorFishVD>();
            if (m_inventory != null)
            {
                var counts = new Dictionary<int, int>();
                var datas = new Dictionary<int, ItemData_Fish>();
                var order = new List<int>();
                EntityHandle[] slots = m_inventory.GetSlots(ItemType.Fish);
                for (int i = 0; i < slots.Length; i++)
                {
                    var fish = EntityManager.Get<Entity_Fish>(slots[i]);   // 빈/유실 핸들은 null
                    if (fish == null) continue;
                    int id = fish.DataId;
                    if (!counts.ContainsKey(id)) { counts[id] = 0; datas[id] = fish.ItemData; order.Add(id); }
                    counts[id]++;
                }
                foreach (int id in order)
                {
                    var vd = BuildVD(datas[id], id, counts[id], -1);
                    if (vd != null) deploy.Add(vd);
                }
            }
            Deployable.Value = deploy;

            // Placed — 아쿠아리움 배치 목록(인덱스 = 회수 대상)
            var placed = new List<AquariumMirrorFishVD>();
            if (m_aquarium != null)
            {
                var ids = m_aquarium.FishDataIds;
                for (int i = 0; i < ids.Count; i++)
                {
                    var vd = BuildVD(m_aquarium.GetFishData(ids[i]), ids[i], 0, i);
                    if (vd != null) placed.Add(vd);
                }
            }
            Placed.Value = placed;
        }

        private AquariumMirrorFishVD BuildDetail(int dataId)
            => m_aquarium != null ? BuildVD(m_aquarium.GetFishData(dataId), dataId, 0, -1) : null;

        private AquariumMirrorFishVD BuildVD(ItemData_Fish f, int dataId, int count, int index)
        {
            if (f == null) return null;
            string matName = f.AquariumMaterial != null ? f.AquariumMaterial.Name : "-";
            return new AquariumMirrorFishVD
            {
                DataId = dataId,
                Name = f.Name,
                Star = (int)f.Star,
                Size = f.Size,
                MaterialName = matName,
                PointsPerHour = m_aquarium != null ? m_aquarium.PointsPerHour(f) : 0f,
                IconKey = AssetKeys.Of(f, AssetUsage.Icon),
                Index = index,
                Count = count,
            };
        }
    }
}
