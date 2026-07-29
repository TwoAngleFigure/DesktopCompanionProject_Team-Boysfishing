using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 'UI'의 중앙 관리자(WorldManager와 동형). 도메인 구독은 갖지 않는다 —
    /// 개별 UI는 팀원이 UIViewBase/UIWindowBase를 상속해 만들고, System 구독은 각 ViewModel 안에서.
    /// UIManager는 유닛의 호스트/레지스트리 + 공통 의존성 공급자 + 활성 윈도우 관리자 + 자동 배치자다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private static UIManager s_instance;   // 유닛 자가 등록 접근점
        private static readonly List<UIViewBase> s_pending = new();   // Initialize 전(씬 로드)에 등록 시도한 뷰 대기

        [Header("Window Layout")]
        [Tooltip("창 사이 간격(px)")]
        [SerializeField] private float m_windowPadding = 10f;
        [Tooltip("첫 창 오른쪽 가장자리와 화면 우측 사이 여백(px)")]
        [SerializeField] private float m_rightMargin = 20f;
        [Tooltip("세로 중앙에서의 y 오프셋(px)")]
        [SerializeField] private float m_verticalCenterOffset = 0f;
        [Tooltip("자동 배치 on/off")]
        [SerializeField] private bool m_autoLayout = true;

        private SystemManager m_systemManager;
        private EntityManager m_entityManager;
        private AssetProvider m_assetProvider;
        private bool m_initialized;

        private readonly List<UIViewBase> m_views = new();
        private readonly List<UIWindowBase> m_activeWindows = new();   // 마지막에 열린 것이 맨 뒤(top)

        private void AdoptPending(UIViewBase view)
        {
            if (view == null)
            {
                return;
            }
            RegisterInternal(view);
            // 이미 '표시 중'인 윈도우만 활성 스택에 반영한다(OnEnable의 Push가 s_instance null로 유실됐을 수 있음).
            // ※ IsShown 기준: CanvasGroup 창은 활성이어도 숨김(닫힘)이면 스택에 넣지 않는다.
            if (view is UIWindowBase window && window.IsShown)
            {
                PushActiveWindowInternal(window);
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// GameManager.OnBootCompleted에서 호출. 이 시점에 싱글턴을 지정하고(조립 루트가 수명을 통제),
        /// Initialize 이전(씬 로드)에 등록을 시도해 대기 중이던 View를 흡수·바인딩한다.
        /// </summary>
        public void Initialize(SystemManager systemManager, EntityManager entityManager, AssetProvider assetProvider)
        {
            s_instance = this;
            m_systemManager = systemManager;
            m_entityManager = entityManager;
            m_assetProvider = assetProvider;
            m_initialized = true;

            // 스냅샷 후 비우고 순회한다. Bind가 다른 뷰를 켜고 끌 수 있고(예: UITabWindow의 초기 탭 적용 →
            // 비선택 창 SetActive(false) → OnDisable → Unregister → s_pending.Remove), 그 제거가
            // 순회 중인 목록을 밀어 항목을 건너뛰게 하기 때문이다.
            UIViewBase[] pending = s_pending.ToArray();
            s_pending.Clear();
            for (int i = 0; i < pending.Length; i++)
            {
                AdoptPending(pending[i]);
            }
        }

        // ── 자가 등록·수명 (WorldManager와 동일 패턴) ──

        public static void Register(UIViewBase view)
        {
            if (view == null)
            {
                return;
            }
            if (s_instance == null)
            {
                if (!s_pending.Contains(view))   // 매니저 Initialize 전 — 유실 대신 대기
                {
                    s_pending.Add(view);
                }
                return;
            }
            s_instance.RegisterInternal(view);
        }

        public static void Unregister(UIViewBase view)
        {
            if (view == null)
            {
                return;
            }
            s_pending.Remove(view);   // 아직 대기 중이었다면 제거
            if (s_instance != null)
            {
                s_instance.m_views.Remove(view);
            }
        }

        private void RegisterInternal(UIViewBase view)
        {
            if (m_views.Contains(view))
            {
                return;
            }
            m_views.Add(view);
            if (m_initialized)
            {
                BindView(view);
            }
        }

        private void BindView(UIViewBase view)
        {
            // 뷰 하나의 주입/Bind 실패가 다른 뷰·부팅 완료를 막지 않도록 격리한다.
            try
            {
                view.Inject(m_systemManager, m_entityManager, m_assetProvider);
                view.Bind();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[UIManager] View Bind 실패: {view.GetType().Name} — {e}", view);
            }
        }

        // ── 활성 윈도우 관리 (윈도우형 전용) ──

        /// <summary>윈도우가 켜질 때(OnEnable) 호출 — 최근 열림을 스택 top으로.</summary>
        public static void PushActiveWindow(UIWindowBase window)
        {
            if (s_instance == null || window == null)
            {
                return;
            }
            s_instance.PushActiveWindowInternal(window);
        }

        private void PushActiveWindowInternal(UIWindowBase window)
        {
            m_activeWindows.Remove(window);   // 재진입 시 중복 방지
            m_activeWindows.Add(window);      // 최근 열림 = 맨 뒤
            RelayoutWindows();
        }

        /// <summary>윈도우가 꺼질 때(OnDisable/Hide) 호출 — 스택에서 제거.</summary>
        public static void RemoveActiveWindow(UIWindowBase window)
        {
            if (s_instance == null || window == null)
            {
                return;
            }

            s_instance.RemoveActiveWindowInternal(window);
        }

        private void RemoveActiveWindowInternal(UIWindowBase window)
        {
            if (m_activeWindows.Remove(window))
            {
                RelayoutWindows();
            }
        }

        /// <summary>
        /// 활성 창을 우측→좌측으로 재배치(오래된 것=우측 가장자리·세로 중앙, 최근=좌측). 빈틈 제거.
        /// 대상 창은 고정 크기 + Canvas(또는 전체화면 루트) 직속 자식이어야 앵커(1,0.5)가 화면 우측·세로중앙과 일치.
        /// </summary>
        private void RelayoutWindows()
        {
            if (!m_autoLayout)
            {
                return;
            }

            float x = -m_rightMargin;
            for (int i = 0; i < m_activeWindows.Count; i++)
            {
                UIWindowBase w = m_activeWindows[i];
                if (w == null || !w.ParticipatesInLayout)
                {
                    continue;   // 배치 예외 창은 건너뜀(닫기 스택에는 계속 참여)
                }

                if (w.transform is not RectTransform rt)
                {
                    continue;
                }

                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);   // 우측·세로중앙 기준
                rt.pivot = new Vector2(1f, 0.5f);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);        // 동적 크기(ContentSizeFitter) 대응
                float width = rt.rect.width;

                rt.anchoredPosition = new Vector2(x, m_verticalCenterOffset);
                x -= (width + m_windowPadding);                         // 다음 창은 왼쪽으로
            }
        }

        /// <summary>
        /// 중앙 닫기 요청(우클릭·ESC 등)의 단일 처리 지점. 활성 윈도우를 최근 열린 순(LIFO)으로 훑어
        /// <see cref="UIWindowBase.ClosableByShortcut"/>가 켜진 첫 창을 닫는다.
        /// 끈 창(보호 창)은 건너뛰므로 이 경로에서는 보이지 않는 셈이며, 그 아래 창이 닫힌다.
        /// ※ 특정 창을 지목해 닫으려면 이 함수가 아니라 그 창의 Close()/Hide()를 직접 부른다.
        /// </summary>
        public void CloseTopWindow()
        {
            for (int i = m_activeWindows.Count - 1; i >= 0; i--)
            {
                UIWindowBase window = m_activeWindows[i];
                if (window == null)
                {
                    continue;
                }
                if (window.ClosableByShortcut == false)
                {
                    continue;   // 보호 창 — 건너뛰고 아래 창을 찾는다
                }

                window.Close();   // → Hide() (HideMode에 따라 CanvasGroup 숨김 또는 SetActive(false)) → RemoveActiveWindow
                return;
            }
        }

        /// <summary>중앙 닫기 요청(정적 통로 — 우클릭·ESC 등 닫기 입력에서 호출. 보호 창은 건너뛴다).</summary>
        public static void RequestCloseTopWindow() => s_instance?.CloseTopWindow();
    }
}
