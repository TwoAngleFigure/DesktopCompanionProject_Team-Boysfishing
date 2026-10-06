using System;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// View 위젯이 바인딩하는 사용자 명령. 실행 동작과 실행 가능 조건을 함께 보유한다.
    /// </summary>
    public class RelayCommand
    {
        private readonly Action m_execute;
        private readonly Func<bool> m_canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            m_execute = execute;
            m_canExecute = canExecute;
        }

        public bool CanExecute => m_canExecute == null || m_canExecute();

        public void Execute()
        {
            if (CanExecute) m_execute?.Invoke();
        }
    }

    /// <summary>인자를 받는 사용자 명령.</summary>
    public class RelayCommand<TArg>
    {
        private readonly Action<TArg> m_execute;

        public RelayCommand(Action<TArg> execute) => m_execute = execute;

        public void Execute(TArg arg) => m_execute?.Invoke(arg);
    }
}
