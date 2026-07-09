using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 'World'(3D 월드 조작)의 중앙 관리자.
    /// 도메인별 구독 로직을 직접 갖지 않는다 — 개별 오브젝트는 팀원이 WorldViewBase를 상속해 만들고,
    /// System Action 구독은 그 유닛 안에서 한다. WorldManager는 유닛의 호스트/레지스트리이자
    /// 공통 의존성(SystemManager, AssetProvider) 공급자다.
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
        /// GameManager.OnBootCompleted에서 호출. 이 시점에 싱글턴을 지정하고(조립 루트가 수명을 통제),
        /// Initialize 이전(씬 로드)에 등록을 시도해 대기 중이던 유닛을 흡수·바인딩한다.
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
        /// 유닛이 스스로 호출(자가 등록). 매니저 Initialize 전이면 대기 큐에 담아 유실을 막고,
        /// 초기화 이후면 즉시 바인딩한다.
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

        /// <summary>유닛이 스스로 호출(등록 해제).</summary>
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
            view.Inject(m_systemManager, m_entityManager, m_assetProvider);
            view.Bind();   // 유닛이 자기 System Action을 구독하는 지점
        }
    }
}
