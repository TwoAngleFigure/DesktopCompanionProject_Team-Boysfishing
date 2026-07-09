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

        private SystemManager m_systemManager;
        private EntityManager m_entityManager;
        private AssetProvider m_assetProvider;
        private bool m_initialized;

        private readonly List<UIViewBase> m_views = new();
        private readonly List<UIWindowBase> m_activeWindows = new();   // 마지막에 열린 것이 맨 뒤(top)

        private void Awake() => s_instance = this;

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>GameManager.OnBootCompleted에서 호출(의존성 주입 + 대기 View 일괄 바인딩).</summary>
        public void Initialize(SystemManager systemManager, EntityManager entityManager, AssetProvider assetProvider)
        {
            m_systemManager = systemManager;
            m_entityManager = entityManager;
            m_assetProvider = assetProvider;
            m_initialized = true;

            for (int i = 0; i < m_views.Count; i++)
            {
                BindView(m_views[i]);
            }
        }

        // ── 자가 등록·수명 (WorldManager와 동일 패턴) ──

        public static void Register(UIViewBase view)
        {
            if (s_instance == null || view == null)
            {
                return;
            }

            if (s_instance.m_views.Contains(view))
            {
                return;
            }

            s_instance.m_views.Add(view);

            if (s_instance.m_initialized)
            {
                s_instance.BindView(view);
            }
        }

        public static void Unregister(UIViewBase view)
        {
            if (s_instance == null || view == null)
            {
                return;
            }

            s_instance.m_views.Remove(view);
        }

        private void BindView(UIViewBase view)
        {
            view.Inject(m_systemManager, m_entityManager, m_assetProvider);
            view.Bind();
        }

        // ── 활성 윈도우 관리 (윈도우형 전용) ──

        /// <summary>윈도우가 켜질 때(OnEnable) 호출 — 최근 열림을 스택 top으로.</summary>
        public static void PushActiveWindow(UIWindowBase window)
        {
            if (s_instance == null || window == null)
            {
                return;
            }

            s_instance.m_activeWindows.Remove(window);   // 재진입 시 중복 방지
            s_instance.m_activeWindows.Add(window);      // 최근 열림 = 맨 뒤
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
