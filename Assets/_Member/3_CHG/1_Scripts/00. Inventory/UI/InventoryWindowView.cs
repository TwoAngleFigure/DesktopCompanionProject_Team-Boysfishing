using System.Collections.Generic;
using System.Text;
using DesktopCompanion.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class InventoryWindowView : UIWindowBase
    {
        [Header("Tab Buttons")]
        [SerializeField] private Button m_fishTabButton;
        [SerializeField] private Button m_equipmentTabButton;
        [SerializeField] private Button m_materialTabButton;

        [Header("Window Buttons")]
        [SerializeField] private Button m_closeButton;

        [Header("Slot Grid")]
        [SerializeField] private Transform m_slotRoot;
        [SerializeField] private InventorySlotView m_slotPrefab;

        [Header("Gold")]
        [SerializeField] private TMP_Text m_goldText;

        [Header("Hover Tooltip")]
        [SerializeField] private InventoryItemTooltipView m_itemTooltip;

        [Header("Item Pickup")]
        [SerializeField] private ItemPickupController m_itemPickupController;

        private readonly InventoryViewModel m_vm = new();
        private readonly List<InventorySlotView> m_slotViews = new();

        private int m_hoveredSlotIndex = -1;

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.Slots.Bind(RefreshSlotViews);
            m_vm.Gold.Bind(RefreshGoldText);

            if (m_itemPickupController != null)
            {
                m_itemPickupController.OnPickupChanged += RefreshPickupSourceFrames;
            }
            else
            {
                Debug.LogError("[InventoryWindowView] ItemPickupController is not assigned.");
            }

            if (m_itemTooltip == null)
            {
                Debug.LogError("[InventoryWindowView] InventoryItemTooltipView is not assigned.");
            }

            if (m_fishTabButton != null)
            {
                m_fishTabButton.onClick.AddListener(OnFishTabClicked);
            }

            if (m_equipmentTabButton != null)
            {
                m_equipmentTabButton.onClick.AddListener(OnEquipmentTabClicked);
            }

            if (m_materialTabButton != null)
            {
                m_materialTabButton.onClick.AddListener(OnMaterialTabClicked);
            }

            if (m_closeButton != null)
            {
                m_closeButton.onClick.AddListener(OnCloseButtonClicked);
            }
        }

        public override void Unbind()
        {
            ClearHoveredTooltip();

            if (m_itemPickupController != null)
            {
                m_itemPickupController.OnPickupChanged -= RefreshPickupSourceFrames;
                m_itemPickupController.ClearPickup();
            }

            m_vm.Slots.Unbind(RefreshSlotViews);
            m_vm.Gold.Unbind(RefreshGoldText);

            if (m_fishTabButton != null)
            {
                m_fishTabButton.onClick.RemoveListener(OnFishTabClicked);
            }

            if (m_equipmentTabButton != null)
            {
                m_equipmentTabButton.onClick.RemoveListener(OnEquipmentTabClicked);
            }

            if (m_materialTabButton != null)
            {
                m_materialTabButton.onClick.RemoveListener(OnMaterialTabClicked);
            }

            if (m_closeButton != null)
            {
                m_closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            }

            m_vm.Unbind();
        }

        private void RefreshSlotViews(List<InventorySlotViewData> slots)
        {
            if (slots == null)
            {
                return;
            }

            if (!EnsureSlotViews(slots.Count))
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotViewData slotData = slots[i];
                Sprite icon = GetIcon(slotData);

                m_slotViews[i].Set(slotData, icon);
            }

            for (int i = slots.Count; i < m_slotViews.Count; i++)
            {
                m_slotViews[i].gameObject.SetActive(false);
            }

            RefreshPickupSourceFrames();

            RefreshHoveredTooltip();
        }

        private bool EnsureSlotViews(int requiredCount)
        {
            if (m_slotPrefab == null)
            {
                Debug.LogError($"[InventoryWindowView] Slot prefab is not assigned. {this}");
                return false;
            }

            if (m_slotRoot == null)
            {
                Debug.LogError("[InventoryWindowView] Slot root is not assigned.");
                return false;
            }

            while (m_slotViews.Count < requiredCount)
            {
                InventorySlotView slotView = Instantiate(m_slotPrefab, m_slotRoot);
                int slotIndex = m_slotViews.Count;

                slotView.Initialize(
                    slotIndex,
                    OnSlotClicked,
                    OnSlotDoubleClicked,
                    OnSlotPointerEntered,
                    OnSlotPointerExited);

                m_slotViews.Add(slotView);
            }

            for (int i = 0; i < requiredCount; i++)
            {
                m_slotViews[i].gameObject.SetActive(true);
            }

            return true;
        }

        private void OnSlotClicked(int slotIndex)
        {
            List<InventorySlotViewData> slots = m_vm.Slots.Value;

            if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            {
                return;
            }

            InventorySlotViewData clickedSlot = slots[slotIndex];

            if (clickedSlot == null)
            {
                return;
            }

            m_vm.SelectSlotCommand.Execute(slotIndex);

            if (m_itemPickupController == null)
            {
                return;
            }

            // 현재 집고 있는 아이템이 없으면 새 Pickup 시작
            if (!m_itemPickupController.HasItem)
            {
                if (clickedSlot.IsEmpty)
                {
                    return;
                }

                ClearHoveredTooltip();
                BeginPickup(clickedSlot);
                return;
            }

            // 장비창에서 선택한 장비를 인벤토리에 내려놓는 경우
            if (m_itemPickupController.Source == ItemPickupSource.Equipment)
            {
                // 장비탭 한정
                if (clickedSlot.SlotType != ItemType.Equipment)
                {
                    return;
                }

                bool placed = m_vm.PlaceEquippedItemAtSlot(
                    m_itemPickupController.SourceEquipmentArea,
                    slotIndex);

                if (placed)
                {
                    ClearHoveredTooltip();
                    m_itemPickupController.ClearPickup();
                }

                return;
            }

            // 다른 인벤토리 탭의 슬롯과는 교환 금지
            if (m_itemPickupController.SourceSlotType != clickedSlot.SlotType)
            {
                return;
            }

            // 출발 슬롯을 다시 클릭하면 Pickup 취소
            if (m_itemPickupController.SourceSlotIndex == slotIndex)
            {
                m_itemPickupController.ClearPickup();
                return;
            }

            if (!IsPickedItemStillValid())
            {
                m_itemPickupController.ClearPickup();
                return;
            }

            bool swapped = m_vm.SwapSlots(m_itemPickupController.SourceSlotIndex, slotIndex);

            if (swapped)
            {
                m_itemPickupController.ClearPickup();
            }
        }

        private void OnSlotDoubleClicked(int slotIndex)
        {
            List<InventorySlotViewData> slots = m_vm.Slots.Value;

            if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            {
                return;
            }

            InventorySlotViewData slotData = slots[slotIndex];

            if (slotData == null || slotData.IsEmpty)
            {
                return;
            }

            if (slotData.ItemType != ItemType.Equipment)
            {
                return;
            }

            ClearHoveredTooltip();

            bool equipped = m_vm.EquipEquipmentAtSlot(slotIndex);

            if (equipped)
            {
                m_itemPickupController?.ClearPickup();
            }
        }

        private void OnSlotPointerEntered(int slotIndex)
        {
            m_hoveredSlotIndex = slotIndex;
            RefreshHoveredTooltip();
        }

        private void OnSlotPointerExited(int slotIndex)
        {
            if (m_hoveredSlotIndex != slotIndex)
            {
                return;
            }

            ClearHoveredTooltip();
        }

        private void RefreshHoveredTooltip()
        {
            if (m_itemTooltip == null || m_hoveredSlotIndex < 0)
            {
                return;
            }

            List<InventorySlotViewData> slots = m_vm.Slots.Value;

            if (slots == null || m_hoveredSlotIndex >= slots.Count)
            {
                ClearHoveredTooltip();
                return;
            }

            InventorySlotViewData hoveredSlot = slots[m_hoveredSlotIndex];

            // 빈 슬롯엔 Tooltip 미표시
            if (hoveredSlot == null || hoveredSlot.IsEmpty)
            {
                ClearHoveredTooltip();
                return;
            }

            if (m_hoveredSlotIndex >= m_slotViews.Count)
            {
                ClearHoveredTooltip();
                return;
            }

            RectTransform slotRect =
                m_slotViews[m_hoveredSlotIndex].transform as RectTransform;

            if (slotRect == null)
            {
                ClearHoveredTooltip();
                return;
            }

            m_itemTooltip.Show(hoveredSlot, slotRect);
        }

        private void ClearHoveredTooltip()
        {
            m_hoveredSlotIndex = -1;
            m_itemTooltip?.Hide();
        }

        private Sprite GetIcon(InventorySlotViewData slotData)
        {
            if (slotData == null
                || slotData.IsEmpty
                || string.IsNullOrEmpty(slotData.IconKey))
            {
                return null;
            }

            if (AssetProvider.TryGet<Sprite>(slotData.IconKey, out var icon))
            {
                return icon;
            }
            
            Debug.LogWarning($"[Inventory] Icon not found for key: {slotData.IconKey}. Returning null.");
            return null;
        }


        private void BeginPickup(InventorySlotViewData slotData)
        {
            if (m_itemPickupController == null)
            {
                return;
            }

            Sprite icon = GetIcon(slotData);

            m_itemPickupController.BeginPickup(
                slotData.SlotType,
                slotData.SlotIndex,
                slotData.Handle,
                icon);
        }

        private bool IsPickedItemStillValid()
        {
            if (m_itemPickupController == null)
            {
                return false;
            }

            List<InventorySlotViewData> slots = m_vm.Slots.Value;
            int sourceSlotIndex = m_itemPickupController.SourceSlotIndex;

            if (slots == null
                || sourceSlotIndex < 0
                || sourceSlotIndex >= slots.Count)
            {
                return false;
            }

            InventorySlotViewData sourceSlot = slots[sourceSlotIndex];

            if (sourceSlot == null || sourceSlot.IsEmpty)
            {
                return false;
            }

            return sourceSlot.SlotType == m_itemPickupController.SourceSlotType
                && sourceSlot.Handle.Equals(m_itemPickupController.PickedHandle);
        }

        private void RefreshPickupSourceFrames()
        {
            for (int i = 0; i < m_slotViews.Count; i++)
            {
                bool isPickupSource =
                    m_itemPickupController != null
                    && m_itemPickupController.HasItem
                    && m_itemPickupController.SourceSlotType == m_vm.CurrentTab.Value
                    && m_itemPickupController.SourceSlotIndex == i;

                m_slotViews[i].SetPickupSource(isPickupSource);
            }
        }

        private void OnFishTabClicked()
        {
            ClearPickup();
            ClearHoveredTooltip();

            m_vm.SelectTabCommand.Execute(ItemType.Fish);
        }

        private void OnEquipmentTabClicked()
        {
            // 인벤토리에서 집은 아이템은 탭 변경 시 취소
            if (m_itemPickupController != null && m_itemPickupController.HasItem && m_itemPickupController.Source == ItemPickupSource.Inventory)
            {
                m_itemPickupController.ClearPickup();
            }

            ClearHoveredTooltip();

            m_vm.SelectTabCommand.Execute(ItemType.Equipment);
        }

        private void OnMaterialTabClicked()
        {
            ClearPickup();
            ClearHoveredTooltip();

            m_vm.SelectTabCommand.Execute(ItemType.Materials);
        }

        private void ClearPickup()
        {
            if (m_itemPickupController != null
                && m_itemPickupController.HasItem)
            {
                m_itemPickupController.ClearPickup();
            }
        }

        private void OnCloseButtonClicked()
        {
            ClearPickup();
            ClearHoveredTooltip();

            Close();
        }

        private void RefreshGoldText(int gold)
        {
            if (m_goldText == null)
            {
                return;
            }

            // 천 단위 쉼표로 표시
            // 예: 1000 -> 1,000
            m_goldText.text = gold.ToString("N0");
        }
    }
}