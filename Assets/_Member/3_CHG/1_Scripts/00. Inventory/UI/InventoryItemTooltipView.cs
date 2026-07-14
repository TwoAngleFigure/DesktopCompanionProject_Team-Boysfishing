using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class InventoryItemTooltipView : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas m_canvas;
        [SerializeField] private RectTransform m_tooltipRoot;
        [SerializeField] private CanvasGroup m_canvasGroup;

        [Header("Item Info")]
        [SerializeField] private Image m_iconImage;
        [SerializeField] private TMP_Text m_itemNameText;
        [SerializeField] private TMP_Text m_gradeText;
        [SerializeField] private TMP_Text m_effectText;
        [SerializeField] private TMP_Text m_sellPriceText;

        [Header("Position")]
        [SerializeField] private Vector2 m_pointerOffset = new(18f, -18f);

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
            if (!m_isVisible)
            {
                return;
            }

            FollowPointer();
        }

        public void Show(InventorySlotViewData data, Sprite icon)
        {
            if (data == null || data.IsEmpty || m_tooltipRoot == null)
            {
                Hide();
                return;
            }

            if (m_iconImage != null)
            {
                m_iconImage.sprite = icon;
                m_iconImage.enabled = icon != null;
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

            if (m_sellPriceText != null)
            {
                m_sellPriceText.text = data.SellPriceText;
            }

            m_tooltipRoot.gameObject.SetActive(true);

            // Canvas 안에서 다른 UI보다 위에 그려지도록
            m_tooltipRoot.SetAsLastSibling();

            m_isVisible = true;

            // Layout Group과 Content Size Fitter의 크기 즉시 갱신
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_tooltipRoot);

            FollowPointer();
        }

        public void Hide()
        {
            m_isVisible = false;

            if (m_iconImage != null)
            {
                m_iconImage.sprite = null;
                m_iconImage.enabled = false;
            }

            if (m_tooltipRoot != null)
            {
                m_tooltipRoot.gameObject.SetActive(false);
            }
        }

        private void FollowPointer()
        {
            if (Mouse.current == null || m_canvas == null || m_canvasRect == null || m_tooltipRoot == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();

            Camera eventCamera = m_canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : m_canvas.worldCamera;

            bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(m_canvasRect, screenPosition, eventCamera, out Vector2 localPosition);

            if (!converted)
            {
                return;
            }

            Vector2 targetPosition = localPosition + m_pointerOffset;

            m_tooltipRoot.anchoredPosition = ClampInsideCanvas(targetPosition);
        }

        private Vector2 ClampInsideCanvas(Vector2 position)
        {
            Rect canvasBounds = m_canvasRect.rect;
            Rect tooltipBounds = m_tooltipRoot.rect;
            Vector2 pivot = m_tooltipRoot.pivot;

            float minX = canvasBounds.xMin + tooltipBounds.width * pivot.x;
            float maxX = canvasBounds.xMax - tooltipBounds.width * (1f - pivot.x);

            float minY = canvasBounds.yMin + tooltipBounds.height * pivot.y;
            float maxY = canvasBounds.yMax - tooltipBounds.height * (1f - pivot.y);

            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = Mathf.Clamp(position.y, minY, maxY);

            return position;
        }
    }
}