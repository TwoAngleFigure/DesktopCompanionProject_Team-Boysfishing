using System;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 마스크 영역 안에서 내용물을 밀어 올린다. 버튼이 켜지면 뒤에서 무언가 올라오는 연출이다.
    ///
    /// 상태별 값은 <b>얼마나 올라와 있는가를 0~1</b>로 적는다(0=완전히 숨음, 1=완전히 올라옴).
    /// 실제 픽셀은 마스크 높이에서 계산하므로, 마스크 크기를 바꿔도 값이 따라오고
    /// 내용물이 어중간하게 걸쳐 보이는 사고가 없다.
    ///
    /// 마스크는 RectMask2D를 쓴다 — Mask와 달리 스텐실을 쓰지 않아 배치 분리 비용이 없고,
    /// 사각 잘라내기만 필요한 이 용도에 맞다.
    /// ★내용물이 버튼 클릭을 가리지 않도록 마스크 쪽 Raycast Target은 꺼 둔다.
    /// </summary>
    [Serializable]
    public class RevealEffect : FloatStateEffect
    {
        [Tooltip("RectMask2D가 붙은 잘라내기 영역")]
        [SerializeField] private RectTransform m_viewport;

        [Tooltip("영역 안에서 오르내릴 내용물")]
        [SerializeField] private RectTransform m_content;

        [Tooltip("완전히 올라왔을 때의 anchoredPosition.y. 보통 0")]
        [SerializeField] private float m_shownY;

        protected override void SetValue(float value)
        {
            if (m_viewport == null || m_content == null)
            {
                return;
            }

            // 내용물이 마스크보다 크면 그만큼 더 내려야 완전히 숨는다.
            float travel = Mathf.Max(m_viewport.rect.height, m_content.rect.height);
            float hiddenY = m_shownY - travel;

            Vector2 position = m_content.anchoredPosition;
            position.y = Mathf.LerpUnclamped(hiddenY, m_shownY, value);
            m_content.anchoredPosition = position;
        }
    }
}
