using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 장비창의 개별 슬롯을 담당하는 재사용 가능한 위젯(UI 컴포넌트)입니다.
    /// </summary>
    public class EquipmentSlotWidget : MonoBehaviour
    {
        [SerializeField] private EquipmentMountingArea m_area;
        [SerializeField] private Button m_slotButton;


        [SerializeField] private TextMeshProUGUI m_itemNameText;

        public EquipmentMountingArea Area => m_area;

        public void Bind(Action<EquipmentMountingArea> onClickAction)
        {
            m_slotButton.onClick.AddListener(() => onClickAction?.Invoke(m_area));
        }

        public void Unbind()
        {
            m_slotButton.onClick.RemoveAllListeners();
        }

        public void RefreshSlotUI(string itemName)
        {
            if (m_itemNameText != null)
            {
                m_itemNameText.text = itemName;
            }
        }
    }
}