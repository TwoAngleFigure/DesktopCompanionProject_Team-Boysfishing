using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 월드 View 유닛의 레지스트리이자 공통 의존성(SystemManager·EntityManager·AssetProvider) 공급자.
    /// 유닛의 등록·주입·Bind 수명을 관리한다. 도메인별 구독 로직은 갖지 않는다.
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        private static WorldManager s_instance;   // 유닛 자가 등록 접근점
        private static readonly List<WorldViewBase> s_pending = new();   // Initialize 전(씬 로드)에 등록 시도한 유닛 대기

        private SystemManager m_systemManager;
        private EntityManager m_entityManager;
        private AssetProvider m_assetProvider;
        private bool m_initialized;

        private readonly List<WorldViewBase> m_views = new();

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// GameManager.OnBootCompleted에서 호출한다. 싱글턴을 지정하고 의존성을 보관한 뒤,
        /// 대기 큐에 쌓인 유닛을 모두 등록·바인딩한다.
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
                if (s_pending[i] != null)
                {
                    RegisterInternal(s_pending[i]);   // m_initialized=true이므로 즉시 Bind
                }
            }
            s_pending.Clear();
        }

        /// <summary>
        /// 유닛을 등록한다(유닛이 자가 호출). Initialize 이전이면 대기 큐에 담고, 이후면 즉시 바인딩한다.
        /// </summary>
        public static void Register(WorldViewBase view)
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

        /// <summary>유닛을 등록 해제한다(대기 큐·활성 목록 양쪽에서 제거).</summary>
        public static void Unregister(WorldViewBase view)
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

        private void RegisterInternal(WorldViewBase view)
        {
            if (m_views.Contains(view))
            {
                return;
            }
            m_views.Add(view);
            if (m_initialized)
            {
                BindView(view);   // 늦게 온 유닛 즉시 바인딩
            }
        }

        private void BindView(WorldViewBase view)
        {
            // 유닛 하나의 주입/Bind 실패가 다른 유닛·부팅 완료를 막지 않도록 격리한다.
            try
            {
                view.Inject(m_systemManager, m_entityManager, m_assetProvider);
                view.Bind();   // 유닛이 자기 System Action을 구독하는 지점
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[WorldManager] View Bind 실패: {view.GetType().Name} — {e}", view);
            }
        }
    }
}
