using System;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 버튼의 상태 하나를 표현으로 옮기는 규칙.
    ///
    /// **진행 상태를 갖지 않는다.** 어디서 어디로 얼마나 왔는지는 전부 인자로 온다 —
    /// 버튼은 언제나 하나의 상태에 있고 전이도 하나뿐이므로, 그 한 벌은
    /// <see cref="ButtonStateOwner"/>가 갖는다. 덕분에 중단·역전 처리가 이쪽에 없다.
    ///
    /// 새 효과는 이 클래스(또는 <see cref="FloatStateEffect"/>)를 상속하기만 하면 된다.
    /// 등록할 열거형도 팩토리도 없다 — Unity가 하위 클래스를 인스펙터 드롭다운에 자동으로 채운다.
    /// </summary>
    [Serializable]
    public abstract class ButtonStateEffect
    {
        /// <summary>
        /// 소유자가 <c>Awake</c>에서 한 번 부른다. 참조 캐싱이 필요한 효과만 재정의한다.
        /// </summary>
        public virtual void Initialize(ButtonStateOwner owner) { }

        /// <param name="from">전이 출발 상태</param>
        /// <param name="to">전이 도착 상태</param>
        /// <param name="t">진행도 0~1. 1이면 <paramref name="to"/>의 최종 모습이다</param>
        public abstract void Apply(ButtonVisualState from, ButtonVisualState to, float t);
    }

    /// <summary>
    /// 상태별 숫자 하나를 보간하는 효과의 공통 뼈대.
    ///
    /// 새 숫자 효과는 <see cref="SetValue"/> 하나만 구현하면 된다 —
    /// 상태별 값 3개, 곡선, 보간 계산은 여기가 이미 갖고 있다.
    /// </summary>
    [Serializable]
    public abstract class FloatStateEffect : ButtonStateEffect
    {
        [Tooltip("대기 상태의 값")]
        [SerializeField] private float m_normal;

        [Tooltip("마우스가 올라왔을 때의 값")]
        [SerializeField] private float m_hover;

        [Tooltip("켜졌을 때의 값")]
        [SerializeField] private float m_active;

        [Tooltip("비우면 선형. 튕기거나 늦게 붙는 느낌을 주고 싶을 때만 채운다")]
        [SerializeField] private AnimationCurve m_shape;

        public sealed override void Apply(ButtonVisualState from, ButtonVisualState to, float t)
        {
            float shaped = (m_shape != null && m_shape.length > 0) ? m_shape.Evaluate(t) : t;

            // LerpUnclamped: 곡선이 0~1을 넘어가는 오버슈트를 살린다.
            SetValue(Mathf.LerpUnclamped(ValueOf(from), ValueOf(to), shaped));
        }

        /// <summary>보간된 값을 실제 대상에 쓴다.</summary>
        protected abstract void SetValue(float value);

        private float ValueOf(ButtonVisualState state) => state switch
        {
            ButtonVisualState.Hover => m_hover,
            ButtonVisualState.Active => m_active,
            _ => m_normal,
        };
    }
}
