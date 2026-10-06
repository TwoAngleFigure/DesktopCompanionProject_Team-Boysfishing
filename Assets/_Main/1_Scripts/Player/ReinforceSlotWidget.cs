using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 강화 대상 장비 칸의 입력 담당. 아이콘·티어 테두리·hover 팝업 등 표시는 같은 오브젝트의
    /// <see cref="ItemSlotView"/>가 맡고, 이 위젯은 드롭과 클릭만 창(<see cref="ReinforceWindowView"/>)에 전달한다.
    /// </summary>
    public class ReinforceSlotWidget : MonoBehaviour, IDropHandler
    {
        [Tooltip("비우면 같은 오브젝트에서 자동으로 찾는다")]
        [SerializeField] private Button m_slotButton;

        public event Action OnSlotClicked;
        public event Action OnSlotDropped;

        private void Awake()
        {
            if (m_slotButton == null)
            {
                m_slotButton = GetComponent<Button>();
            }
            if (m_slotButton != null)
            {
                m_slotButton.onClick.AddListener(RaiseClicked);
            }
        }

        private void OnDestroy()
        {
            if (m_slotButton != null)
            {
                m_slotButton.onClick.RemoveListener(RaiseClicked);
            }
        }

        private void RaiseClicked() => OnSlotClicked?.Invoke();

        public void OnDrop(PointerEventData eventData) => OnSlotDropped?.Invoke();
    }
}
