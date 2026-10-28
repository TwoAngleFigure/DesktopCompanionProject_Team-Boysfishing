using UnityEngine;
using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 월드 View 유닛의 공통 베이스. 파생 클래스는 Bind()에서 자기 System의 Action을 구독한다.
    /// 수명·의존성 주입은 WorldManager가 담당한다.
    ///
    /// 흐름: OnEnable → WorldManager.Register(this) → Inject → Bind
    ///       OnDisable → Unbind → WorldManager.Unregister(this)
    /// </summary>
    public abstract class WorldViewBase : MonoBehaviour
    {
        protected SystemManager SystemManager { get; private set; }
        protected EntityManager EntityManager { get; private set; }   // EntityHandle → Entity 조회
        protected AssetProvider AssetProvider { get; private set; }

        // 씬 배치 유닛은 스스로 매니저에 등록한다.
        protected virtual void OnEnable() => WorldManager.Register(this);

        protected virtual void OnDisable()
        {
            Unbind();
            WorldManager.Unregister(this);
        }

        // WorldManager가 바인딩 직전에 호출(같은 어셈블리 내부 전용).
        internal void Inject(SystemManager systemManager, EntityManager entityManager, AssetProvider assetProvider)
        {
            SystemManager = systemManager;
            EntityManager = entityManager;
            AssetProvider = assetProvider;
        }

        /// <summary>의존성 주입 후 호출된다. System Action 구독(+=)을 수행한다.</summary>
        public abstract void Bind();

        /// <summary>구독을 해제한다(-=). Bind와 1:1 대칭이며 OnDisable에서 호출된다.</summary>
        public abstract void Unbind();
    }
}
