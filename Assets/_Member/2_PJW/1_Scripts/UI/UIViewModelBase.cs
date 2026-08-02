using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// MVVM의 ViewModel 베이스(plain C#).
    /// System Action 구독으로 표시용 상태(BindableProperty)를 갱신하고,
    /// 사용자 명령(RelayCommand)을 System의 public 메서드 호출로 변환한다.
    /// 타 System 참조는 SystemManager.GetSystem&lt;T&gt;()로 얻는다.
    /// </summary>
    public abstract class UIViewModelBase
    {
        protected SystemManager SystemManager { get; private set; }

        // View가 자기 VM에 주입한다(같은 어셈블리 내부 전용).
        protected EntityManager EntityManager { get; private set; }   // EntityHandle → Entity 조회

        internal void Inject(SystemManager systemManager, EntityManager entityManager)
        {
            SystemManager = systemManager;
            EntityManager = entityManager;
        }

        /// <summary>System Action을 구독해 표시용 상태 갱신 경로를 연결한다.</summary>
        public abstract void Bind();

        /// <summary>구독을 해제한다(Bind와 1:1 대칭).</summary>
        public abstract void Unbind();
    }
}
