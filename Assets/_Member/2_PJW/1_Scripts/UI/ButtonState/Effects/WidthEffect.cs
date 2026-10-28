using System;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 상태별 가로 폭(px)을 보간한다. 탭 책갈피가 hover에서 늘어나는 연출이 이것이다.
    ///
    /// ★부모에 LayoutGroup이나 ContentSizeFitter가 있으면 폭이 그쪽에 의해 결정(driven)되어
    /// 이 값이 무시된다. 그 경우 부모의 자동 배치를 끄거나 대상에 LayoutElement를 써야 한다.
    /// </summary>
    [Serializable]
    public class WidthEffect : FloatStateEffect
    {
        [Tooltip("폭을 바꿀 대상. 보통 버튼 자신")]
        [SerializeField] private RectTransform m_target;

        protected override void SetValue(float value)
        {
            if (m_target == null)
            {
                return;
            }
            m_target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, value);
        }
    }
}
