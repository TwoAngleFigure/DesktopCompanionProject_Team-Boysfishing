using System;
using DesktopCompanion.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class InventorySlotView : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] private Button m_button;

        [Header("Visual")]
        [SerializeField] private Image m_iconImage;
        [SerializeField] private TMP_Text m_quantityText;
        [SerializeField] private TMP_Text m_subInfoText;

        [Header("State")]
        [SerializeField] private GameObject m_emptyRoot;
        [SerializeField] private GameObject m_selectedFrame;

        [Header("Temporary Move Mode")]
        [SerializeField] private GameObject m_moveSourceFrame;
        [SerializeField] private GameObject m_moveTargetFrame;

        private int m_slotIndex;
        private Action<int> m_onClicked;

        private void Awake()
        {
            if (m_button == null)
            {
                m_button = GetComponent<Button>();
            }
        }

        public void Initialize(int slotIndex, Action<int> onClicked)
        {
            m_slotIndex = slotIndex;
            m_onClicked = onClicked;

            if (m_button == null)
            {
                return;
            }

            m_button.onClick.RemoveAllListeners();
            m_button.onClick.AddListener(() => m_onClicked?.Invoke(m_slotIndex));
        }

        public void Set(InventorySlotViewData data, Sprite icon)
        {
            if (data == null || data.IsEmpty)
            {
                SetEmpty(data);
                return;
            }

            if (m_emptyRoot != null)
            {
                m_emptyRoot.SetActive(false);
            }

            if (m_iconImage != null)
            {
                m_iconImage.enabled = icon != null;
                m_iconImage.sprite = icon;
            }

            if (m_quantityText != null)
            {
                m_quantityText.text = data.Quantity > 1 ? data.Quantity.ToString() : string.Empty;
            }

            if (m_subInfoText != null)
            {
                m_subInfoText.text = GetSubInfoText(data);
            }

            SetStateFrames(data);
        }

        private void SetEmpty(InventorySlotViewData data)
        {
            if (m_emptyRoot != null)
            {
                m_emptyRoot.SetActive(true);
            }

            if (m_iconImage != null)
            {
                m_iconImage.enabled = false;
                m_iconImage.sprite = null;
            }

            if (m_quantityText != null)
            {
                m_quantityText.text = string.Empty;
            }

            if (m_subInfoText != null)
            {
                m_subInfoText.text = string.Empty;
            }

            SetStateFrames(data);
        }

        private void SetStateFrames(InventorySlotViewData data)
        {
            bool isSelected = data != null && data.IsSelected;

            // TEMP: Drag-Drop 도입 시 수정
            bool isMoveSource = data != null && data.IsMoveSource;
            bool isMoveTarget = data != null && data.IsMoveMode && !data.IsMoveSource;

            if (m_selectedFrame != null)
            {
                m_selectedFrame.SetActive(isSelected);
            }

            if (m_moveSourceFrame != null)
            {
                m_moveSourceFrame.SetActive(isMoveSource);
            }

            if (m_moveTargetFrame != null)
            {
                m_moveTargetFrame.SetActive(isMoveTarget);
            }
        }

        private string GetSubInfoText(InventorySlotViewData data)
        {
            switch (data.ItemType)
            {
                case ItemType.Fish:
                    return GetQualityText(data.Quality);

                case ItemType.Equipment:
                    return data.UpgradeLevel > 0 ? $"+{data.UpgradeLevel}" : string.Empty;

                case ItemType.Materials:
                case ItemType.Consumables:
                    return string.Empty;

                default:
                    return string.Empty;
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

        public void SetPickupSource(bool isPickupSource)
        {
            if (m_moveSourceFrame != null)
            {
                m_moveSourceFrame.SetActive(isPickupSource);
            }
        }
    }
}