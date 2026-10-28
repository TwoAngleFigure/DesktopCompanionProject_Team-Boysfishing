using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class ShopProductView : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] private Button m_button;

        [Header("Visual")]
        [SerializeField] private Image m_backgroundImage;
        [SerializeField] private TMP_Text m_itemName;
        [SerializeField] private TMP_Text m_typeText;
        [SerializeField] private TMP_Text m_descriptionText;
        [SerializeField] private TMP_Text m_priceText;
        [SerializeField] private ItemSlotView m_slotView;

        private ShopProductViewData m_productData;


        Action<ShopProductViewData> m_onClick;

        public void Initialize(Action<ShopProductViewData> onClick)
        {
            m_onClick = onClick;

            if (m_button == null)
            {
                Debug.LogWarning("[ShopProductView] 버튼이 할당되지 않았습니다.");
                return;
            }

            if(m_slotView == null)
            {
                m_slotView = GetComponentInChildren<ItemSlotView>();
            }

            m_button.onClick.RemoveListener(OnClickButton);
            m_button.onClick.AddListener(OnClickButton);
        }

        public void Set(Sprite background, ShopProductViewData data, Sprite icon, ItemSlotVD vd, IItemTooltipSource source)
        {
            if (data == null)
                return;

            m_productData = data;

            if(background != null)
            {
                m_backgroundImage.sprite = background;
            }

            if (m_itemName != null)
                m_itemName.text = data.Name;

            if (m_typeText != null)
                m_typeText.text = data.TypeText;

            if (m_descriptionText != null)
                m_descriptionText.text = data.Description;

            if (m_priceText != null)
                m_priceText.text = data.Price.ToString();

            if (m_button != null)
                m_button.interactable = data.IsBuyable;

            if(vd != null && source != null)
            {
                m_slotView.Set(vd, icon, source);
            }
        }

        public void OnClickButton()
        {
            if (m_productData == null)
                return;
            m_onClick?.Invoke(m_productData);
        }
    }
}

