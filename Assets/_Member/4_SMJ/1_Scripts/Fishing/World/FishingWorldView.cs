using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using DesktopCompanion.Views;
using UnityEngine;

public class FishingWorldView : WorldViewBase
{
    [Header("Catch Path")]
    [SerializeField] private FishingCatchPathEffect m_catchPathEffect;
    [SerializeField] private Vector3 m_flightModelScale = Vector3.one;
    [SerializeField] private Vector3 m_flightModelLocalEulerAngles = Vector3.zero;

    [Header("Catch Fish Size Scale")]
    [SerializeField] private float m_flightReferenceSizeCm = 100f;
    [SerializeField] private float m_minFlightScale = 0.2f;

    [Header("Catch Popup")]
    [SerializeField] private CaughtFishPopupWorldView m_caughtFishPopup;

    [Header("Drop Popup")]
    [SerializeField] private FishingDropPopupWorldView m_dropPopup;
    [SerializeField] private Sprite m_tempDropIcon;

    private FishingSystem m_fishingSystem;
    private GameObject m_currentFlightRoot;
    private GameObject m_currentFlightModel;

    public override void Bind()
    {
        m_fishingSystem = SystemManager.GetSystem<FishingSystem>();

        if (m_fishingSystem == null)
        {
            Debug.LogWarning("[FishingWorldView] FishingSystem을 찾을 수 없습니다.");
            return;
        }

        m_fishingSystem.OnFishCaughtPresentation += HandleFishCaught;
        m_fishingSystem.OnFishingResult += HandleFishingResult;
        m_fishingSystem.OnPendingCatchChanged += HandlePendingCatchChanged;
        m_fishingSystem.OnItemDropped += HandleItemDropped;
    }

