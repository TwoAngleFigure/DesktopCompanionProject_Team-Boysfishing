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

        private const float DoubleClickInterval = 0.3f;

        private int m_slotIndex;
        private float m_lastClickTime = -1f;

        private Action<int> m_onClick;
        private Action<int> m_onDoubleClick;

        private void Awake()
        {
            if (m_button == null)
            {
                m_button = GetComponent<Button>();
            }
        }

        public void Initialize(int slotIndex, Action<int> onClick, Action<int> onDoubleClick)
        {
            m_slotIndex = slotIndex;
            m_onClick = onClick;
            m_onDoubleClick = onDoubleClick;

            m_button.onClick.RemoveListener(HandleClick);
            m_button.onClick.AddListener(HandleClick);
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

        private void HandleClick()
        {
            float currentTime = Time.unscaledTime;
            bool isDoubleClick = currentTime - m_lastClickTime <= DoubleClickInterval;

            if (isDoubleClick)
            {
                m_lastClickTime = -1f;
                m_onDoubleClick?.Invoke(m_slotIndex);
                return;
            }

            m_lastClickTime = currentTime;
            m_onClick?.Invoke(m_slotIndex);
        }
    }
}