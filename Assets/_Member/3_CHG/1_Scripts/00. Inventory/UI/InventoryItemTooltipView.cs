using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class InventoryItemTooltipView : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas m_canvas;
        [SerializeField] private RectTransform m_tooltipRoot;
        [SerializeField] private CanvasGroup m_canvasGroup;

        [Header("Contents")]
        [SerializeField] private Image m_itemIcon; 
        [SerializeField] private TMP_Text m_itemNameText;
        [SerializeField] private TMP_Text m_gradeText;
        [SerializeField] private TMP_Text m_effectText;

        [Header("Position")]
        [SerializeField] private float m_horizontalGap = 0f;

        private RectTransform m_targetSlot;

        // GetWorldCorners 호출 시 매번 배열을 생성하지 않기 위한 버퍼
        private readonly Vector3[] m_slotCorners = new Vector3[4];
        private readonly Vector3[] m_tooltipCorners = new Vector3[4];

        private RectTransform m_canvasRect;
        private bool m_isVisible;

        private void Awake()
        {
            if (m_canvas == null)
            {
                m_canvas = GetComponentInParent<Canvas>();
            }

            if (m_tooltipRoot == null)
            {
                m_tooltipRoot = transform as RectTransform;
            }

            if (m_canvasGroup == null)
            {
                m_canvasGroup = GetComponent<CanvasGroup>();
            }

            if (m_canvas != null)
            {
                m_canvasRect = m_canvas.transform as RectTransform;
            }

            if (m_canvasGroup != null)
            {
                m_canvasGroup.interactable = false;
                m_canvasGroup.blocksRaycasts = false;
            }

            Hide();
        }

        private void Update()
        {
            if (!m_isVisible || m_targetSlot == null)
            {
                return;
            }

            PositionBesideSlot(m_targetSlot);
        }

        public void Show(InventorySlotViewData data, Sprite icon, RectTransform slotRect)
        {
            if (data == null
                || data.IsEmpty
                || slotRect == null
                || m_tooltipRoot == null)
            {
                Hide();
                return;
            }

            if (m_itemNameText != null)
            {
                m_itemNameText.text = data.ItemName;
            }

            if (m_gradeText != null)
            {
                m_gradeText.text = data.GradeText;
            }

            if (m_effectText != null)
            {
                m_effectText.text = data.EffectText;
            }

            if(m_itemIcon != null)
            {
                m_itemIcon.sprite = icon;
                m_itemIcon.enabled = icon != null;
            }

            if (!m_tooltipRoot.gameObject.activeSelf)
            {
                m_tooltipRoot.gameObject.SetActive(true);
            }

            // 다른 UI보다 위에 표시
            m_tooltipRoot.SetAsLastSibling();

            m_targetSlot = slotRect;
            m_isVisible = true;

            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 1f;
            }

            // 텍스트에 따라 Tooltip 높이가 바뀌므로 위치 계산 전에 갱신
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_tooltipRoot);
            Canvas.ForceUpdateCanvases();

            PositionBesideSlot(slotRect);
        }

        public void Hide()
        {
            m_isVisible = false;
            m_targetSlot = null;

            if (m_canvasGroup != null)
            {
                m_canvasGroup.alpha = 0f;
            }

            // 이전 아이템 Sprite가 남지 않도록 초기화
            if (m_itemIcon != null)
            {
                m_itemIcon.sprite = null;
                m_itemIcon.enabled = false;
            }
        }

        /// <summary>
        /// 슬롯 옆에 툴팁을 띄우기 위한 포지션 함수
        /// </summary>
        private void PositionBesideSlot(RectTransform slotRect)
        {
            if (slotRect == null
                || m_canvasRect == null
                || m_tooltipRoot == null)
            {
                return;
            }

            // 슬롯의 네 모서리를 World 좌표로 변환
            // 0 : 왼쪽 아래
            // 1 : 왼쪽 위
            // 2 : 오른쪽 위
            // 3 : 오른쪽 아래
            slotRect.GetWorldCorners(m_slotCorners);

            Vector2 slotTopLeft =
                m_canvasRect.InverseTransformPoint(m_slotCorners[1]);

            Vector2 slotTopRight =
                m_canvasRect.InverseTransformPoint(m_slotCorners[2]);

            // 기본 배치 : 슬롯 오른쪽 위 <-> Tooltip 왼쪽 위
            m_tooltipRoot.pivot = new Vector2(0f, 1f);

            SetTooltipPivotPosition(
                slotTopRight + Vector2.right * m_horizontalGap);

            // 오른쪽으로 Canvas를 벗어나면 반대편으로 전환
            if (IsOverflowingRight())
            {
                // 반대 배치 : 슬롯 왼쪽 위 <-> Tooltip 오른쪽 위
                m_tooltipRoot.pivot = new Vector2(1f, 1f);

                SetTooltipPivotPosition(
                    slotTopLeft + Vector2.left * m_horizontalGap);
            }

            // Tooltip이 너무 크거나 슬롯이 화면 가장자리에 있는 경우를 위한 최종 보정
            ClampTooltipInsideCanvas();
        }

        private void SetTooltipPivotPosition(Vector2 canvasLocalPosition)
        {
            Vector3 worldPosition = m_canvasRect.TransformPoint(
                new Vector3(canvasLocalPosition.x, canvasLocalPosition.y, 0f));

            m_tooltipRoot.position = worldPosition;
        }

        private bool IsOverflowingRight()
        {
            m_tooltipRoot.GetWorldCorners(m_tooltipCorners);

            Vector2 tooltipTopRight =
                m_canvasRect.InverseTransformPoint(m_tooltipCorners[2]);

            return tooltipTopRight.x > m_canvasRect.rect.xMax;
        }

        /// <summary>
        /// 툴팁 위치 최종 보정
        /// </summary>
        private void ClampTooltipInsideCanvas()
        {
            m_tooltipRoot.GetWorldCorners(m_tooltipCorners);

            Vector2 tooltipBottomLeft =
                m_canvasRect.InverseTransformPoint(m_tooltipCorners[0]);

            Vector2 tooltipTopRight =
                m_canvasRect.InverseTransformPoint(m_tooltipCorners[2]);

            Rect canvasBounds = m_canvasRect.rect;
            Vector2 correction = Vector2.zero;

            if (tooltipBottomLeft.x < canvasBounds.xMin)
            {
                correction.x = canvasBounds.xMin - tooltipBottomLeft.x;
            }
            else if (tooltipTopRight.x > canvasBounds.xMax)
            {
                correction.x = canvasBounds.xMax - tooltipTopRight.x;
            }

            if (tooltipBottomLeft.y < canvasBounds.yMin)
            {
                correction.y = canvasBounds.yMin - tooltipBottomLeft.y;
            }
            else if (tooltipTopRight.y > canvasBounds.yMax)
            {
                correction.y = canvasBounds.yMax - tooltipTopRight.y;
            }

            Vector3 worldCorrection = m_canvasRect.TransformVector(
                new Vector3(correction.x, correction.y, 0f));

            m_tooltipRoot.position += worldCorrection;
        }
    }
}