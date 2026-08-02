using System;
using System.Collections.Generic;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// ViewModel이 표시용 상태를 담는 관찰 가능한 값. Value가 실제로 바뀔 때만 OnChanged를 발행한다.
    /// </summary>
    public class BindableProperty<T>
    {
        private T m_value;
        public event Action<T> OnChanged;

        public BindableProperty(T initial = default) => m_value = initial;

        public T Value
        {
            get => m_value;
            set
            {
                // EqualityComparer<T>.Default: 값 타입(int/float/enum 등) 비교 시 박싱 없음.
                if (EqualityComparer<T>.Default.Equals(m_value, value)) return;
                m_value = value;
                OnChanged?.Invoke(m_value);
            }
        }

        /// <summary>구독을 등록하고 현재값을 1회 즉시 전달한다.</summary>
        public void Bind(Action<T> onChanged)
        {
            OnChanged += onChanged;
            onChanged(m_value);
        }

        public void Unbind(Action<T> onChanged) => OnChanged -= onChanged;
    }
}
