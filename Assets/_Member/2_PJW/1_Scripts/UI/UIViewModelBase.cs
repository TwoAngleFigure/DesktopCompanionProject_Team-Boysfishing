using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// MVVM의 ViewModel 베이스(plain C#).
    /// System Action 구독으로 표시용 상태(BindableProperty)를 갱신하고,
    /// 사용자 명령(RelayCommand)을 System의 public 메서드 호출로 변환한다.
    /// 크로스 참조는 SystemManager.GetSystem&lt;T&gt;()(D11)로만.
    /// </summary>
    public abstract class UIViewModelBase
    {
        protected SystemManager SystemManager { get; private set; }

        // View가 자기 VM에 주입한다(같은 어셈블리 내부 전용).
        internal void Inject(SystemManager systemManager) => SystemManager = systemManager;

        /// <summary>System Action 구독(System→View 상태 갱신)을 여기서.</summary>
        public abstract void Bind();

        /// <summary>구독 해제(Bind와 1:1 대칭).</summary>
        public abstract void Unbind();
    }
}
