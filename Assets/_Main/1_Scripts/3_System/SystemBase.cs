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

        /// <summary>
        /// Phase 1(생산). 자기 자신의 런타임 상태만 구성한다.
        /// 이 단계에서는 다른 System의 런타임 상태를 읽지 않는다(아직 준비 전일 수 있음).
        /// 기본은 no-op. 필요하면 override 한다.
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// Phase 2(소비·배선). 모든 System이 Phase 1을 마쳐 자기 상태가 존재하는 시점.
        /// 다른 System 조회(SystemManager.GetSystem&lt;T&gt;())·이벤트 구독처럼
        /// "다른 System이 준비된 뒤에 해야 하는 초기화"를 여기서 한다.
        /// 이렇게 나누면 생산→소비 의존이 등록 순서와 무관하게 안전해진다(상호 의존 포함).
        /// 기본은 no-op. 필요하면 override 한다.
        /// </summary>
        public virtual void PostInitialize() { }
    }
}
