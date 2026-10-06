using DG.Tweening;
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
    ///
    /// 열림·닫힘 연출(<see cref="m_openDuration"/>·<see cref="m_closeDuration"/>)은 기본이 0이라
    /// 켜지 않으면 기존 동작 그대로다. 닫힘 연출은 표시만 미루고 입력·활성 스택은 즉시 닫는다.
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

        [Header("Open Animation")]
        [Tooltip("열릴 때 재생할 연출 시간(초). 0이면 연출 없이 즉시 표시된다")]
        [SerializeField, Min(0f)] private float m_openDuration = 0f;

        [Tooltip("아래에서 위로 올라오는 거리(px). 0이면 알파만 변한다")]
        [SerializeField] private float m_openRise = 40f;

        [SerializeField] private Ease m_openEase = Ease.OutCubic;

        [Tooltip("연출 대상. 비우면 이 창 자신을 움직인다. " +
                 "★Participate In Layout이 켜진 창은 UIManager가 좌표를 소유하므로 반드시 지정해야 한다 " +
                 "— 지정하지 않으면 이동이 지워지고 알파 페이드만 재생된다")]
        [SerializeField] private RectTransform m_openAnimationRoot;

        [Tooltip("닫힐 때 재생할 연출 시간(초). 0이면 즉시 닫힌다. 열림의 반대로 내려가며 사라진다. " +
                 "거리·연출 대상은 열림 설정을 그대로 쓴다")]
        [SerializeField, Min(0f)] private float m_closeDuration = 0f;

        [SerializeField] private Ease m_closeEase = Ease.InCubic;

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
                    PlayOpenAnimation();
                }
                else
                {
                    SetVisible(false);   // 활성이지만 닫힘(스택 밖)
                }
            }
            else // Deactivate: 활성 = 열림
            {
                UIManager.PushActiveWindow(this);
                PlayOpenAnimation();
            }
        }

        protected override void OnDisable()
        {
            KillCloseAnimation();               // 꺼진 채로 트윈이 남지 않게
            KillOpenAnimation();
            UIManager.RemoveActiveWindow(this); // 스택에서 제거
            base.OnDisable();                   // UIViewBase: Unbind + Unregister
        }

        /// <summary>창을 열고 활성 스택 top으로 올린다(HideMode에 따른 방식으로).</summary>
        public void Show()
        {
            KillCloseAnimation();   // 닫히는 중이었다면 되돌리고 다시 연다

            if (m_hideMode == HideMode.CanvasGroup)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);   // 비활성이었다면 활성화(OnEnable에서 등록/바인드)
                }
                SetVisible(true);
                UIManager.PushActiveWindow(this);
                PlayOpenAnimation();              // 재배치가 끝난 뒤라야 도착 위치를 옳게 잡는다
                return;
            }

            // Deactivate
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);       // → OnEnable에서 Register+Bind+Push+연출
            }
            else
            {
                UIManager.PushActiveWindow(this); // 이미 활성이면 스택 top으로
                PlayOpenAnimation();
            }
        }

        /// <summary>
        /// 창을 닫는다. CanvasGroup 모드는 오브젝트를 유지하고, Deactivate 모드는 SetActive(false)한다.
        /// 닫힘 연출을 켜 두면 표시만 남기고 실제 닫기는 연출이 끝난 뒤에 한다 —
        /// 입력·활성 스택·<see cref="IsShown"/>은 호출 즉시 닫힌 것으로 바뀐다.
        /// </summary>
        public void Hide()
        {
            if (TryPlayCloseAnimation())
            {
                return;   // 마무리는 연출 완료 콜백이 한다
            }

            HideImmediate();
        }

        private void HideImmediate()
        {
            KillOpenAnimation();

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

        // ── 열림 연출 ──

        private Sequence m_openSequence;
        private Vector2 m_openBasePosition;   // 연출이 끝나야 할 제자리
        private bool m_openAnimating;
        private bool m_openMoved;             // 이번 연출이 위치까지 건드렸는지
        private bool m_openWarned;

        private Sequence m_closeSequence;
        private Vector2 m_closeBasePosition;
        private bool m_closing;
        private bool m_closeMoved;

        /// <summary>연출로 움직일 대상. 지정이 없으면 창 자신이다.</summary>
        private RectTransform OpenAnimationRoot =>
            m_openAnimationRoot != null ? m_openAnimationRoot : transform as RectTransform;

        /// <summary>
        /// 알파를 올리며 아래에서 제자리로 띄운다. 자동 배치가 끝난 뒤에 불러야 도착 위치가 맞는다.
        /// 시간이 0이면 아무 일도 하지 않아, 연출을 쓰지 않는 창은 기존과 완전히 동일하게 동작한다.
        ///
        /// 이동은 좌표 소유권이 이쪽에 있을 때만 한다. 자동 배치에 참여하는 창의 anchoredPosition은
        /// <see cref="UIManager"/>가 매 재배치마다 다시 쓰므로, 그 창을 직접 움직이면 이동이 지워진다.
        /// 그런 창은 자식 컨테이너를 <see cref="m_openAnimationRoot"/>로 지정해 축을 나눈다.
        /// </summary>
        private void PlayOpenAnimation()
        {
            if (m_openDuration <= 0f)
            {
                return;
            }

            RectTransform root = OpenAnimationRoot;
            if (root == null)
            {
                return;
            }

            EnsureCanvasGroup();
            KillOpenAnimation();   // 연출 중 다시 열려도 시작 위치가 아래로 누적되지 않게 제자리부터 잡는다

            bool canMove = m_openRise != 0f && (m_openAnimationRoot != null || m_participateInLayout == false);
            WarnIfMoveBlocked(canMove);

            m_openAnimating = true;
            m_canvasGroup.alpha = 0f;

            m_openSequence = DOTween.Sequence()
                                    .Join(m_canvasGroup.DOFade(1f, m_openDuration).SetEase(Ease.OutQuad))
                                    .SetUpdate(true)        // timeScale 무관
                                    .SetLink(gameObject)    // 파괴 시 자동 정리
                                    .OnComplete(() => m_openAnimating = false);

            if (canMove == false)
            {
                return;   // 알파만 재생한다
            }

            m_openMoved = true;
            m_openBasePosition = root.anchoredPosition;
            root.anchoredPosition = m_openBasePosition + Vector2.down * m_openRise;

            m_openSequence.Join(root.DOAnchorPos(m_openBasePosition, m_openDuration).SetEase(m_openEase));
        }

        /// <summary>이동이 막힌 설정을 한 번만 알린다. 조용히 안 움직이면 원인을 찾기 어렵다.</summary>
        private void WarnIfMoveBlocked(bool canMove)
        {
            if (canMove || m_openWarned || m_openRise == 0f)
            {
                return;
            }

            m_openWarned = true;
            Debug.LogWarning("[UIWindowBase] Participate In Layout이 켜진 창은 UIManager가 좌표를 소유해 " +
                             "이동 연출이 지워진다. 내용물을 감싼 자식을 Open Animation Root로 지정하라 " +
                             "(알파 페이드만 재생한다).", this);
        }

        /// <summary>
        /// 열림의 반대로 내려가며 사라진다. 연출을 시작했으면 true — 실제 닫기는 완료 뒤로 미뤄진다.
        ///
        /// 표시만 미루고 <b>입력·활성 스택은 즉시</b> 닫는다. 사라지는 중인 창이 클릭을 먹거나
        /// 자동 배치 자리를 붙잡고 있으면 안 되기 때문이다(남은 창들은 바로 재배치된다).
        /// </summary>
        private bool TryPlayCloseAnimation()
        {
            if (m_closeDuration <= 0f || gameObject.activeInHierarchy == false || IsShown == false)
            {
                return false;   // 이미 닫힌 창은 연출 없이 통과시킨다
            }
            if (m_closing)
            {
                return true;   // 이미 닫히는 중 — 다시 시작하지 않는다
            }

            KillOpenAnimation();   // 열림 연출이 남아 있으면 제자리로 확정하고 끊는다
            EnsureCanvasGroup();

            m_closing = true;
            m_canvasGroup.interactable = false;
            m_canvasGroup.blocksRaycasts = false;   // 이 시점부터 IsShown = false
            UIManager.RemoveActiveWindow(this);

            m_closeSequence = DOTween.Sequence()
                                     .Join(m_canvasGroup.DOFade(0f, m_closeDuration).SetEase(Ease.InQuad))
                                     .SetUpdate(true)
                                     .SetLink(gameObject)
                                     .OnComplete(FinishClose);

            RectTransform root = OpenAnimationRoot;
            bool canMove = root != null && m_openRise != 0f &&
                           (m_openAnimationRoot != null || m_participateInLayout == false);

            if (canMove == false)
            {
                return true;   // 알파만 재생한다
            }

            m_closeMoved = true;
            m_closeBasePosition = root.anchoredPosition;

            m_closeSequence.Join(root.DOAnchorPos(m_closeBasePosition + Vector2.down * m_openRise, m_closeDuration)
                                     .SetEase(m_closeEase));
            return true;
        }

        private void FinishClose()
        {
            m_closeSequence = null;
            KillCloseAnimation();   // 자리·알파 복구
            HideImmediate();
        }

        /// <summary>닫힘 연출을 끊고 제자리·불투명으로 되돌린다. 닫히는 중에 다시 열 때도 쓴다.</summary>
        private void KillCloseAnimation()
        {
            if (m_closeSequence != null)
            {
                m_closeSequence.Kill();
                m_closeSequence = null;
            }

            if (m_closing == false)
            {
                return;
            }
            m_closing = false;

            if (m_closeMoved)
            {
                m_closeMoved = false;

                RectTransform root = OpenAnimationRoot;
                if (root != null)
                {
                    root.anchoredPosition = m_closeBasePosition;
                }
            }

            if (m_canvasGroup == null)
            {
                return;
            }

            // 열린 상태로 되돌린다. 닫힘 확정은 HideImmediate의 SetVisible/비활성화가 이어서 한다.
            // 여기서 blocksRaycasts를 되살리지 않으면 Deactivate 모드 창이 다시 열려도 IsShown이 false로 남는다.
            m_canvasGroup.alpha = 1f;
            m_canvasGroup.interactable = true;
            m_canvasGroup.blocksRaycasts = true;
        }

        /// <summary>연출을 끊고 제자리·불투명으로 확정한다. 중간에 닫아도 창이 어긋난 채 남지 않는다.</summary>
        private void KillOpenAnimation()
        {
            if (m_openSequence != null)
            {
                m_openSequence.Kill();
                m_openSequence = null;
            }

            if (m_openAnimating == false)
            {
                return;
            }
            m_openAnimating = false;

            if (m_openMoved)
            {
                m_openMoved = false;

                RectTransform root = OpenAnimationRoot;
                if (root != null)
                {
                    root.anchoredPosition = m_openBasePosition;
                }
            }

            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 1f;   // 닫힘 처리는 SetVisible/비활성화가 이어서 한다
            }
        }

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
