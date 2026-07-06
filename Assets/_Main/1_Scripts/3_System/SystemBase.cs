using DesktopCompanion.Core;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// System 구현의 베이스. EntityManager와 SystemManager를 주입받아
    /// 상속 클래스가 protected 프로퍼티로 바로 쓰게 한다.
    /// - Entity 생성/조회/소멸: <see cref="EntityManager"/>
    /// - 다른 System 참조(D11): <see cref="SystemManager"/>.GetSystem&lt;T&gt;()
    /// 팀원은 이 클래스를 상속해 각자의 System(예: ItemInventorySystem)을 작성한다.
    /// </summary>
    public abstract class SystemBase : ISystem
    {
        protected EntityManager EntityManager { get; private set; }
        protected DataManager DataManager { get; private set; }     // 정의 직접 조회용(GetData/GetAll)
        protected SystemManager SystemManager { get; private set; }

        // SystemManager.Register 시 호출된다(같은 어셈블리 내부 전용).
        internal void Inject(EntityManager entityManager, DataManager dataManager, SystemManager systemManager)
        {
            EntityManager = entityManager;
            DataManager = dataManager;
            SystemManager = systemManager;
        }

        /// <summary>기본은 no-op. 초기화가 필요하면 override 한다.</summary>
        public virtual void Initialize() { }
    }
}
