using System;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 상태별로 Animator 상태를 맞춘다. 스프라이트 교체로는 안 되는 프레임 애니메이션용이다.
    ///
    /// 상태 이름은 Animator Controller의 State 이름과 <b>정확히</b> 같아야 한다 —
    /// Animator.Play는 없는 상태를 넘겨도 예외도 경고도 없이 무시하므로,
    /// 이름이 어긋나면 복구가 통째로 사라지고 컨트롤러가 기본 상태부터 자기 전이를 태운다.
    /// 클립을 끌어다 놓으면 상태 이름이 클립 이름을 따라가니 특히 어긋나기 쉽다.
    ///
    /// 클립은 Loop Time을 꺼야 한다. 켜져 있으면 normalizedTime 1이 0으로 감겨 첫 프레임이 나온다.
    /// </summary>
    [Serializable]
    public class AnimatorEffect : ButtonStateEffect
    {
        [Tooltip("대상 Animator. 버튼의 자식에 있어도 된다")]
        [SerializeField] private Animator m_animator;

        [Tooltip("켜짐 여부를 넘길 bool 파라미터 이름. 비우면 넘기지 않는다")]
        [SerializeField] private string m_boolParameter = "On";

        [Header("상태 이름 — 비우면 그 상태에서 Play하지 않는다")]
        [SerializeField] private string m_normalState = "Off";
        [SerializeField] private string m_hoverState = "";
        [SerializeField] private string m_activeState = "On";

        [Tooltip("전이가 이 지점을 넘으면 다음 상태를 따른다. 0이면 즉시")]
        [Range(0f, 1f)]
        [SerializeField] private float m_switchAt;

        // 아래는 전이 상태가 아니라 참조·해시 캐시다.
        private ButtonStateOwner m_owner;
        private int m_boolHash;
        private int m_normalHash;
        private int m_hoverHash;
        private int m_activeHash;

        public override void Initialize(ButtonStateOwner owner)
        {
            m_owner = owner;

            m_boolHash = string.IsNullOrEmpty(m_boolParameter) ? 0 : Animator.StringToHash(m_boolParameter);
            m_normalHash = string.IsNullOrEmpty(m_normalState) ? 0 : Animator.StringToHash(m_normalState);
            m_hoverHash = string.IsNullOrEmpty(m_hoverState) ? 0 : Animator.StringToHash(m_hoverState);
            m_activeHash = string.IsNullOrEmpty(m_activeState) ? 0 : Animator.StringToHash(m_activeState);
        }

        public override void Apply(ButtonVisualState from, ButtonVisualState to, float t)
        {
            if (m_animator == null || m_animator.runtimeAnimatorController == null)
            {
                return;
            }

            ButtonVisualState shown = t > m_switchAt ? to : from;

            if (m_boolHash != 0)
            {
                m_animator.SetBool(m_boolHash, shown == ButtonVisualState.Active);
            }

            int stateHash = StateHashOf(shown);
            if (stateHash != 0)
            {
                m_animator.Play(stateHash, 0, 1f);   // 끝 프레임 = 연출이 이미 끝난 모습
            }

            // 창이 SetActive로 켜질 때 부모의 OnEnable이 자식 Animator의 Awake보다 먼저 돈다.
            // 그 시점에 Update를 부르면 'm_DidAwake' 어서션이 나고, Play도 Awake의 초기화에 덮인다.
            // Start를 지난 뒤에만 즉시 평가하고, 그 전의 요청은 소유자가 Start에서 다시 보낸다.
            if (m_owner != null && m_owner.IsStarted)
            {
                m_animator.Update(0f);
            }
        }

        private int StateHashOf(ButtonVisualState state) => state switch
        {
            ButtonVisualState.Hover => m_hoverHash != 0 ? m_hoverHash : m_normalHash,
            ButtonVisualState.Active => m_activeHash,
            _ => m_normalHash,
        };
    }
}
