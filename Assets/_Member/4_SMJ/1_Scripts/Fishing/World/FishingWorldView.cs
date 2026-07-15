using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using DesktopCompanion.Views;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FishingWorldView : WorldViewBase
{
    [SerializeField] private CanvasGroup m_popupCanvasGroup;

    [Header("Model")]
    [SerializeField] private Transform m_spawnPoint;
    [SerializeField] private Transform m_modelRoot;
    [SerializeField] private Vector3 m_modelScale = Vector3.one;

    [Header("Catch Popup")]
    [SerializeField] private GameObject m_popupRoot;
    [SerializeField] private TMP_Text m_fishInfoText;
    [SerializeField] private Image[] m_qualityStars;
    [SerializeField] private float m_popupHoldDuration = 1.5f;
    [SerializeField] private float m_popupMoveDistance = 0.4f;
    [SerializeField] private float m_popupEnterDuration = 0.35f;
    [SerializeField] private float m_popupExitDuration = 0.4f;

    private FishingSystem m_fishingSystem;
    private GameObject m_currentModel;
    private DitherFade m_currentModelFade;
    private Sequence m_popupSequence;

    private Vector3 m_popupShownLocalPosition;

    private void Awake()
    {
        if (m_popupRoot != null)
        {
            m_popupShownLocalPosition =
                m_popupRoot.transform.localPosition;
        }
    }

    public override void Bind()
    {
        m_fishingSystem = SystemManager.GetSystem<FishingSystem>();

        if (m_fishingSystem == null)
        {
            Debug.LogWarning("[FishingCatchWorldView] FishingSystem을 찾을 수 없습니다.");
            return;
        }

        m_fishingSystem.OnFishingResult += HandleFishingResult;
    }

    public override void Unbind()
    {
        m_popupSequence?.Kill();
        m_popupSequence = null;

        ClearCurrentModel();

        if (m_popupRoot != null)
        {
            m_popupRoot.SetActive(false);
        }

        if (m_fishingSystem != null)
        {
            m_fishingSystem.OnFishingResult -= HandleFishingResult;
        }

        m_fishingSystem = null;
    }

    private string GetResultText(FishingResultType resultType)
    {
        return resultType switch
        {
            FishingResultType.Failed => "낚시 실패",
            FishingResultType.InventoryFull => "인벤토리 부족",
            _ => string.Empty
        };

    }
    private void UpdateCatchPopup(Entity_Fish fish)
    {
        if (fish == null)
        {
            return;
        }

        if (m_fishInfoText != null)
        {
            m_fishInfoText.text = $"{fish.Name} / {fish.Size:0.00} cm";
        }

        SetQualityStars((int)fish.Quality);
    }

    private void SetQualityStars(int count)
    {
        if (m_qualityStars == null)
        {
            return;
        }

        int visibleCount = Mathf.Clamp(
            count,
            0,
            m_qualityStars.Length);

        for (int i = 0; i < m_qualityStars.Length; i++)
        {
            if (m_qualityStars[i] != null)
            {
                m_qualityStars[i].gameObject.SetActive(
                    i < visibleCount);
            }
        }
    }

    private void UpdateFailurePopup(FishingResultType resultType)
    {
        if (m_fishInfoText != null)
        {
            m_fishInfoText.text = GetResultText(resultType);
        }

        SetQualityStars(0);
    }


    private void HandleFishingResult(
        EntityHandle fishHandle,
        FishingResultType resultType)
    {
        if (resultType == FishingResultType.Success)
        {
            HandleFishCaught(fishHandle);
            return;
        }

        ClearCurrentModel();
        UpdateFailurePopup(resultType);
        PlayResultPopup();
    }

    private void HandleFishCaught(EntityHandle fishHandle)
    {
        Entity_Fish fish = EntityManager.Get<Entity_Fish>(fishHandle);

        if (fish == null)
        {
            Debug.LogWarning("[FishingWorldView] 잡힌 물고기 Entity를 찾을 수 없음");
            return;
        }

        ClearCurrentModel();
        UpdateCatchPopup(fish);
        CreateCaughtFishModel(fish.ItemData);
        PlayResultPopup();
    }

    private void CreateCaughtFishModel(ItemData_Fish fishData)
    {

        if (fishData == null)
        {
            Debug.LogWarning("[FishingWorldView] 잡힌 물고기의 Data가 없음");
            return;
        }

        string modelKey = AssetKeys.Of(fishData, AssetUsage.Model);
        GameObject prefab = AssetProvider.Get<GameObject>(modelKey);

        if (prefab == null)
        {
            Debug.LogWarning($"[FishingWorldView] 물고기 모델 프리팹을 찾을 수 없음: key={modelKey}");
            return;
        }

        Transform parent = m_modelRoot != null ? m_modelRoot : transform;
        Vector3 position = m_spawnPoint != null ? m_spawnPoint.position : transform.position;
        Quaternion rotation = m_spawnPoint != null ? m_spawnPoint.rotation : transform.rotation;

        m_currentModel = Instantiate(prefab, position, rotation, parent);
        m_currentModel.transform.localScale = m_modelScale;

        m_currentModelFade =
            m_currentModel.GetComponent<DitherFade>();

        if (m_currentModelFade == null)
        {
            Debug.LogWarning(
                "[FishingWorldView] 생성된 물고기 모델에 DitherFade가 없습니다."
            );
        }

        Debug.Log($"[FishingWorldView] 물고기 모델 생성: {fishData.Name}, key={modelKey}");
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
            Debug.LogWarning("[FishingWorldView] 결과 팝업 참조가 설정되지 않았습니다.");
            return;
        }

        m_popupSequence?.Kill();

        Transform popupTransform = m_popupRoot.transform;

        float hiddenY =
            m_popupShownLocalPosition.y - m_popupMoveDistance;

        // 아래쪽 시작 위치
        popupTransform.localPosition = new Vector3(
            m_popupShownLocalPosition.x,
            hiddenY,
            m_popupShownLocalPosition.z
        );

        m_popupRoot.SetActive(true);

        // SetActive 시 DitherFade 자동 재생이 시작될 수 있으므로
        // 활성화 이후 다시 초기화한다.
        m_popupCanvasGroup.alpha = 0f;
        m_currentModelFade?.SetFadeImmediate(0f);

        m_popupSequence = DOTween.Sequence();

        // 아래에서 올라오면서 패널과 모델 페이드 인
        m_popupSequence
            .Append(
                popupTransform
                    .DOLocalMoveY(
                        m_popupShownLocalPosition.y,
                        m_popupEnterDuration
                    )
                    .SetEase(Ease.OutCubic)
            )
            .Join(
                m_popupCanvasGroup
                    .DOFade(1f, m_popupEnterDuration)
                    .SetEase(Ease.OutQuad)
            );

        if (m_currentModelFade != null)
        {
            m_popupSequence.Join(
                m_currentModelFade.DOFadeIn(m_popupEnterDuration)
            );
        }

        // 유지
        m_popupSequence.AppendInterval(m_popupHoldDuration);

        // 아래로 내려가면서 패널과 모델 페이드 아웃
        m_popupSequence
            .Append(
                popupTransform
                    .DOLocalMoveY(hiddenY, m_popupExitDuration)
                    .SetEase(Ease.InCubic)
            )
            .Join(
                m_popupCanvasGroup
                    .DOFade(0f, m_popupExitDuration)
                    .SetEase(Ease.InQuad)
            );

        if (m_currentModelFade != null)
        {
            m_popupSequence.Join(
                m_currentModelFade.DOFadeOut(m_popupExitDuration)
            );
        }

        m_popupSequence
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                ClearCurrentModel();

                // 다음 재생을 위해 원래 위치로 복구
                popupTransform.localPosition =
                    m_popupShownLocalPosition;

                m_popupCanvasGroup.alpha = 0f;
                m_popupRoot.SetActive(false);
                m_popupSequence = null;
            });
    }
}
