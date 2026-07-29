using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 윈도우형 UI의 베이스. 표시 상태에 연동해 UIManager의 활성 윈도우 스택에 참여하고,
    /// 자동 배치·중앙 닫기 요청의 대상이 된다. 창이 아닌 일반 UI는 UIViewBase를 쓴다.
    ///
    /// 개폐 방식은 <see cref="HideMode"/>가 결정한다:
    ///  - Deactivate: GameObject SetActive로 개폐한다. 씬의 활성 상태가 곧 초기 상태다.
    ///  - CanvasGroup: GameObject를 활성으로 유지하고 CanvasGroup(alpha·interactable·blocksRaycasts)으로만
    ///    개폐한다. 초기 표시는 <see cref="m_openOnStart"/>가 결정한다.
    /// </summary>
    public abstract class UIWindowBase : UIViewBase
    {
        public enum HideMode { Deactivate, CanvasGroup }

        [Header("Window")]
        [Tooltip("자동 배치(우측→좌측) 참여 여부. false면 자체 위치 유지(닫기 스택에는 계속 참여)")]
        [SerializeField] private bool m_participateInLayout = true;

        [Tooltip("중앙 닫기 요청(우클릭·ESC 등 UIManager.CloseTopWindow)의 대상이 될지 여부. " +
                 "false면 그 수단으로는 닫히지 않는다(요청은 이 창을 건너뛰고 그 아래 창을 닫는다). " +
                 "상시 표시 HUD성 창 보호용. Close()/Hide()/토글 버튼 등 직접 닫기는 그대로 동작")]
        [SerializeField] private bool m_closableByShortcut = true;

        [Tooltip("개폐 방식. Deactivate=SetActive(기본) / CanvasGroup=항상 활성+alpha(즉시·페이드·상태보존)")]
        [SerializeField] private HideMode m_hideMode = HideMode.Deactivate;

        [Tooltip("CanvasGroup 모드용. 비우면 필요 시 자동 추가")]
        [SerializeField] private CanvasGroup m_canvasGroup;

        [Tooltip("(선택) 창 전용 Canvas — CanvasGroup 숨김 시 렌더 제외해 드로우콜 절감")]
        [SerializeField] private Canvas m_ownCanvas;

        [Tooltip("CanvasGroup 모드에서 Play 시작 시 열린 채로 둘지(기본 false=숨김). " +
                 "Deactivate 모드는 씬의 활성상태가 곧 초기상태(비활성=닫힘).")]
        [SerializeField] private bool m_openOnStart = false;

        /// <summary>자동 배치 참여 여부. UIManager가 읽는다.</summary>
        public bool ParticipatesInLayout => m_participateInLayout;

        /// <summary>중앙 닫기 요청의 대상이 될지 여부. false여도 활성 스택·자동 배치에는 계속 참여한다.</summary>
        public bool ClosableByShortcut => m_closableByShortcut;

        /// <summary>현재 보이면서 입력을 받는 상태인지. CanvasGroup으로 숨긴 창은 false다.</summary>
        public bool IsShown => gameObject.activeSelf && (m_canvasGroup == null || m_canvasGroup.blocksRaycasts);

        protected override void OnEnable()
        {
            base.OnEnable();   // UIViewBase: 자가 등록(부팅 시 Bind)

            if (m_hideMode == HideMode.CanvasGroup)
            {
                // GameObject는 활성 유지. 초기 표시는 openOnStart로 결정(기본 숨김) → SetActive는 안 건드림.
                if (m_openOnStart)
                {
                    SetVisible(true);
                    UIManager.PushActiveWindow(this);
                }
                else
                {
                    SetVisible(false);   // 활성이지만 닫힘(스택 밖)
                }
            }
            else // Deactivate: 활성 = 열림
            {
                UIManager.PushActiveWindow(this);
            }
        }

        protected override void OnDisable()
        {
            UIManager.RemoveActiveWindow(this); // 스택에서 제거
            base.OnDisable();                   // UIViewBase: Unbind + Unregister
        }

        /// <summary>창을 열고 활성 스택 top으로 올린다(HideMode에 따른 방식으로).</summary>
        public void Show()
        {
            if (m_hideMode == HideMode.CanvasGroup)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);   // 비활성이었다면 활성화(OnEnable에서 등록/바인드)
                }
                SetVisible(true);
                UIManager.PushActiveWindow(this);
                return;
            }

            // Deactivate
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);       // → OnEnable에서 Register+Bind+Push
            }
            else
            {
                UIManager.PushActiveWindow(this); // 이미 활성이면 스택 top으로
            }
        }

        /// <summary>창을 닫는다. CanvasGroup 모드는 오브젝트를 유지하고, Deactivate 모드는 SetActive(false)한다.</summary>
        public void Hide()
        {
            if (m_hideMode == HideMode.CanvasGroup)
            {
                SetVisible(false);
                UIManager.RemoveActiveWindow(this);
                return;
            }
            gameObject.SetActive(false);          // → OnDisable에서 Remove + Unbind
        }

        /// <summary>이 윈도우를 닫는다(<see cref="Hide"/>와 동일).</summary>
        public void Close() => Hide();

        private void SetVisible(bool on)
        {
            EnsureCanvasGroup();
            m_canvasGroup.alpha = on ? 1f : 0f;
            m_canvasGroup.interactable = on;
            m_canvasGroup.blocksRaycasts = on;
            if (m_ownCanvas != null)
            {
                m_ownCanvas.enabled = on;         // (선택) 드로우콜 절감
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
