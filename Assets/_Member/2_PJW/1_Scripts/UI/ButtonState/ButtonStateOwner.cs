using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 버튼의 표시 상태를 소유하고, 붙어 있는 연출들에게 전달한다.
    ///
    /// 소유 범위는 <b>표시 상태</b>다. Hover는 스스로 판정해 갖고 최종 상태도 이쪽이 정하지만,
    /// Active의 근거가 되는 <b>논리 상태는 바깥이 소유한다</b> — 낚시는 FishingSystem, 탭은 UITabWindow다.
    /// 그쪽이 <see cref="SetActive"/>로 넣어 준다.
    ///
    /// 이름에 View를 쓰지 않는다. 이 프로젝트에서 View는 ViewModel과 짝을 이루는 MVVM의 View
    /// (<see cref="UIViewBase"/> 계열)를 가리키는데, 이 컴포넌트는 ViewModel을 갖지 않는다.
    ///
    /// 연출은 <see cref="ButtonStateEffect"/> 목록으로 조합한다. 컴포넌트를 여러 개 붙이지 않는 이유는
    /// 값이 여러 인스펙터로 흩어지지 않게 하기 위함이다 — 아티스트는 여기 한 곳만 보면 된다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class ButtonStateOwner : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("켜진 상태에서도 마우스 오버 표시를 낼지. 꺼두면 켜짐이 오버를 덮는다(탭 버튼의 기존 동작)")]
        [SerializeField] private bool m_hoverOverridesActive;

        [Header("전이 시간(초) — 그 상태로 '들어갈 때'의 시간이다")]
        [SerializeField] private float m_toNormalDuration = 0.10f;
        [SerializeField] private float m_toHoverDuration = 0.12f;
        [SerializeField] private float m_toActiveDuration = 0.12f;

        [Tooltip("이 버튼의 연출 목록. + 를 눌러 종류를 고르고 값을 채운다")]
        [SerializeReference] private List<ButtonStateEffect> m_effects = new();

        private bool m_isActive;
        private bool m_isHovered;
        private bool m_awake;
        private bool m_started;

        // 진행 중인 전이 — 버튼당 하나뿐이다. 효과의 상태가 아니라 버튼 자신의 상태다.
        private ButtonVisualState m_from = ButtonVisualState.Normal;
        private ButtonVisualState m_to = ButtonVisualState.Normal;
        private float m_t = 1f;
        private float m_duration;

        /// <summary>
        /// Start를 지났는지. 자식 Animator를 즉시 평가해도 되는 시점인지 효과가 묻는다 —
        /// 창이 SetActive로 켜질 때 부모의 OnEnable이 자식의 Awake보다 먼저 돌기 때문이다.
        /// </summary>
        public bool IsStarted => m_started;

        /// <summary>현재 표시 상태. 전이 중이면 도착 상태다.</summary>
        public ButtonVisualState CurrentState => m_to;

        /// <summary>
        /// 논리 상태를 바깥에서 넣는다. 버튼이 스스로 뒤집지 않으므로,
        /// 시스템이 요청을 거부한 경우(낚시 시작 거부 등)에 표시만 켜지는 어긋남이 생기지 않는다.
        /// </summary>
        public void SetActive(bool isActive)
        {
            if (m_isActive == isActive)
            {
                return;
            }

            m_isActive = isActive;
            GoTo(Resolve(), instant: false);
        }

        private void Awake()
        {
            for (int i = 0; i < m_effects.Count; i++)
            {
                m_effects[i]?.Initialize(this);
            }
            m_awake = true;
        }

        // 비활성화됐다 켜지면 연출이 처음부터 재생되면 안 된다 — 현재 상태의 최종 모습으로 바로 맞춘다.
        private void OnEnable() => GoTo(Resolve(), instant: true);

        // Awake 전에 밀어 둔 반영과, Start 이후에만 되는 일(Animator 즉시 평가)을 여기서 마무리한다.
        private void Start()
        {
            m_started = true;
            GoTo(Resolve(), instant: true);
        }

        private void OnDisable()
        {
            // OnPointerExit이 오지 않으므로 hover가 남지 않게 한다. 표시는 다음 OnEnable이 맞춘다.
            m_isHovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_isHovered = true;
            GoTo(Resolve(), instant: false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_isHovered = false;
            GoTo(Resolve(), instant: false);
        }

        private ButtonVisualState Resolve()
        {
            if (m_isHovered && (m_hoverOverridesActive || m_isActive == false))
            {
                return ButtonVisualState.Hover;
            }
            return m_isActive ? ButtonVisualState.Active : ButtonVisualState.Normal;
        }

        private void GoTo(ButtonVisualState next, bool instant)
        {
            if (m_awake == false)
            {
                return;   // Awake 전 — OnEnable이 다시 부른다
            }

            if (instant || DurationTo(next) <= 0f)
            {
                m_from = next;
                m_to = next;
                m_t = 1f;
            }
            else if (next == m_from && m_t < 1f)
            {
                // 되돌아간다(오버했다 바로 빼는 경우) — 방향만 뒤집어 값이 튀지 않게 한다.
                (m_from, m_to) = (m_to, m_from);
                m_t = 1f - m_t;
                m_duration = DurationTo(next);
            }
            else if (next != m_to)
            {
                m_from = m_to;
                m_to = next;
                m_t = 0f;
                m_duration = DurationTo(next);
            }
            else
            {
                return;   // 이미 그 상태로 가는 중이다
            }

            ApplyAll();
        }

        private void Update()
        {
            if (m_t >= 1f)
            {
                return;
            }

            // Time.unscaledDeltaTime: 일시정지·배속과 무관하게 UI 반응은 같아야 한다.
            m_t = m_duration <= 0f ? 1f : Mathf.Clamp01(m_t + Time.unscaledDeltaTime / m_duration);
            ApplyAll();
        }

        private void ApplyAll()
        {
            for (int i = 0; i < m_effects.Count; i++)
            {
                m_effects[i]?.Apply(m_from, m_to, m_t);
            }
        }

        private float DurationTo(ButtonVisualState state) => state switch
        {
            ButtonVisualState.Hover => m_toHoverDuration,
            ButtonVisualState.Active => m_toActiveDuration,
            _ => m_toNormalDuration,
        };

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 미리보기 전용. 그 상태의 최종 모습을 씬에 그대로 반영한다.
        /// ★실제 값을 바꾸므로 저장 전에 대기 상태로 되돌려야 한다.
        /// </summary>
        public void PreviewState(ButtonVisualState state)
        {
            for (int i = 0; i < m_effects.Count; i++)
            {
                m_effects[i]?.Initialize(this);
                m_effects[i]?.Apply(state, state, 1f);
            }
        }
#endif
    }
}
