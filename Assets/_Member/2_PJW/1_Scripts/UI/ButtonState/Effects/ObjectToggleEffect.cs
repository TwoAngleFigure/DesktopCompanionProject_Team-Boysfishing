using System;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 상태별로 오브젝트를 켜고 끈다. 탭 책갈피의 선택 오버레이가 이것이다.
    ///
    /// ★오버레이가 버튼의 자식이면 클릭을 막지 못한다(이벤트가 부모로 버블링).
    /// 클릭까지 막아야 한다면 오버레이를 버튼 밖에 두거나 별도 처리가 필요하다.
    /// </summary>
    [Serializable]
    public class ObjectToggleEffect : ButtonStateEffect
    {
        [Tooltip("켜고 끌 대상")]
        [SerializeField] private GameObject m_target;

        [SerializeField] private bool m_onNormal;
        [SerializeField] private bool m_onHover;
        [SerializeField] private bool m_onActive = true;

        [Tooltip("전이가 이 지점을 넘으면 다음 상태를 따른다. 0이면 즉시")]
        [Range(0f, 1f)]
        [SerializeField] private float m_switchAt;

        public override void Apply(ButtonVisualState from, ButtonVisualState to, float t)
        {
            if (m_target == null)
            {
                return;
            }

            bool shown = ShownAt(t > m_switchAt ? to : from);
            if (m_target.activeSelf != shown)
            {
                m_target.SetActive(shown);   // 값이 같을 때 건드리지 않는다 — SetActive는 매번 계층을 훑는다
            }
        }

        private bool ShownAt(ButtonVisualState state) => state switch
        {
            ButtonVisualState.Hover => m_onHover,
            ButtonVisualState.Active => m_onActive,
            _ => m_onNormal,
        };
    }
}
