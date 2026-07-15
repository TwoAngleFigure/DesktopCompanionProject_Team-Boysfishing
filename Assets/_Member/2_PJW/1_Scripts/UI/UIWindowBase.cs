using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 윈도우형 UI(인벤토리 등)만 상속하는 베이스. 일반 UI(HUD 등)는 UIViewBase를 그대로 쓴다.
    /// 활성(=표시)에 연동해 UIManager의 활성 윈도우 스택에 참여한다.
    /// 우클릭 닫기(UIManager.CloseTopWindow)는 이 스택의 top(가장 최근 열린 창)을 닫는다.
    ///
    /// 창 on/off는 <see cref="HideMode"/>로 선택한다(인스펙터 토글):
    ///  - Deactivate(기본): GameObject SetActive(false) — 현행과 동일. OnDisable→Unbind로 숨김 중 System 수신·렌더 0(단 페이드 불가·재바인드 비용).
    ///  - CanvasGroup: alpha0+blocksRaycasts off — 즉시·페이드·상태 보존(단 VM 바인드 유지 → 숨김 중 CPU 소모).
    /// 두 방식의 성능/비용 분석은 계획 Docs/Plan/10_WindowUIManagement.md 참고.
    /// ※ Bind/Unbind는 기존 경로(UIViewBase 등록/OnDisable)만 담당한다 — Show/Hide는 바인드를 이중으로 건드리지 않는다.
    /// </summary>
    public abstract class UIWindowBase : UIViewBase
    {
        public enum HideMode { Deactivate, CanvasGroup }

        [Header("Window")]
        [Tooltip("자동 배치(우측→좌측) 참여 여부. false면 자체 위치 유지(닫기 스택에는 계속 참여)")]
        [SerializeField] private bool m_participateInLayout = true;

        [Tooltip("숨김 방식. Deactivate=SetActive(기본, 현행 동일) / CanvasGroup=alpha0(즉시·페이드·상태보존)")]
        [SerializeField] private HideMode m_hideMode = HideMode.Deactivate;

        [Tooltip("CanvasGroup 모드용. 비우면 필요 시 자동 추가")]
        [SerializeField] private CanvasGroup m_canvasGroup;

        [Tooltip("(선택) 창 전용 Canvas — CanvasGroup 숨김 시 렌더 제외해 드로우콜 절감")]
        [SerializeField] private Canvas m_ownCanvas;

        [Tooltip("Play 시작 시 열린 상태로 둘지. 기본 false = 닫힘(씬에서 활성으로 둬도 Play 시 닫힘, 버튼/코드로 Show)")]
        [SerializeField] private bool m_openOnStart = false;

        private bool m_lifecycleActive;   // base.OnEnable(등록·바인드)을 실제로 탄 상태인지

        /// <summary>자동 배치 참여 여부(UIManager가 읽음).</summary>
        public bool ParticipatesInLayout => m_participateInLayout;

        /// <summary>현재 표시(보이고 입력 받는) 상태인지.</summary>
        public bool IsShown => gameObject.activeSelf && (m_canvasGroup == null || m_canvasGroup.blocksRaycasts);

        protected override void OnEnable()
        {
            // 씬 로드 시(부팅 전) 활성 창은 초기 표시하지 않는다. base(Register/Bind) 호출 전에 닫아
            // 플래시와 'Unbind-without-Bind'를 모두 회피한다. 나중에 Show()(부팅 후)면 정상 오픈.
            if (!UIManager.IsBooted && !m_openOnStart)
            {
                gameObject.SetActive(false);
                return;
            }

            base.OnEnable();                    // UIViewBase: 자가 등록(→ Bind)
            m_lifecycleActive = true;
            if (m_hideMode == HideMode.CanvasGroup)
            {
                SetVisible(true);               // 활성화 = 표시(CanvasGroup 모드에서 이전 Hide의 alpha0 초기화)
            }
            UIManager.PushActiveWindow(this);   // 최근 열림 = 스택 top
        }

        protected override void OnDisable()
        {
            // OnEnable에서 초기-닫힘으로 base를 안 탄 경우엔 Register/Bind가 없으므로 Unregister/Unbind도 스킵.
            if (!m_lifecycleActive)
            {
                return;
            }
            m_lifecycleActive = false;
            UIManager.RemoveActiveWindow(this); // 스택에서 제거
            base.OnDisable();                   // UIViewBase: Unbind + Unregister
        }

        /// <summary>창을 켠다. 비활성이면 활성화(→OnEnable), 이미 활성(CanvasGroup 숨김)이면 표시+스택 등록.</summary>
        public void Show()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);     // → OnEnable에서 Register+Bind(+표시)+Push
                return;
            }
            if (m_hideMode == HideMode.CanvasGroup)
            {
                SetVisible(true);
            }
            UIManager.PushActiveWindow(this);   // 멱등(Remove 후 Add)
        }

        /// <summary>창을 끈다. HideMode에 따라 CanvasGroup 숨김 또는 SetActive(false).</summary>
        public void Hide()
        {
            if (m_hideMode == HideMode.Deactivate)
            {
                gameObject.SetActive(false);    // → OnDisable에서 Remove + Unbind
                return;
            }
            SetVisible(false);                  // CanvasGroup: 오브젝트·VM 유지
            UIManager.RemoveActiveWindow(this);
        }

        /// <summary>이 윈도우를 닫는다(팀원 호출 지점 유지 — 본문만 Hide로).</summary>
        public void Close() => Hide();

        private void SetVisible(bool on)
        {
            EnsureCanvasGroup();
            m_canvasGroup.alpha = on ? 1f : 0f;
            m_canvasGroup.interactable = on;
            m_canvasGroup.blocksRaycasts = on;
            if (m_ownCanvas != null)
            {
                m_ownCanvas.enabled = on;       // (선택) 드로우콜 절감
            }
        }

        private void EnsureCanvasGroup()
        {
            if (m_canvasGroup == null)
            {
                m_canvasGroup = GetComponent<CanvasGroup>();
                if (m_canvasGroup == null)
                {
                    m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
    }
}
