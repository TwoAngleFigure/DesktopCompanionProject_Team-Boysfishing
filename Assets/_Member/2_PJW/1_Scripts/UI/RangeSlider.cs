using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 핸들 두 개로 [Low, High] 구간을 고르는 슬라이더. uGUI Slider는 값 하나만 다뤄 직접 구현한다.
    ///
    /// 계층 전제:
    ///  - Fill·핸들 두 개는 모두 TrackArea의 자식이어야 한다. 값을 앵커 x로 환산해 배치하므로
    ///    부모가 다르면 좌표가 어긋난다.
    ///  - 포인터를 받으려면 이 컴포넌트가 붙은 오브젝트(또는 그 자식)에 Raycast Target인 Graphic이 있어야 한다.
    ///    보통 트랙 배경 Image가 그 역할을 한다.
    ///
    /// 입력은 트랙 전체에서 받고 누른 지점에서 가까운 핸들을 잡는다. 핸들에 개별 드래그 처리를 두지 않아
    /// 프리팹 구성이 단순하고, 핸들 밖(트랙 여백)을 눌러도 구간이 따라온다.
    /// </summary>
    public class RangeSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [Header("Refs")]
        [Tooltip("핸들이 움직이는 기준 영역. 이 rect의 가로 폭이 0~1에 대응한다")]
        [SerializeField] private RectTransform m_trackArea;
        [Tooltip("선택 구간을 칠하는 rect. 없어도 동작한다")]
        [SerializeField] private RectTransform m_fill;
        [SerializeField] private RectTransform m_lowHandle;
        [SerializeField] private RectTransform m_highHandle;

        [Header("Range")]
        [Tooltip("두 핸들 사이의 최소 간격(0~1). 컨트롤러 쪽 최소 폭과 맞춰 둔다")]
        [SerializeField, Range(0f, 0.5f)] private float m_minWidth = 0.05f;
        [SerializeField, Range(0f, 1f)] private float m_low = 0f;
        [SerializeField, Range(0f, 1f)] private float m_high = 1f;

        [Header("비활성 표시")]
        [Tooltip("비활성 시 알파를 낮출 그룹. 없으면 입력만 막는다")]
        [SerializeField] private CanvasGroup m_group;
        [SerializeField, Range(0f, 1f)] private float m_disabledAlpha = 0.4f;
        [SerializeField] private bool m_interactable = true;

        /// <summary>구간이 바뀔 때마다 (low, high)를 방송한다. 드래그 중 매 프레임 발생한다.</summary>
        public event Action<float, float> OnValueChanged;

        public float LowValue => m_low;
        public float HighValue => m_high;

        public bool Interactable
        {
            get => m_interactable;
            set
            {
                m_interactable = value;
                RefreshInteractable();
            }
        }

        private bool m_draggingLow;

        private void OnEnable()
        {
            UpdateVisuals();
            RefreshInteractable();
        }

        private void OnValidate()
        {
            if (m_high < m_low)
            {
                (m_low, m_high) = (m_high, m_low);
            }
            UpdateVisuals();
            RefreshInteractable();
        }

        /// <summary>알림 없이 구간을 반영한다. 컨트롤러가 보정한 결과를 되돌릴 때 쓴다.</summary>
        public void SetValuesWithoutNotify(float low, float high) => SetValues(low, high, notify: false);

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_interactable == false || TryGetValue(eventData, out float value) == false)
            {
                return;
            }

            // 같은 거리면 low를 잡는다. 구간 바깥을 눌렀을 때 가까운 쪽 경계가 따라오는 게 자연스럽다.
            m_draggingLow = Mathf.Abs(value - m_low) <= Mathf.Abs(value - m_high);
            ApplyDrag(value);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_interactable == false || TryGetValue(eventData, out float value) == false)
            {
                return;
            }
            ApplyDrag(value);
        }

        // 잡고 있는 핸들만 움직인다. 반대쪽 핸들은 최소 간격만큼 떨어진 지점에서 멈춘다.
        private void ApplyDrag(float value)
        {
            if (m_draggingLow)
            {
                SetValues(Mathf.Min(value, m_high - m_minWidth), m_high, notify: true);
            }
            else
            {
                SetValues(m_low, Mathf.Max(value, m_low + m_minWidth), notify: true);
            }
        }

        private void SetValues(float low, float high, bool notify)
        {
            low = Mathf.Clamp01(low);
            high = Mathf.Clamp01(high);
            if (high < low)
            {
                (low, high) = (high, low);
            }

            bool changed = Mathf.Approximately(low, m_low) == false || Mathf.Approximately(high, m_high) == false;
            m_low = low;
            m_high = high;
            UpdateVisuals();

            if (notify && changed)
            {
                OnValueChanged?.Invoke(m_low, m_high);
            }
        }

        // 포인터 위치를 트랙 기준 0~1로 환산한다.
        private bool TryGetValue(PointerEventData eventData, out float value)
        {
            value = 0f;
            if (m_trackArea == null)
            {
                return false;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_trackArea, eventData.position, eventData.pressEventCamera, out Vector2 local) == false)
            {
                return false;
            }

            Rect rect = m_trackArea.rect;
            if (rect.width <= 0f)
            {
                return false;
            }

            value = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            return true;
        }

        // 값을 앵커 x로 옮긴다. 앵커로 두면 트랙 폭이 바뀌어도 위치가 따라온다.
        private void UpdateVisuals()
        {
            SetHandleAnchor(m_lowHandle, m_low);
            SetHandleAnchor(m_highHandle, m_high);
            SetFillAnchors(m_fill, m_low, m_high);
        }

        // 핸들은 점 앵커를 옮기고 가로 오프셋만 0으로 맞춘다.
        // Fill처럼 offsetMin/Max를 0으로 눕히면 앵커 간격이 0이라 폭까지 0이 된다.
        private static void SetHandleAnchor(RectTransform rect, float x)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(x, rect.anchorMin.y);
            rect.anchorMax = new Vector2(x, rect.anchorMax.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        }

        // Fill은 두 값 사이를 늘린다. 세로 배치(높이·여백)는 프리팹에서 잡은 값을 유지한다.
        private static void SetFillAnchors(RectTransform rect, float minX, float maxX)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(minX, rect.anchorMin.y);
            rect.anchorMax = new Vector2(maxX, rect.anchorMax.y);
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
        }

        private void RefreshInteractable()
        {
            if (m_group != null)
            {
                m_group.alpha = m_interactable ? 1f : m_disabledAlpha;
            }
        }
    }
}
