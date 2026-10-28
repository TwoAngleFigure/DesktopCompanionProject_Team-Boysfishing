using System;
using DesktopCompanion.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class InventorySlotView : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] private Button m_button;

        [Header("Visual")]
        [SerializeField] private Image m_slotBackground;
        [SerializeField] private Image m_iconImage;
        [SerializeField] private TMP_Text m_quantityText;
        [SerializeField] private TMP_Text m_subInfoText;
        [SerializeField] private GameObject m_fishStarImage;
        [SerializeField] private GameObject m_enhanceIcon;
        [SerializeField] private Image m_tierBorder;

        [Header("State")]
        [SerializeField] private GameObject m_emptyRoot;
        [SerializeField] private GameObject m_sellSelectedIcon;
        [SerializeField] private TMP_Text m_sellSelectedAmountText;

        private const float DoubleClickInterval = 0.3f;

        private int m_slotIndex;
        private float m_lastClickTime = -1f;
        private bool m_canDoubleClick;
        private bool m_isSellMode;
        private ItemSlotView m_slotView;

        private Action<int> m_onClick;
        private Action<int> m_onDoubleClick;


        private void Awake()
        {
            if (m_button == null)
            {
                m_button = GetComponent<Button>();
            }
            if(m_slotView == null)
            {
                m_slotView = GetComponent<ItemSlotView>();
            }
        }

        public void Initialize(int slotIndex, Action<int> onClick, Action<int> onDoubleClick)
        {
            m_slotIndex = slotIndex;

            m_onClick = onClick;
            m_onDoubleClick = onDoubleClick;

            if (m_button == null)
            {
                Debug.LogError($"[InventorySlotView] Button is not assigned. slotIndex: {slotIndex}");
                return;
            }

            m_button.onClick.RemoveListener(HandleClick);
            m_button.onClick.AddListener(HandleClick);
        }

        public void Set(InventorySlotViewData data, Sprite icon, Sprite slotBackground, ItemSlotVD vd, IItemTooltipSource tooltipSource)
        {
            if (data == null)
                return;
            if (m_slotBackground != null)
            {
                m_slotBackground.sprite = slotBackground;
                m_slotBackground.enabled = slotBackground != null;
            }

            if(data.IsEmpty)
            {
                SetEmpty(data);
                return;
            }

            m_canDoubleClick = data.ItemType == ItemType.Equipment || data.ItemType == ItemType.Consumables;
            if(m_tierBorder != null)
            {
                m_tierBorder.gameObject.SetActive(true);
            }

            if (m_emptyRoot != null)
            {
                m_emptyRoot.SetActive(false);
            }

            if (m_iconImage != null)
            {
                m_iconImage.sprite = icon;
                m_iconImage.enabled = icon != null;
            }


            if (m_subInfoText != null)
            {
                m_subInfoText.text = GetSubInfoText(data);
            }

            if (m_fishStarImage != null)
            {
                m_fishStarImage.SetActive(data.ItemType == ItemType.Fish);
            }

            if(m_enhanceIcon != null)
            {
                m_enhanceIcon.SetActive(data.ItemType == ItemType.Equipment && data.UpgradeLevel > 0);
            }

            if(vd != null && tooltipSource != null)
            {
                m_slotView.Set(vd, icon, tooltipSource);
            }

            // ItemSlotView.Set이 수량 칸을 자기 기준(Quantity > 1)으로 덮어쓰므로 그 뒤에 적용한다.
            // 강화 수치(+3) 같은 이 창 고유의 표시가 지워지지 않게 하기 위함이다.
            ApplyQuantityText(data);
        }

        // 아이템 종류별 수량·강화 수치. 표시할 값이 없으면 빈 문자열이 되어 텍스트와 배경이 함께 꺼진다.
        private void ApplyQuantityText(InventorySlotViewData data)
        {
            string text;

            switch (data.ItemType)
            {
                case ItemType.Equipment:
                    text = data.UpgradeLevel > 0 ? $"+{data.UpgradeLevel}" : string.Empty;
                    break;

                case ItemType.Consumables:
                case ItemType.Materials:
                    text = data.Quantity > 1 ? data.Quantity.ToString() : string.Empty;
                    break;

                case ItemType.Fish:
                default:
                    text = string.Empty;
                    break;
            }

            SetQuantityText(text);
        }

        // 수량 칸의 주인은 ItemSlotView다. 그쪽을 거쳐야 배경 이미지가 텍스트와 같이 켜고 꺼진다.
        // m_quantityText 직접 쓰기는 ItemSlotView가 없는 구성에서의 폴백이다(배경은 동기화되지 않는다).
        private void SetQuantityText(string text)
        {
            if (m_slotView != null)
            {
                m_slotView.SetQuantityText(text);
                return;
            }

            if (m_quantityText != null)
            {
                m_quantityText.text = text;
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

            SetQuantityText(string.Empty);

            if (m_subInfoText != null)
            {
                m_subInfoText.text = string.Empty;
            }

            if (m_fishStarImage != null)
            {
                m_fishStarImage.SetActive(false);
            }

            if(m_enhanceIcon != null)
            {
                m_enhanceIcon.SetActive(false);
            }

            if(m_tierBorder != null)
            {
                m_tierBorder.gameObject.SetActive(false);
            }

            if(m_slotView != null)
            {
                m_slotView.Set(null, null, null);
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

        private void OnDestroy()
        {
            if (m_button != null)
            {
                m_button.onClick.RemoveListener(HandleClick);
            }

            m_onClick = null;
            m_onDoubleClick = null;
        }
    }
}