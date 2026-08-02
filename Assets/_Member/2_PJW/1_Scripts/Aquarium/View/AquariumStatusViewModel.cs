using System.Collections.Generic;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 재료 생산 경과와 수족관 정보 창의 ViewModel. 갱신 경로가 둘로 나뉜다.
    ///  - 구조 변경(배치·회수·강화·인벤토리 변동): 목록을 재구성하고 <see cref="Materials"/>를 재할당한다.
    ///  - 1초 정산: 기존 VD의 수치만 제자리 갱신하고 <see cref="ProgressTick"/>을 올린다.
    /// 정렬은 구조 변경과 사용자 조작 시에만 다시 적용한다.
    /// </summary>
    public class AquariumStatusViewModel : UIViewModelBase
    {
        private AquariumSystem m_aquarium;
        private InventorySystem m_inventory;

        private readonly List<AquariumMaterialVD> m_materials = new();
        private readonly Dictionary<int, AquariumMaterialVD> m_materialById = new();

        public readonly BindableProperty<List<AquariumMaterialVD>> Materials = new(new List<AquariumMaterialVD>());
        public readonly BindableProperty<int> ProgressTick = new(0);
        public readonly BindableProperty<AquariumInfoVD> Info = new(null);

        public RelayCommand UpgradeCommand { get; private set; }

        private AquariumMaterialSortKey m_sortKey;
        private SortDirection m_sortDirection;

        public AquariumMaterialSortKey SortKey => m_sortKey;
        public SortDirection SortDirection => m_sortDirection;

        public override void Bind()
        {
            m_aquarium = SystemManager.GetSystem<AquariumSystem>();
            m_inventory = SystemManager.GetSystem<InventorySystem>();

            (m_sortKey, m_sortDirection) = AquariumUiSettings.LoadMaterialSort(AquariumUiSettings.ListMaterial);

            UpgradeCommand = new RelayCommand(() => m_aquarium?.TryUpgrade());

            if (m_aquarium != null)
            {
                m_aquarium.OnAquariumChanged += RebuildAll;
                m_aquarium.OnAquariumProgress += ApplyProgress;
            }
            if (m_inventory != null)
            {
                m_inventory.OnInventoryChanged += RefreshInfo;   // 강화 재료 보유량 변동
            }
            RebuildAll();
        }

        public override void Unbind()
        {
            if (m_aquarium != null)
            {
                m_aquarium.OnAquariumChanged -= RebuildAll;
                m_aquarium.OnAquariumProgress -= ApplyProgress;
            }
            if (m_inventory != null)
            {
                m_inventory.OnInventoryChanged -= RefreshInfo;
            }
            m_aquarium = null;
            m_inventory = null;
        }

        public void SetSort(AquariumMaterialSortKey key, SortDirection direction)
        {
            m_sortKey = key;
            m_sortDirection = direction;
            AquariumUiSettings.SaveMaterialSort(AquariumUiSettings.ListMaterial, key, direction);
            RebuildAll();
        }

        // ── 구조 변경 경로 ──
        private void RebuildAll()
        {
            if (m_aquarium == null)
            {
                return;
            }

            m_materials.Clear();
            m_materialById.Clear();
            foreach (AquariumSystem.MaterialStatus status in m_aquarium.GetMaterialStatuses())
            {
                AquariumMaterialVD vd = AquariumMaterialVD.From(status, m_aquarium);
                m_materials.Add(vd);
                m_materialById[vd.MaterialId] = vd;
            }
            AquariumSort.Apply(m_materials, m_sortKey, m_sortDirection);

            // 리스트 인스턴스를 새로 만들어 넘긴다 — 참조가 바뀌어야 View가 행을 재구성한다.
            Materials.Value = new List<AquariumMaterialVD>(m_materials);
            RefreshInfo();
        }

        // ── 1초 정산 경로 ──
        private void ApplyProgress()
        {
            if (m_aquarium == null)
            {
                return;
            }

            List<AquariumSystem.MaterialStatus> statuses = m_aquarium.GetMaterialStatuses();

            // 재료 종류가 늘거나 줄었으면 구조 변경으로 처리(행 수가 달라져야 한다).
            if (statuses.Count != m_materials.Count)
            {
                RebuildAll();
                return;
            }

            foreach (AquariumSystem.MaterialStatus status in statuses)
            {
                if (m_materialById.TryGetValue(status.MaterialId, out AquariumMaterialVD vd) == false)
                {
                    RebuildAll();
                    return;
                }
                vd.Points = status.Points;
                vd.Required = status.Required;
                vd.Pending = status.Pending;
                vd.PerHour = status.PerHour;
            }

            ProgressTick.Value++;   // 수치만 갱신됐음을 알린다(행 재생성 없음)
            RefreshInfo();          // 골드는 전용 이벤트가 없어 1초 주기로 반영한다
        }

        private void RefreshInfo() => Info.Value = AquariumInfoVD.From(m_aquarium);
    }
}
