using System;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 상태별로 그림을 갈아 끼운다. 이산값이라 보간하지 않고 기준점에서 한 번에 바뀐다.
    ///
    /// 오버·켜짐 칸을 비우면 대기 그림을 쓴다 — 세 장을 다 준비하지 않아도 동작한다.
    /// </summary>
    [Serializable]
    public class SpriteEffect : ButtonStateEffect
    {
        [Tooltip("그림을 바꿀 대상")]
        [SerializeField] private Image m_target;

        [SerializeField] private Sprite m_normal;

        [Tooltip("비우면 대기 그림을 쓴다")]
        [SerializeField] private Sprite m_hover;

        [Tooltip("비우면 대기 그림을 쓴다")]
        [SerializeField] private Sprite m_active;

        [Tooltip("전이가 이 지점을 넘으면 다음 상태의 그림으로 바꾼다. 0이면 즉시")]
        [Range(0f, 1f)]
        [SerializeField] private float m_switchAt;

        public override void Apply(ButtonVisualState from, ButtonVisualState to, float t)
        {
            if (m_target == null)
            {
                return;
            }
            m_target.sprite = SpriteOf(t > m_switchAt ? to : from);
        }

        private Sprite SpriteOf(ButtonVisualState state) => state switch
        {
            ButtonVisualState.Hover => m_hover != null ? m_hover : m_normal,
            ButtonVisualState.Active => m_active != null ? m_active : m_normal,
            _ => m_normal,
        };
    }
}
