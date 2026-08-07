using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 드롭다운 옵션 1칸의 텍스트 색을 hover 여부에 따라 바꾼다.
    ///
    /// 옵션은 런타임에 복제되므로 하나하나 붙일 수 없다. 대신 원본 Template의 아이템에 붙여 두면
    /// TMP_Dropdown이 그 아이템을 통째로 Instantiate 하면서 이 컴포넌트와 색 값이 함께 복제된다.
    /// 직접 붙이지 않아도 <see cref="DropdownAnimator"/>가 Awake에서 심는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DropdownOptionTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("비우면 자식에서 TMP_Text를 찾는다")]
        [SerializeField] private TMP_Text m_label;

        [SerializeField] private Color m_normalColor = Color.white;
        [SerializeField] private Color m_hoverColor = Color.yellow;

        /// <summary>DropdownAnimator가 Template 아이템에 심을 때 색을 주입한다.</summary>
        public void SetColors(Color normal, Color hover)
        {
            m_normalColor = normal;
            m_hoverColor = hover;
        }

        private void OnEnable()
        {
            if (m_label == null)
            {
                m_label = GetComponentInChildren<TMP_Text>(true);
            }
            Apply(false);   // 복제 직후 기본색을 확정한다. TMP는 옵션 색을 item.image에만 적용하므로 충돌이 없다
        }

        public void OnPointerEnter(PointerEventData eventData) => Apply(true);

        public void OnPointerExit(PointerEventData eventData) => Apply(false);

        private void Apply(bool hovered)
        {
            if (m_label == null)
            {
                return;
            }
            m_label.color = hovered ? m_hoverColor : m_normalColor;
        }
    }
}
