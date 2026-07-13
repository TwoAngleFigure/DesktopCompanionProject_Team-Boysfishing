using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using DesktopCompanion.Views;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

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
    [SerializeField] private float m_popupFadeDuration = 0.4f;


    private FishingSystem m_fishingSystem;
    private ItemData_Fish m_currentFishData;
    private GameObject m_currentModel;
    private DitherFade m_currentModelFade;
    private Sequence m_popupSequence;


    public override void Bind()
    {
        m_fishingSystem = SystemManager.GetSystem<FishingSystem>();

        if (m_fishingSystem == null)
        {
            Debug.LogWarning("[FishingCatchWorldView] FishingSystem을 찾을 수 없습니다.");
            return;
        }

        m_fishingSystem.OnBattleStarted += HandleBattleStarted;
        m_fishingSystem.OnFishCaught += HandleFishCaught;

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
            m_fishingSystem.OnBattleStarted -= HandleBattleStarted;
            m_fishingSystem.OnFishCaught -= HandleFishCaught;
        }

        m_currentFishData = null;
        m_fishingSystem = null;
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

        int starCount = Mathf.Clamp(
            (int)fish.Quality,
            1,
            m_qualityStars.Length
        );

        for (int i = 0; i < m_qualityStars.Length; i++)
        {
            if (m_qualityStars[i] != null)
            {
                m_qualityStars[i].gameObject.SetActive(i < starCount);
            }
        }
    }
    private void HandleBattleStarted(EntityHandle battleFishHandle)
    {
        Entity_BattleFish battleFish = EntityManager.Get<Entity_BattleFish>(battleFishHandle);

        if (battleFish == null)
        {
            Debug.LogWarning("[FishingWorldView] 전투 물고기 Entity를 찾을 수 없음");
            m_currentFishData = null;
            return;
        }

        m_currentFishData = battleFish.BattleData.ItemFish;
    }

    private void HandleFishCaught(EntityHandle fishHandle)
    {
        Entity_Fish fish = EntityManager.Get<Entity_Fish>(fishHandle);

        if (fish == null)
        {
            Debug.LogWarning("[FishingWorldView] 잡힌 물고기 Entity를 찾을 수 없음");
            return;
        }

        if (m_currentFishData == null)
        {
            Debug.LogWarning("[FishingWorldView] 저장된 물고기 Data가 없음");
            return;
        }

        string modelKey = AssetKeys.Of(m_currentFishData, AssetUsage.Model);
        GameObject prefab = AssetProvider.Get<GameObject>(modelKey);

        if (prefab == null)
        {
            Debug.LogWarning($"[FishingWorldView] 물고기 모델 프리팹을 찾을 수 없음: key={modelKey}");
            return;
        }

        ClearCurrentModel();

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

        UpdateCatchPopup(fish);
        PlayCatchPopup();

        Debug.Log($"[FishingWorldView] 물고기 모델 생성: {fish.Name}, key={modelKey}");
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

    private void PlayCatchPopup()
    {
        m_popupSequence?.Kill();

        m_popupCanvasGroup.alpha = 1f;
        m_currentModelFade?.SetFadeImmediate(1f);
        m_popupRoot.SetActive(true);

        m_popupSequence = DOTween.Sequence()
            .AppendInterval(m_popupHoldDuration)
            .Append(
                m_popupCanvasGroup
                    .DOFade(0f, m_popupFadeDuration)
                    .SetEase(Ease.InQuad)
            );

        if (m_currentModelFade != null)
        {
            m_popupSequence.Join(
                m_currentModelFade.DOFadeOut(m_popupFadeDuration)
            );
        }

        m_popupSequence
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                ClearCurrentModel();
                m_popupRoot.SetActive(false);
                m_popupCanvasGroup.alpha = 1f;
            });
    }
}
