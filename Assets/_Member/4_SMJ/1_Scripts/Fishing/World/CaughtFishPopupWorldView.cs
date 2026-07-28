using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using DesktopCompanion.Views;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaughtFishPopupWorldView : MonoBehaviour
{
    [Header("Popup Model")]
    [SerializeField] private Transform m_spawnPoint;
    [SerializeField] private Transform m_modelRoot;
    [SerializeField] private Vector3 m_modelScale = Vector3.one;

    [Header("Catch Popup")]
    [SerializeField] private CanvasGroup m_popupCanvasGroup;
    [SerializeField] private GameObject m_popupRoot;
    [SerializeField] private TMP_Text m_fishInfoText;
    [SerializeField] private GameObject m_collectionUpdateBadge;
    [SerializeField] private Image[] m_qualityStars;
    [SerializeField] private float m_popupHoldDuration = 1.5f;
    [SerializeField] private float m_popupMoveDistance = 0.4f;
    [SerializeField] private float m_popupEnterDuration = 0.35f;
    [SerializeField] private float m_popupExitDuration = 0.4f;

    [Header("Pending Action")]
    [SerializeField] private RectTransform m_pendingActionAnchor;

    private GameObject m_currentModel;
    private DitherFade m_currentModelFade;
    private Sequence m_popupSequence;
    private Vector3 m_popupShownLocalPosition;

    public RectTransform PendingActionAnchor => m_pendingActionAnchor;

    private void Awake()
    {
        if (m_popupRoot != null)
        {
            m_popupShownLocalPosition = m_popupRoot.transform.localPosition;
        }
    }

    public void ShowCaughtFish(
        Entity_Fish fish,
        GameObject prefab,
        FishCollectionUpdateResult collectionResult)
    {
        if (fish == null)
        {
            return;
        }

        ClearCurrentModel();
        UpdateCatchPopup(fish, collectionResult);
        CreateCaughtFishModel(prefab);
        PlayResultPopup();
    }

    public void ShowFailure(string resultText)
    {
        ClearCurrentModel();
        SetCollectionUpdateBadge(false);
        UpdateFailurePopup(resultText);
        PlayResultPopup();
    }

    public void Cleanup()
    {
        m_popupSequence?.Kill();
        m_popupSequence = null;

        ClearCurrentModel();

        if (m_popupRoot != null)
        {
            m_popupRoot.SetActive(false);
        }
    }

    public void KeepVisibleForPending()
    {
        if (m_popupRoot == null || m_popupCanvasGroup == null)
        {
            Debug.LogWarning(
                "[CaughtFishPopupWorldView] Pending 표시를 위한 팝업 참조가 없습니다.");
            return;
        }

        m_popupSequence?.Kill();
        m_popupSequence = null;

        m_popupRoot.SetActive(true);
        m_popupRoot.transform.localPosition = m_popupShownLocalPosition;

        m_popupCanvasGroup.alpha = 1f;
        m_currentModelFade?.SetFadeImmediate(1f);
    }

    public void CloseAfterPending()
    {
        m_popupSequence?.Kill();
        m_popupSequence = null;

        ClearCurrentModel();

        if (m_popupRoot == null)
        {
            return;
        }

        m_popupRoot.transform.localPosition = m_popupShownLocalPosition;

        if (m_popupCanvasGroup != null)
        {
            m_popupCanvasGroup.alpha = 0f;
        }

        m_popupRoot.SetActive(false);
    }

    private void UpdateCatchPopup(
        Entity_Fish fish,
        FishCollectionUpdateResult collectionResult)
    {
        if (m_fishInfoText != null)
        {
            m_fishInfoText.text = $"{fish.Name} / {fish.Size:0.0} cm";
        }

        SetQualityStars((int)fish.Quality);
        SetCollectionUpdateBadge(
            collectionResult.UpdateType != FishCollectionUpdateType.None);
    }

    private void UpdateFailurePopup(string resultText)
    {
        if (m_fishInfoText != null)
        {
            m_fishInfoText.text = resultText;
        }

        SetQualityStars(0);
        SetCollectionUpdateBadge(false);
    }

    private void SetCollectionUpdateBadge(bool isVisible)
    {
        if (m_collectionUpdateBadge != null)
        {
            m_collectionUpdateBadge.SetActive(isVisible);
        }
    }

    private void SetQualityStars(int count)
    {
        if (m_qualityStars == null)
        {
            return;
        }

        int visibleCount = Mathf.Clamp(count, 0, m_qualityStars.Length);

        for (int i = 0; i < m_qualityStars.Length; i++)
        {
            if (m_qualityStars[i] != null)
            {
                m_qualityStars[i].gameObject.SetActive(i < visibleCount);
            }
        }
    }

    private void CreateCaughtFishModel(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        Transform parent = m_modelRoot != null ? m_modelRoot : transform;
        Vector3 position = m_spawnPoint != null ? m_spawnPoint.position : transform.position;
        Quaternion rotation = m_spawnPoint != null ? m_spawnPoint.rotation : transform.rotation;

        m_currentModel = Instantiate(prefab, position, rotation, parent);
        m_currentModel.transform.localScale = m_modelScale;

        m_currentModelFade = m_currentModel.GetComponent<DitherFade>();

        if (m_currentModelFade == null)
        {
            Debug.LogWarning("[CaughtFishPopupWorldView] 생성한 물고기 모델에 DitherFade가 없습니다.");
        }
    }

    private void ClearCurrentModel()
    {
        if (m_currentModel != null)
        {
            Destroy(m_currentModel);
            m_currentModel = null;
        }

        m_currentModelFade = null;
    }

    private void PlayResultPopup()
    {
        if (m_popupRoot == null || m_popupCanvasGroup == null)
        {
            ClearCurrentModel();
            Debug.LogWarning("[CaughtFishPopupWorldView] 결과 팝업 참조가 설정되지 않았습니다.");
            return;
        }

        m_popupSequence?.Kill();

        Transform popupTransform = m_popupRoot.transform;
        float hiddenY = m_popupShownLocalPosition.y - m_popupMoveDistance;

        popupTransform.localPosition = new Vector3(
            m_popupShownLocalPosition.x,
            hiddenY,
            m_popupShownLocalPosition.z);

        m_popupRoot.SetActive(true);
        m_popupCanvasGroup.alpha = 0f;
        m_currentModelFade?.SetFadeImmediate(0f);

        m_popupSequence = DOTween.Sequence();

        m_popupSequence
            .Append(
                popupTransform
                    .DOLocalMoveY(m_popupShownLocalPosition.y, m_popupEnterDuration)
                    .SetEase(Ease.OutCubic))
            .Join(
                m_popupCanvasGroup
                    .DOFade(1f, m_popupEnterDuration)
                    .SetEase(Ease.OutQuad));

        if (m_currentModelFade != null)
        {
            m_popupSequence.Join(m_currentModelFade.DOFadeIn(m_popupEnterDuration));
        }

        m_popupSequence.AppendInterval(m_popupHoldDuration);

        m_popupSequence
            .Append(
                popupTransform
                    .DOLocalMoveY(hiddenY, m_popupExitDuration)
                    .SetEase(Ease.InCubic))
            .Join(
                m_popupCanvasGroup
                    .DOFade(0f, m_popupExitDuration)
                    .SetEase(Ease.InQuad));

        if (m_currentModelFade != null)
        {
            m_popupSequence.Join(m_currentModelFade.DOFadeOut(m_popupExitDuration));
        }

        m_popupSequence
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                ClearCurrentModel();
                popupTransform.localPosition = m_popupShownLocalPosition;
                m_popupCanvasGroup.alpha = 0f;
                m_popupRoot.SetActive(false);
                m_popupSequence = null;
            });
    }
}
