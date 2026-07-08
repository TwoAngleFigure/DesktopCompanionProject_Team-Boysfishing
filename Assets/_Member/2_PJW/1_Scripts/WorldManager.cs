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

        private SystemManager m_systemManager;
        private AssetProvider m_assetProvider;
        private bool m_initialized;

        private readonly List<WorldViewBase> m_views = new();

        private void Awake() => s_instance = this;

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>GameManager.OnBootCompleted에서 호출(의존성 주입 + 대기 유닛 일괄 바인딩).</summary>
        public void Initialize(SystemManager systemManager, AssetProvider assetProvider)
        {
            m_systemManager = systemManager;
            m_assetProvider = assetProvider;
            m_initialized = true;

            // 이미 등록되어 대기 중이던 유닛들을 일괄 바인딩.
            for (int i = 0; i < m_views.Count; i++)
            {
                BindView(m_views[i]);
            }
        }

        /// <summary>유닛이 스스로 호출(자가 등록). 초기화 이후 등록된 유닛은 즉시 바인딩된다.</summary>
        public static void Register(WorldViewBase view)
        {
            if (s_instance == null || view == null)
            {
                return;   // 매니저 부재 시 방어
            }

            if (s_instance.m_views.Contains(view))
            {
                return;
            }

            s_instance.m_views.Add(view);

            if (s_instance.m_initialized)
            {
                s_instance.BindView(view);   // 늦게 온 유닛 즉시 바인딩
            }
        }

        /// <summary>유닛이 스스로 호출(등록 해제).</summary>
        public static void Unregister(WorldViewBase view)
        {
            if (s_instance == null || view == null)
            {
                return;
            }

            s_instance.m_views.Remove(view);
        }

        private void BindView(WorldViewBase view)
        {
            view.Inject(m_systemManager, m_assetProvider);
            view.Bind();   // 유닛이 자기 System Action을 구독하는 지점
        }
    }
}