    public override void Unbind()
    {
        ClearCurrentFlightModel();
        m_caughtFishPopup?.Cleanup();
        m_dropPopup?.Cleanup();

        if (m_fishingSystem != null)
        {
            m_fishingSystem.OnFishCaughtPresentation -= HandleFishCaught;
            m_fishingSystem.OnFishingResult -= HandleFishingResult;
            m_fishingSystem.OnPendingCatchChanged -= HandlePendingCatchChanged;
            m_fishingSystem.OnItemDropped -= HandleItemDropped;
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

    private void HandleFishingResult(FishingResultType resultType)
    {
        if (resultType == FishingResultType.Success)
        {
            return;
        }

        if (resultType == FishingResultType.InventoryFull && m_fishingSystem != null && m_fishingSystem.HasPendingCatch)
        {
            return;
        }

        ClearCurrentFlightModel();

        if (m_caughtFishPopup == null)
        {
            Debug.LogWarning("[FishingWorldView] CaughtFishPopupWorldView가 연결되지 않았습니다.");
            return;
        }

        m_caughtFishPopup.ShowFailure(GetResultText(resultType));
    }

    private void HandleFishCaught(
        EntityHandle fishHandle,
        FishCollectionUpdateResult collectionResult)
    {
        Entity_Fish fish = EntityManager.Get<Entity_Fish>(fishHandle);

        if (fish == null)
        {
            Debug.LogWarning("[FishingWorldView] 포획 물고기 Entity를 찾을 수 없습니다.");
            return;
        }

        GameObject fishPrefab = GetFishModelPrefab(fish.ItemData);

        if (fishPrefab != null)
        {
            PlayCatchPath(fishPrefab, fish.Size);
        }

        if (m_caughtFishPopup == null)
        {
            Debug.LogWarning("[FishingWorldView] CaughtFishPopupWorldView가 연결되지 않았습니다.");
            return;
        }

        m_caughtFishPopup.ShowCaughtFish(fish, fishPrefab, collectionResult);
    }

    private void HandlePendingCatchChanged()
    {
        if (m_caughtFishPopup == null)
        {
            return;
        }

        if (m_fishingSystem != null &&
            m_fishingSystem.HasPendingCatch)
        {
            m_caughtFishPopup.KeepVisibleForPending();
            return;
        }

        m_caughtFishPopup.CloseAfterPending();
    }

    private void HandleItemDropped(
    FishingGrantedDropInfo dropInfo)
    {
        if (m_fishingSystem == null || m_dropPopup == null)
        {
            return;
        }

        ItemData itemData = m_fishingSystem.GetItemData(dropInfo.ItemType, dropInfo.DataId);

        if (itemData == null)
        {
            Debug.LogWarning($"[FishingWorldView] 드롭 아이템 데이터를 찾을 수 없습니다: " +
                $"type={dropInfo.ItemType}, id={dropInfo.DataId}");
            return;
        }

        string iconKey = AssetKeys.Of(
            itemData,
            AssetUsage.Icon);

        Sprite icon = AssetProvider.Get<Sprite>(iconKey);

        if (icon == null)
        {
            // TODO: 실제 드롭 아이콘 등록이 끝나면 임시 아이콘 대체와 함께 로그를 다시 활성화합니다.
            // Debug.LogWarning($"[FishingWorldView] 드롭 아이콘을 찾을 수 없습니다: key={iconKey}");
            icon = m_tempDropIcon;
        }

        if (icon == null)
        {
            return;
        }

        m_dropPopup.Show(icon, dropInfo.Count);
    }

    private GameObject GetFishModelPrefab(ItemData_Fish fishData)
    {
        if (fishData == null)
        {
            Debug.LogWarning("[FishingWorldView] 포획 물고기의 데이터가 없습니다.");
            return null;
        }

        string modelKey = AssetKeys.Of(fishData, AssetUsage.Model);
        GameObject prefab = AssetProvider.Get<GameObject>(modelKey);

        if (prefab == null)
        {
            Debug.LogWarning(
                $"[FishingWorldView] 물고기 모델 프리팹을 찾을 수 없습니다: key={modelKey}");
        }

        return prefab;
    }

    private void PlayCatchPath(GameObject fishPrefab, float fishSizeCm)
    {
        if (m_catchPathEffect == null)
        {
            Debug.LogWarning("[FishingWorldView] Catch Path Effect가 연결되지 않았습니다.");
            return;
        }

        ClearCurrentFlightModel();

        Transform flightParent = m_catchPathEffect.transform;
        m_currentFlightRoot = new GameObject("FishingCatchFlightRoot");

        Transform flightRootTransform = m_currentFlightRoot.transform;
        flightRootTransform.SetParent(flightParent, false);

        m_currentFlightModel = Instantiate(fishPrefab, flightRootTransform);

        Transform modelTransform = m_currentFlightModel.transform;
        modelTransform.localPosition = Vector3.zero;
        modelTransform.localRotation = Quaternion.Euler(m_flightModelLocalEulerAngles);

        float normalizedSize = fishSizeCm / m_flightReferenceSizeCm;
        float sizeScale = Mathf.Sqrt(Mathf.Max(0f, normalizedSize));
        sizeScale = Mathf.Max(sizeScale, m_minFlightScale);

        modelTransform.localScale = m_flightModelScale * sizeScale;

        GameObject flightRoot = m_currentFlightRoot;

        bool started = m_catchPathEffect.Play(flightRootTransform, () =>
        {
            if (m_currentFlightRoot == flightRoot)
            {
                m_currentFlightRoot = null;
                m_currentFlightModel = null;
            }

            if (flightRoot != null)
            {
                Destroy(flightRoot);
            }
        });

        if (!started)
        {
            ClearCurrentFlightModel();
        }
    }

    private void ClearCurrentFlightModel()
    {
        m_catchPathEffect?.Stop();

        if (m_currentFlightRoot != null)
        {
            Destroy(m_currentFlightRoot);
        }
        else if (m_currentFlightModel != null)
        {
            Destroy(m_currentFlightModel);
        }

        m_currentFlightRoot = null;
        m_currentFlightModel = null;
    }
}
