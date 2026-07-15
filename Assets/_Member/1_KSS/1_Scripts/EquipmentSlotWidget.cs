using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // [추가됨] 드래그 앤 드롭 이벤트를 감지하기 위한 필수 네임스페이스

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 장비창의 개별 슬롯을 담당하는 재사용 가능한 위젯(UI 컴포넌트)입니다.
    /// </summary>
    // [수정됨] 마우스 드롭(IDropHandler), 드래그 시작(IBeginDragHandler), 드래그 중(IDragHandler) 인터페이스 상속
    public class EquipmentSlotWidget : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private EquipmentMountingArea m_area;
        [SerializeField] private Button m_slotButton;
        [SerializeField] private TextMeshProUGUI m_itemNameText;
        [SerializeField] private Image m_iconImage; // [추가] 장착된 아이템의 아이콘을 표시할 이미지

        public EquipmentMountingArea Area => m_area;

        // View(상위 스크립트)로 이벤트를 전달할 콜백 함수들
        private Action<EquipmentMountingArea> m_onClickAction;
        private Action<EquipmentMountingArea> m_onDropAction;
        private Action<EquipmentMountingArea> m_onBeginDragAction;

        // [수정됨] 클릭뿐만 아니라, 드롭과 드래그 이벤트도 외부에서 연결할 수 있도록 매개변수 추가
        // (기존 코드와의 호환성을 위해 = null 처리하여 에러를 방지했습니다)
        public void Bind(
            Action<EquipmentMountingArea> onClickAction,
            Action<EquipmentMountingArea> onDropAction = null,
            Action<EquipmentMountingArea> onBeginDragAction = null)
        {
            m_onClickAction = onClickAction;
            m_onDropAction = onDropAction;
            m_onBeginDragAction = onBeginDragAction;

            m_slotButton.onClick.AddListener(() => m_onClickAction?.Invoke(m_area));
        }

        public void Unbind()
        {
            m_slotButton.onClick.RemoveAllListeners();
            m_onClickAction = null;
            m_onDropAction = null;
            m_onBeginDragAction = null;
        }

        public void RefreshSlotUI(string itemName, Sprite icon)
        {
            if (m_itemNameText != null)
            {
                m_itemNameText.text = itemName;
            }

            // [추가] 아이콘 업데이트
            if (m_iconImage != null)
            {
                m_iconImage.sprite = icon;
                // 아이콘이 있으면 활성화(또는 투명도 100%), 없으면 비활성화(투명도 0%) 등 적절히 처리
                m_iconImage.enabled = icon != null;
            }
        }

        // ========================================================
        // 유니티 드래그 앤 드롭 이벤트 감지 센서 (EventSystems)
        // ========================================================

        /// <summary>
        /// 1. 인벤토리에서 끌고 온 아이템을 내 위에 떨어뜨렸을 때 (Drop)
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            m_onDropAction?.Invoke(m_area);
        }

        /// <summary>
        /// 2. 내 슬롯에 장착된 장비를 클릭해서 인벤토리로 끌기 시작할 때 (Begin Drag)
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            m_onBeginDragAction?.Invoke(m_area);
        }

        /// <summary>
        /// 3. 드래그 중일 때 (Unity 규칙상 IBeginDragHandler를 쓰려면 IDragHandler도 함께 구현해야 합니다)
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            // 실제 마우스를 따라다니는 아이콘 이동 처리는 
            // 팀원분이 만드신 ItemPickupController에서 담당하므로 여기는 비워둡니다.
        }
    }
}