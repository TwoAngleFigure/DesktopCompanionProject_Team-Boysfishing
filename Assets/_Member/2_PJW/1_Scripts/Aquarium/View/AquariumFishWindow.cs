using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 물고기 목록 창(계획 27 W2). 위=인벤토리 물고기([넣기]), 아래=수족관 물고기([빼기]).
    /// 두 목록 모두 개체 단위이며, 같은 항목을 비교하므로 정렬 바(기준 + 오름/내림) 하나를 공유한다.
    /// 액션은 <see cref="EntityHandle"/>로 개체를 지목하므로 정렬을 바꿔도 대상이 어긋나지 않는다.
    /// 각 행의 상세는 슬롯 hover 팝업이 담당한다(계획 28) — 이 창이 그 상세의 공급자다.
    /// </summary>
    public class AquariumFishWindow : UIWindowBase, IItemTooltipSource
    {
        [Header("인벤토리 목록")]
        [SerializeField] private Transform m_inventoryRoot;

        [Header("수족관 목록")]
        [SerializeField] private Transform m_placedRoot;

        [Header("공용")]
        [Tooltip("두 목록에 함께 적용되는 정렬 바")]
        [SerializeField] private SortBarView m_sortBar;
        [SerializeField] private AquariumFishRow m_rowPrefab;
        [SerializeField] private TMP_Text m_capacityText;
        [SerializeField] private Button m_closeButton;

        private readonly AquariumFishListViewModel m_vm = new();
        private readonly List<AquariumFishRow> m_inventoryRows = new();
        private readonly List<AquariumFishRow> m_placedRows = new();

        private AquariumSystem m_aquarium;   // 툴팁의 아쿠아리움 지표 조립용

        public override void Bind()
        {
            m_aquarium = SystemManager.GetSystem<AquariumSystem>();

            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.Inventory.Bind(RefreshInventory);
            m_vm.Placed.Bind(RefreshPlaced);
            m_vm.CapacityText.Bind(RefreshCapacity);

            if (m_sortBar != null)
            {
                m_sortBar.Initialize(AquariumSort.FishSortLabels,
                    (int)m_vm.SortKey, m_vm.SortDirection,
                    (key, direction) => m_vm.SetSort((AquariumFishSortKey)key, direction));
            }

            if (m_closeButton != null) m_closeButton.onClick.AddListener(Close);
        }

        public override void Unbind()
        {
            m_vm.Inventory.Unbind(RefreshInventory);
            m_vm.Placed.Unbind(RefreshPlaced);
            m_vm.CapacityText.Unbind(RefreshCapacity);
            m_vm.Unbind();

            if (m_sortBar != null) m_sortBar.Release();
            if (m_closeButton != null) m_closeButton.onClick.RemoveListener(Close);

            m_aquarium = null;
        }

        /// <summary>슬롯이 hover된 순간에만 호출된다(목록 갱신 때 전 행의 상세를 미리 만들지 않는다).</summary>
        public ItemTooltipData BuildTooltip(ItemSlotVD vd)
            => vd != null && vd.HasEntity
                ? ItemTooltipBuilder.FromEntity(vd.Handle, EntityManager, m_aquarium)
                : null;

        private void RefreshCapacity(string text)
        {
            if (m_capacityText != null) m_capacityText.text = text;
        }

        private void RefreshInventory(List<AquariumFishVD> list)
            => Fill(m_inventoryRows, m_inventoryRoot, list, "넣기", handle => m_vm.AddCommand.Execute(handle));

        private void RefreshPlaced(List<AquariumFishVD> list)
            => Fill(m_placedRows, m_placedRoot, list, "빼기", handle => m_vm.RemoveCommand.Execute(handle));

        private void Fill(List<AquariumFishRow> rows, Transform root, List<AquariumFishVD> list,
                          string actionText, Action<EntityHandle> onAction)
        {
            if (m_rowPrefab == null || root == null || list == null)
            {
                return;
            }

            while (rows.Count < list.Count) rows.Add(Instantiate(m_rowPrefab, root));

            AquariumFishSortKey sortKey = m_vm.SortKey;
            for (int i = 0; i < list.Count; i++)
            {
                AquariumFishVD vd = list[i];
                ItemSlotVD slotVD = ItemSlotVD.FromEntity(vd.Handle, EntityManager);

                Sprite icon = null;
                if (string.IsNullOrEmpty(vd.IconKey) == false) AssetProvider.TryGet(vd.IconKey, out icon);

                rows[i].gameObject.SetActive(true);
                rows[i].Set(vd, slotVD, icon, this, sortKey, actionText, onAction);
            }
            for (int i = list.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
        }
    }
}
