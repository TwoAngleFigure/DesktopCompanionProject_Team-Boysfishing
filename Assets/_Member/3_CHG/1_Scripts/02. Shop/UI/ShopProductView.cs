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
        [SerializeField] private Image m_itemIcon;
        [SerializeField] private TMP_Text m_itemName;
        [SerializeField] private TMP_Text m_typeText;
        [SerializeField] private TMP_Text m_descriptionText;
        [SerializeField] private TMP_Text m_priceText;

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

            m_button.onClick.RemoveListener(OnClickButton);
            m_button.onClick.AddListener(OnClickButton);
        }

        public void Set(ShopProductViewData data, Sprite icon)
        {
            if (data == null)
                return;

            m_productData = data;

            if (m_itemIcon != null)
            {
                m_itemIcon.sprite = icon;
                m_itemIcon.enabled = icon != null;
            }

            if (m_itemName != null)
                m_itemName.text = data.Name;

            if (m_typeText != null)
                m_typeText.text = data.TypeText;

            if (m_descriptionText != null)
                m_descriptionText.text = data.Description;

            if (m_priceText != null)
                m_priceText.text = data.Price.ToString();
        }

        public void OnClickButton()
        {
            if (m_productData == null)
                return;
            m_onClick?.Invoke(m_productData);
        }
    }
}

