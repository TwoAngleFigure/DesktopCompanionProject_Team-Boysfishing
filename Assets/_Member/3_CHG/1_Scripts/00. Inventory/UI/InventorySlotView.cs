using System;
using DesktopCompanion.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
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

        [Header("State")]
        [SerializeField] private GameObject m_emptyRoot;
        [SerializeField] private GameObject m_selectedFrame;

        [Header("Pickup State")]
        [FormerlySerializedAs("m_moveSourceFrame")]
        [SerializeField] private GameObject m_pickupSourceFrame;

        private const float DoubleClickInterval = 0.3f;

        private int m_slotIndex;
        private float m_lastClickTime = -1f;
        private bool m_canDoubleClick;

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

            if (m_selectedFrame != null)
            {
                m_selectedFrame.SetActive(data.IsSelected);
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

            if (m_selectedFrame != null)
            {
                m_selectedFrame.SetActive(data != null && data.IsSelected);
            }
        }

        private string GetSubInfoText(InventorySlotViewData data)
        {
            switch (data.ItemType)
            {
                case ItemType.Fish:
                    return GetQualityText(data.Quality);

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

        private string GetQualityText(ItemQuality quality)
        {
            switch (quality)
            {
                case ItemQuality.OneStar:
                    return "¡Ú";

                case ItemQuality.TwoStar:
                    return "¡Ú¡Ú";

                case ItemQuality.ThreeStar:
                    return "¡Ú¡Ú¡Ú";

                case ItemQuality.FourStar:
                    return "¡Ú¡Ú¡Ú¡Ú";

                case ItemQuality.FiveStar:
                    return "¡Ú¡Ú¡Ú¡Ú¡Ú";

                default:
                    return string.Empty;
            }
        }

        public void SetPickupSource(bool isPickupSource)
        {
            if (m_pickupSourceFrame != null)
            {
                m_pickupSourceFrame.SetActive(isPickupSource);
            }
        }

        private void HandleClick()
        {
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