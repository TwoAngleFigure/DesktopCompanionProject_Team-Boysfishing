using UnityEngine;
using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// uGUI View 유닛의 공통 베이스(MVVM의 View). 수명·의존성 주입은 UIManager가 담당한다.
    ///
    /// 흐름: OnEnable → UIManager.Register(this) → Inject → Bind
    ///       OnDisable → Unbind → UIManager.Unregister(this)
    /// </summary>
    public abstract class UIViewBase : MonoBehaviour
    {
        protected SystemManager SystemManager { get; private set; }
        protected EntityManager EntityManager { get; private set; }   // EntityHandle → Entity 조회
        protected AssetProvider AssetProvider { get; private set; }

        protected virtual void OnEnable() => UIManager.Register(this);

        protected virtual void OnDisable()
        {
            Unbind();
            UIManager.Unregister(this);
        }

        // UIManager가 바인딩 직전에 호출(같은 어셈블리 내부 전용).
        internal void Inject(SystemManager systemManager, EntityManager entityManager, AssetProvider assetProvider)
        {
            SystemManager = systemManager;
            EntityManager = entityManager;
            AssetProvider = assetProvider;
        }

        /// <summary>ViewModel을 생성·주입·Bind하고 uGUI 위젯과 VM의 바인딩을 연결한다.</summary>
        public abstract void Bind();

        /// <summary>위젯 연결을 해제하고 VM.Unbind()를 호출한다. OnDisable에서 호출된다.</summary>
        public abstract void Unbind();
    }
}
