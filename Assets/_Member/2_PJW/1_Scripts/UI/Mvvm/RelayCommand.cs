using System;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 실행 가능한 사용자 행동(View→System 명령의 최소 단위).
    /// View 위젯이 System을 직접 부르지 않고 이 명령에 바인딩한다.
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

    /// <summary>인자 있는 명령(예: 슬롯 인덱스 (from, to)).</summary>
    public class RelayCommand<TArg>
    {
        private readonly Action<TArg> m_execute;

        public RelayCommand(Action<TArg> execute) => m_execute = execute;

        public void Execute(TArg arg) => m_execute?.Invoke(arg);
    }
}
