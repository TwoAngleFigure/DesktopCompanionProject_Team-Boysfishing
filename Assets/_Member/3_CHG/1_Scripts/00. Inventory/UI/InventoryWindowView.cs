using System.Collections.Generic;
using DesktopCompanion.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

        [Header("Detail Panel")]
        [SerializeField] private GameObject m_detailPanel;
        [SerializeField] private Image m_detailIconImage;
        [SerializeField] private TMP_Text m_detailNameText;
        [SerializeField] private TMP_Text m_detailInfoText;

        [Header("Item Pickup")]
        [SerializeField] private ItemPickupController m_itemPickupController;

        private readonly InventoryViewModel m_vm = new();
        private readonly List<InventorySlotView> m_slotViews = new();

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.Slots.Bind(RefreshSlotViews);
            m_vm.SelectedSlot.Bind(RefreshDetailPanel);

            if (m_itemPickupController != null)
            {
                m_itemPickupController.OnPickupChanged += RefreshPickupSourceFrames;
            }
            else
            {
                Debug.LogError("[InventoryWindowView] ItemPickupController is not assigned.");
            }

            m_fishTabButton.onClick.AddListener(OnFishTabClicked);
            m_equipmentTabButton.onClick.AddListener(OnEquipmentTabClicked);
            m_materialTabButton.onClick.AddListener(OnMaterialTabClicked);

            m_closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        public override void Unbind()
        {
            if (m_itemPickupController != null)
            {
                m_itemPickupController.OnPickupChanged -= RefreshPickupSourceFrames;
                m_itemPickupController.ClearPickup();
            }

            m_vm.Slots.Unbind(RefreshSlotViews);
            m_vm.SelectedSlot.Unbind(RefreshDetailPanel);

            m_fishTabButton.onClick.RemoveListener(OnFishTabClicked);
            m_equipmentTabButton.onClick.RemoveListener(OnEquipmentTabClicked);
            m_materialTabButton.onClick.RemoveListener(OnMaterialTabClicked);
            m_closeButton.onClick.RemoveListener(OnCloseButtonClicked);

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
        }

        private bool EnsureSlotViews(int requiredCount)
        {
            if (m_slotPrefab == null)
            {
                Debug.LogError("[InventoryWindowView] Slot prefab is not assigned." + this);
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

                slotView.Initialize(slotIndex, OnSlotClicked, OnSlotDoubleClicked);
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

            m_vm.SelectSlotCommand.Execute(slotIndex);

            if (m_itemPickupController == null)
            {
                return;
            }

            if (!m_itemPickupController.HasItem)
            {
                if (clickedSlot.IsEmpty)
                {
                    return;
                }

                BeginPickup(clickedSlot);
                return;
            }

            if (m_itemPickupController.SourceSlotType != clickedSlot.SlotType)
            {
                return;
            }

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

            bool swapped = m_vm.SwapSlots(
                m_itemPickupController.SourceSlotIndex,
                slotIndex);

            if (swapped)
            {
                m_itemPickupController.ClearPickup();
            }
        }

        private Sprite GetIcon(InventorySlotViewData slotData)
        {
            if (slotData == null || slotData.IsEmpty || string.IsNullOrEmpty(slotData.IconKey))
            {
                return null;
            }

            return AssetProvider.Get<Sprite>(slotData.IconKey);
        }

        private void RefreshDetailPanel(InventorySlotViewData selected)
        {
            bool hasItem = selected != null && !selected.IsEmpty;

            if (m_detailPanel != null)
            {
                m_detailPanel.SetActive(true);
            }

            if (!hasItem)
            {
                ClearDetailPanel();
                return;
            }

            Sprite icon = GetIcon(selected);

            if (m_detailIconImage != null)
            {
                m_detailIconImage.enabled = icon != null;
                m_detailIconImage.sprite = icon;
            }

            if (m_detailNameText != null)
            {
                m_detailNameText.text = selected.ItemName;
            }

            if (m_detailInfoText != null)
            {
                m_detailInfoText.text = BuildDetailText(selected);
            }
        }

        private void ClearDetailPanel()
        {
            if (m_detailIconImage != null)
            {
                m_detailIconImage.enabled = false;
                m_detailIconImage.sprite = null;
            }

            if (m_detailNameText != null)
            {
                m_detailNameText.text = "선택된 아이템 없음";
            }

            if (m_detailInfoText != null)
            {
                m_detailInfoText.text = string.Empty;
            }
        }

        private string BuildDetailText(InventorySlotViewData selected)
        {
            switch (selected.ItemType)
            {
                case ItemType.Fish:
                    return $"Type: Fish\nQuality: {GetQualityText(selected.Quality)}\nSize: {selected.Size:0.##}";

                case ItemType.Equipment:
                    return $"Type: Equipment\nUpgrade: +{selected.UpgradeLevel}";

                case ItemType.Materials:
                    return $"Type: Material\nQuantity: {selected.Quantity}";

                case ItemType.Consumables:
                    return $"Type: Consumable\nQuantity: {selected.Quantity}";

                default:
                    return $"Type: {selected.ItemType}";
            }
        }

        private string GetQualityText(ItemQuality quality)
        {
            switch (quality)
            {
                case ItemQuality.OneStar:
                    return "★";

                case ItemQuality.TwoStar:
                    return "★★";

                case ItemQuality.ThreeStar:
                    return "★★★";

                case ItemQuality.FourStar:
                    return "★★★★";

                case ItemQuality.FiveStar:
                    return "★★★★★";

                default:
                    return string.Empty;
            }
        }

        private void BeginPickup(InventorySlotViewData slotData)
        {
            Sprite icon = GetIcon(slotData);

            m_itemPickupController.BeginPickup(
                slotData.SlotType,
                slotData.SlotIndex,
                slotData.Handle,
                icon);
        }

        private bool IsPickedItemStillValid()
        {
            List<InventorySlotViewData> slots = m_vm.Slots.Value;
            int sourceSlotIndex = m_itemPickupController.SourceSlotIndex;

            if (slots == null || sourceSlotIndex < 0 || sourceSlotIndex >= slots.Count)
            {
                return false;
            }

            InventorySlotViewData sourceSlot = slots[sourceSlotIndex];

            if (sourceSlot.IsEmpty)
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
            m_vm.SelectTabCommand.Execute(ItemType.Fish);
        }

        private void OnEquipmentTabClicked()
        {
            ClearPickup();
            m_vm.SelectTabCommand.Execute(ItemType.Equipment);
        }

        private void OnMaterialTabClicked()
        {
            ClearPickup();
            m_vm.SelectTabCommand.Execute(ItemType.Materials);
        }

        private void ClearPickup()
        {
            if (m_itemPickupController != null && m_itemPickupController.HasItem)
            {
                m_itemPickupController.ClearPickup();
            }
        }

        private void OnCloseButtonClicked()
        {
            ClearPickup();

            Close();
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

            // 첫 클릭에서 시작된 아이템 픽업 상태를 제거합니다.
            m_itemPickupController?.ClearPickup();

            m_vm.EquipEquipment(slotData.Handle);
        }
    }
}