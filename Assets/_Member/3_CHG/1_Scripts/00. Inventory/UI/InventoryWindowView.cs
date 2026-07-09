using System.Collections.Generic;
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

        [Header("Temporary Move Mode")]
        [SerializeField] private Button m_moveButton;
        [SerializeField] private TMP_Text m_moveGuideText;

        [Header("Slot Grid")]
        [SerializeField] private Transform m_slotRoot;
        [SerializeField] private InventorySlotView m_slotPrefab;

        [Header("Detail Panel")]
        [SerializeField] private GameObject m_detailPanel;
        [SerializeField] private Image m_detailIconImage;
        [SerializeField] private TMP_Text m_detailNameText;
        [SerializeField] private TMP_Text m_detailInfoText;

        private readonly InventoryViewModel m_vm = new();
        private readonly List<InventorySlotView> m_slotViews = new();

        public void Start()
        {
            Debug.Log("[Test]");
        }

        public override void Bind()
        {
            Debug.Log("[InventoryWindowView] Bind called");

            //m_vm.Inject(SystemManager);
            m_vm.InjectEntityManager(EntityManager);
            m_vm.Bind();

            m_vm.Slots.Bind(RefreshSlotViews);
            m_vm.SelectedSlot.Bind(RefreshDetailPanel);
            m_vm.IsMoveMode.Bind(RefreshMoveModeUI);

            if (m_fishTabButton != null)
            {
                m_fishTabButton.onClick.AddListener(() => m_vm.SelectTabCommand.Execute(ItemType.Fish));
            }

            if (m_equipmentTabButton != null)
            {
                m_equipmentTabButton.onClick.AddListener(() => m_vm.SelectTabCommand.Execute(ItemType.Equipment));
            }

            if (m_materialTabButton != null)
            {
                m_materialTabButton.onClick.AddListener(() => m_vm.SelectTabCommand.Execute(ItemType.Materials));
            }

            if (m_moveButton != null)
            {
                m_moveButton.onClick.AddListener(() => m_vm.MoveButtonCommand.Execute());
            }

            if (m_closeButton != null)
            {
                m_closeButton.onClick.AddListener(Close);
            }
        }

        public override void Unbind()
        {
            m_vm.Slots.Unbind(RefreshSlotViews);
            m_vm.SelectedSlot.Unbind(RefreshDetailPanel);
            m_vm.IsMoveMode.Unbind(RefreshMoveModeUI);

            if (m_fishTabButton != null)
            {
                m_fishTabButton.onClick.RemoveAllListeners();
            }

            if (m_equipmentTabButton != null)
            {
                m_equipmentTabButton.onClick.RemoveAllListeners();
            }

            if (m_materialTabButton != null)
            {
                m_materialTabButton.onClick.RemoveAllListeners();
            }

            if (m_moveButton != null)
            {
                m_moveButton.onClick.RemoveAllListeners();
            }

            if (m_closeButton != null)
            {
                m_closeButton.onClick.RemoveAllListeners();
            }

            m_vm.Unbind();
        }

        private void RefreshSlotViews(List<InventorySlotViewData> slots)
        {
            if (slots == null)
            {
                return;
            }

            EnsureSlotViews(slots.Count);

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
        }

        private void EnsureSlotViews(int requiredCount)
        {
            if (m_slotPrefab == null || m_slotRoot == null)
            {
                return;
            }

            while (m_slotViews.Count < requiredCount)
            {
                InventorySlotView slotView = Instantiate(m_slotPrefab, m_slotRoot);
                int slotIndex = m_slotViews.Count;

                slotView.Initialize(slotIndex, OnSlotClicked);
                m_slotViews.Add(slotView);
            }

            for (int i = 0; i < requiredCount; i++)
            {
                m_slotViews[i].gameObject.SetActive(true);
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            m_vm.SelectSlotCommand.Execute(slotIndex);
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

            if (m_moveButton != null)
            {
                m_moveButton.interactable = true;
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

            if (m_moveButton != null)
            {
                m_moveButton.interactable = false;
            }
        }

        private void RefreshMoveModeUI(bool isMoveMode)
        {
            if (m_moveGuideText != null)
            {
                m_moveGuideText.text = isMoveMode ? "이동할 슬롯을 선택하세요." : string.Empty;
            }

            if (m_moveButton != null)
            {
                TMP_Text moveButtonText = m_moveButton.GetComponentInChildren<TMP_Text>();

                if (moveButtonText != null)
                {
                    // TEMP: Drag-Drop 도입 시 수정
                    moveButtonText.text = isMoveMode ? "취소" : "이동";
                }
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
    }
}