using System;
using UnityEngine;
using UnityEngine.UI;
using DesktopCompanion.Entities;
using TMPro;

namespace DesktopCompanion.Views
{
    public class EnhancementSlotWidget : MonoBehaviour
    {
        [SerializeField] private Image m_itemIcon;
        [SerializeField] private TextMeshProUGUI m_levelText; // "+3" 등 표시
        [SerializeField] private Button m_slotButton;

        public event Action<EntityHandle> OnSlotClicked;
        private EntityHandle m_currentHandle;

        private void Awake()
        {
            if (m_slotButton != null)
            {
                m_slotButton.onClick.AddListener(() => OnSlotClicked?.Invoke(m_currentHandle));
            }
        }

        public void SetItem(EntityHandle handle, Sprite icon, int currentLevel)
        {
            m_currentHandle = handle;
            
            if (handle.Value == Guid.Empty)
            {
                if (m_itemIcon != null) m_itemIcon.enabled = false;
                if (m_levelText != null) m_levelText.text = "";
            }
            else
            {
                if (m_itemIcon != null) 
                {
                    m_itemIcon.enabled = true;
                    m_itemIcon.sprite = icon;
                }
                if (m_levelText != null) 
                {
                    m_levelText.text = currentLevel > 0 ? $"+{currentLevel}" : "";
                }
            }
        }
    }
}
