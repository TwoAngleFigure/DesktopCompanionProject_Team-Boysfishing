using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DesktopCompanion.Entities;
using TMPro;

namespace DesktopCompanion.Views
{
    public class ReinforceSlotWidget : MonoBehaviour, IDropHandler
    {
        [SerializeField] private Image m_itemIcon;
        [SerializeField] private TextMeshProUGUI m_levelText; // "+3" 등 표시
        [SerializeField] private TextMeshProUGUI m_itemNameText; // 아이템 이름 표시
        [SerializeField] private Button m_slotButton;

        public event Action<EntityHandle> OnSlotClicked;
        public event Action OnSlotDropped;
        
        private EntityHandle m_currentHandle;

        private void Awake()
        {
            if (m_slotButton != null)
            {
                m_slotButton.onClick.AddListener(() => OnSlotClicked?.Invoke(m_currentHandle));
            }
        }

        public void SetItem(EntityHandle handle, Sprite icon, int currentLevel, string itemName = "")
        {
            m_currentHandle = handle;
            
            if (handle.Value == Guid.Empty)
            {
                if (m_itemIcon != null) m_itemIcon.enabled = false;
                if (m_levelText != null) m_levelText.text = "";
                if (m_itemNameText != null) m_itemNameText.text = "";
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
                if (m_itemNameText != null)
                {
                    m_itemNameText.text = itemName;
                }
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            OnSlotDropped?.Invoke();
        }
    }
}
