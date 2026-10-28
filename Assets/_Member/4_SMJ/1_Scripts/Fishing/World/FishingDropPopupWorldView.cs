using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FishingDropPopupWorldView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject m_popupRoot;
    [SerializeField] private RectTransform m_popupRect;
    [SerializeField] private CanvasGroup m_canvasGroup;
    [SerializeField] private Image m_itemIcon;
    [SerializeField] private TMP_Text m_countText;

    [Header("Animation")]
    [SerializeField] private float m_holdDuration = 0.2f;
    [SerializeField] private float m_moveDuration = 0.8f;
    [SerializeField] private float m_moveDistance = 50f;

    private Sequence m_sequence;
    private Vector2 m_startPosition;

    private void Awake()
    {
        if (m_popupRect != null)
        {
            m_startPosition = m_popupRect.anchoredPosition;
        }

        if (m_popupRoot != null)
        {
            m_popupRoot.SetActive(false);
        }
    }

    public void Show(Sprite icon, int count)
    {
        if (icon == null || count <= 0)
        {
            return;
        }

        if (m_popupRoot == null ||
            m_popupRect == null ||
            m_canvasGroup == null ||
            m_itemIcon == null ||
            m_countText == null)
        {
            Debug.LogWarning("[FishingDropPopupWorldView] 팝업 참조가 설정되지 않았습니다.");
            return;
        }

        m_sequence?.Kill();
        m_sequence = null;

        m_itemIcon.sprite = icon;
        m_itemIcon.enabled = true;
        m_countText.text = $"+{count}";

        m_popupRect.anchoredPosition = m_startPosition;
        m_canvasGroup.alpha = 1f;
        m_popupRoot.SetActive(true);

        m_sequence = DOTween.Sequence();

        m_sequence.AppendInterval(m_holdDuration);

        m_sequence
            .Append(
                m_popupRect
                    .DOAnchorPosY(
                        m_startPosition.y + m_moveDistance,
                        m_moveDuration)
                    .SetEase(Ease.OutCubic))
            .Join(
                m_canvasGroup
                    .DOFade(0f, m_moveDuration)
                    .SetEase(Ease.InQuad));

        m_sequence
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                m_popupRect.anchoredPosition = m_startPosition;
                m_canvasGroup.alpha = 0f;
                m_popupRoot.SetActive(false);
                m_sequence = null;
            });
    }

    public void Cleanup()
    {
        m_sequence?.Kill();
        m_sequence = null;

        if (m_popupRect != null)
        {
            m_popupRect.anchoredPosition = m_startPosition;
        }

        if (m_canvasGroup != null)
        {
            m_canvasGroup.alpha = 0f;
        }

        if (m_popupRoot != null)
        {
            m_popupRoot.SetActive(false);
        }
    }

    private void OnDisable()
    {
        m_sequence?.Kill();
        m_sequence = null;
    }
}