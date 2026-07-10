using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 'UI'의 중앙 관리자(WorldManager와 동형). 도메인 구독은 갖지 않는다 —
    /// 개별 UI는 팀원이 UIViewBase/UIWindowBase를 상속해 만들고, System 구독은 각 ViewModel 안에서.
    /// UIManager는 유닛의 호스트/레지스트리 + 공통 의존성 공급자 + 활성 윈도우 관리자다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private static UIManager s_instance;   // 유닛 자가 등록 접근점
        private static readonly List<UIViewBase> s_pending = new();   // Initialize 전(씬 로드)에 등록 시도한 뷰 대기

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
            // 윈도우가 이미 활성이면 활성 스택에도 반영한다(OnEnable의 PushActiveWindow도 s_instance null로 유실됐을 수 있음).
            if (view is UIWindowBase window && window.isActiveAndEnabled)
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

            for (int i = 0; i < s_pending.Count; i++)
            {
                AdoptPending(s_pending[i]);
            }
            s_pending.Clear();
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
        }

        /// <summary>윈도우가 꺼질 때(OnDisable) 호출 — 스택에서 제거.</summary>
        public static void RemoveActiveWindow(UIWindowBase window)
        {
            if (s_instance == null || window == null)
            {
                return;
            }

            s_instance.m_activeWindows.Remove(window);
        }

        /// <summary>
        /// 활성 윈도우 중 가장 최근에 열린 것을 닫는다(우클릭 닫기용).
        /// ※ 입력과 미연결: 추후 InputManager가 우클릭을 감지해 이 함수를 호출한다. 지금은 연결하지 않는다.
        /// </summary>
        public void CloseTopWindow()
        {
            if (m_activeWindows.Count == 0)
            {
                return;
            }

            UIWindowBase top = m_activeWindows[m_activeWindows.Count - 1];
            top.Close();   // SetActive(false) → OnDisable → RemoveActiveWindow
        }
    }
}
