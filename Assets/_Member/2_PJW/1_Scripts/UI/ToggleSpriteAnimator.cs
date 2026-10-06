using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 스프라이트 애니메이션으로 켜짐/꺼짐을 연출하는 토글.
    /// 상태는 uGUI <see cref="Toggle"/>이 소유하고, 이 컴포넌트는 그 값을 Animator의 bool 파라미터로 넘기기만 한다.
    /// 어떤 클립을 언제 재생할지는 전부 Animator Controller에서 정하므로,
    /// 나중에 꺼지는 연출 클립을 추가해도 코드는 손대지 않는다.
    ///
    /// 켜진 채로 창이 다시 열릴 때 켜지는 연출이 처음부터 재생되면 안 되므로,
    /// 활성화 시점에는 전이를 건너뛰고 목표 상태의 마지막 프레임으로 바로 맞춘다.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    [DisallowMultipleComponent]
    public class ToggleSpriteAnimator : MonoBehaviour
    {
        [Tooltip("비우면 이 오브젝트와 자식에서 찾는다")]
        [SerializeField] private Animator m_animator;

        [Tooltip("켜짐 여부를 넘길 bool 파라미터 이름")]
        [SerializeField] private string m_onParameter = "On";

        [Header("즉시 반영용 상태 이름")]
        [Tooltip("켜짐 상태의 Animator State 이름. 비우면 활성화 시 전이가 그대로 재생된다")]
        [SerializeField] private string m_onStateName = "On";
        [Tooltip("꺼짐 상태의 Animator State 이름")]
        [SerializeField] private string m_offStateName = "Off";

        private Toggle m_toggle;
        private int m_onParameterHash;
        private int m_onStateHash;
        private int m_offStateHash;

        // Start 전에는 Animator를 건드리지 않는다(§Apply). 첫 활성화에서 미뤄 둔 동기화가 있는지도 함께 들고 있는다.
        private bool m_started;
        private bool m_pendingInstantSync;

        private void Awake()
        {
            m_toggle = GetComponent<Toggle>();

            if (m_animator == null)
            {
                m_animator = GetComponentInChildren<Animator>(true);
            }

            m_onParameterHash = Animator.StringToHash(m_onParameter);
            m_onStateHash = string.IsNullOrEmpty(m_onStateName) ? 0 : Animator.StringToHash(m_onStateName);
            m_offStateHash = string.IsNullOrEmpty(m_offStateName) ? 0 : Animator.StringToHash(m_offStateName);
        }

        private void OnEnable()
        {
            m_toggle.onValueChanged.AddListener(HandleValueChanged);

            // Animator는 비활성화됐다 켜지면 기본 상태로 돌아간다 — 지금 값으로 다시 맞춘다.
            Apply(m_toggle.isOn, true);
        }

        /// <summary>
        /// 첫 활성화에서 미뤄 둔 동기화를 마친다. Start는 그 활성화 묶음의 Awake가 전부 끝난 뒤에 오므로,
        /// 여기서는 자식 Animator도 반드시 깨어 있다. 화면에 그려지기 전이라 한 프레임 깜빡임은 없다.
        /// </summary>
        private void Start()
        {
            m_started = true;

            if (m_pendingInstantSync)
            {
                Apply(m_toggle.isOn, true);
            }
        }

        private void OnDisable()
        {
            m_toggle.onValueChanged.RemoveListener(HandleValueChanged);
        }

        private void HandleValueChanged(bool isOn) => Apply(isOn, false);

        /// <param name="instant">전이를 건너뛰고 목표 상태의 끝 프레임으로 맞출지. 활성화 직후 동기화에 쓴다.</param>
        private void Apply(bool isOn, bool instant)
        {
            if (m_animator == null || m_animator.runtimeAnimatorController == null)
            {
                return;
            }

            // 창이 SetActive로 켜질 때 Unity는 부모의 OnEnable을 자식의 Awake보다 먼저 부른다.
            // Animator가 자식에 있으면 이 시점의 그것은 아직 Awake 전이라, SetBool·Play는 Awake의
            // 기본 상태 초기화에 덮여 사라지고 Update()는 'm_DidAwake' 어서션을 낸다.
            // Start까지 미뤘다가 그때 한 번에 맞춘다.
            if (m_started == false)
            {
                m_pendingInstantSync |= instant;
                return;
            }
            m_pendingInstantSync = false;

            m_animator.SetBool(m_onParameterHash, isOn);

            if (instant == false)
            {
                return;
            }

            int stateHash = isOn ? m_onStateHash : m_offStateHash;
            if (stateHash != 0)
            {
                m_animator.Play(stateHash, 0, 1f);   // 끝 프레임 = 연출이 이미 끝난 모습
            }
            m_animator.Update(0f);                   // 다음 프레임까지 기다리지 않고 지금 반영
        }
    }
}
