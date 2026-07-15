using System;
using DesktopCompanion.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class InventorySlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Button")]
        [SerializeField] private Button m_button;

        [Header("Visual")]
        [SerializeField] private Image m_iconImage;
        [SerializeField] private TMP_Text m_quantityText;
        [SerializeField] private TMP_Text m_subInfoText;
        [SerializeField] private GameObject m_fishStarImage;

        [Header("State")]
        [SerializeField] private GameObject m_emptyRoot;
        [SerializeField] private GameObject m_sellSelectedIcon;
        [SerializeField] private TMP_Text m_sellSelectedAmountText;

        private const float DoubleClickInterval = 0.3f;

        private int m_slotIndex;
        private float m_lastClickTime = -1f;
        private bool m_canDoubleClick;
        private bool m_isSellMode;

        private Action<int> m_onClick;
        private Action<int> m_onDoubleClick;
        private Action<int> m_onPointerEnter;
        private Action<int> m_onPointerExit;

        private void Awake()
        {
            if (m_button == null)
            {
                m_button = GetComponent<Button>();
            }
        }

        public void Initialize(int slotIndex, Action<int> onClick, Action<int> onDoubleClick, Action<int> onPointerEnter, Action<int> onPointerExit)
        {
            m_slotIndex = slotIndex;

            m_onClick = onClick;
            m_onDoubleClick = onDoubleClick;
            m_onPointerEnter = onPointerEnter;
            m_onPointerExit = onPointerExit;

            if (m_button == null)
            {
                Debug.LogError($"[InventorySlotView] Button is not assigned. slotIndex: {slotIndex}");
                return;
            }

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

            m_canDoubleClick = data.ItemType == ItemType.Equipment;

            if (m_emptyRoot != null)
            {
                m_emptyRoot.SetActive(false);
            }

            if (m_iconImage != null)
            {
                m_iconImage.sprite = icon;
                m_iconImage.enabled = icon != null;
            }

            if (m_quantityText != null)
            {
                m_quantityText.text = data.Quantity > 1
                    ? data.Quantity.ToString()
                    : string.Empty;
            }

            if (m_subInfoText != null)
            {
                m_subInfoText.text = GetSubInfoText(data);
            }

            if (m_fishStarImage != null)
            {
                m_fishStarImage.SetActive(data.ItemType == ItemType.Fish);
            }
        }

        private void SetEmpty(InventorySlotViewData data)
        {
            m_canDoubleClick = false;
            m_lastClickTime = -1f;

            if (m_emptyRoot != null)
            {
                m_emptyRoot.SetActive(true);
            }

            if (m_iconImage != null)
            {
                m_iconImage.sprite = null;
                m_iconImage.enabled = false;
            }

            if (m_quantityText != null)
            {
                m_quantityText.text = string.Empty;
            }

            if (m_subInfoText != null)
            {
                m_subInfoText.text = string.Empty;
            }

            if (m_fishStarImage != null)
            {
                m_fishStarImage.SetActive(false);
            }

            SetSellSelection(false, 0, false);
        }

        private string GetSubInfoText(InventorySlotViewData data)
        {
            switch (data.ItemType)
            {
                case ItemType.Fish:
                    return GetQualityNumberText(data.Quality);

                case ItemType.Equipment:
                    return data.UpgradeLevel > 0
                        ? $"+{data.UpgradeLevel}"
                        : string.Empty;

                case ItemType.Materials:
                case ItemType.Consumables:
                    return string.Empty;

                default:
                    return string.Empty;
            }
        }

        private string GetQualityNumberText(ItemQuality quality)
        {
            int qualityNumber = (int)quality;

            if (qualityNumber < 1 || qualityNumber > 5)
            {
                return string.Empty;
            }

            return qualityNumber.ToString();
        }

        public void SetPickupSource(bool isPickupSource)
        {
            if (m_iconImage != null)
            {
                m_iconImage.enabled = !isPickupSource && m_iconImage.sprite != null;
            }
        }

        public void SetSellMode(bool isSellMode)
        {
            m_isSellMode = isSellMode;

            if (isSellMode)
            {
                m_lastClickTime = -1f;
            }
        }

        public void SetSellSelection(bool isSelected, int selectedAmount, bool showAmount)
        {
            if (m_sellSelectedIcon != null)
            {
                m_sellSelectedIcon.SetActive(isSelected);
            }

            if (m_sellSelectedAmountText != null)
            {
                bool showText = isSelected && showAmount;

                m_sellSelectedAmountText.gameObject.SetActive(showText);
                m_sellSelectedAmountText.text = showText
                    ? selectedAmount.ToString()
                    : string.Empty;
            }
        }

        private void HandleClick()
        {
            if (m_isSellMode)
            {
                m_lastClickTime = -1f;
                m_onClick?.Invoke(m_slotIndex);
                return;
            }

            float currentTime = Time.unscaledTime;

            bool isDoubleClick =
                m_canDoubleClick
                && m_lastClickTime >= 0f
                && currentTime - m_lastClickTime <= DoubleClickInterval;

            if (isDoubleClick)
            {
                m_lastClickTime = -1f;

                m_onDoubleClick?.Invoke(m_slotIndex);
                return;
            }

            m_lastClickTime = m_canDoubleClick
                ? currentTime
                : -1f;

            m_onClick?.Invoke(m_slotIndex);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_onPointerEnter?.Invoke(m_slotIndex);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_onPointerExit?.Invoke(m_slotIndex);
        }

        private void OnDestroy()
        {
            if (m_button != null)
            {
                m_button.onClick.RemoveListener(HandleClick);
            }

            m_onClick = null;
            m_onDoubleClick = null;
            m_onPointerEnter = null;
            m_onPointerExit = null;
        }
    }
}