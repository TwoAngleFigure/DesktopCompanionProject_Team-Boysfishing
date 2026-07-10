using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using DesktopCompanion.Views;
using UnityEngine;

public class FishingWorldView : WorldViewBase
{
    [Header("Model")]
    [SerializeField] private Transform m_spawnPoint;
    [SerializeField] private Transform m_modelRoot;
    [SerializeField] private Vector3 m_modelScale = Vector3.one;

    private FishingSystem m_fishingSystem;
    private ItemData_Fish m_currentFishData;
    private GameObject m_currentModel;


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
        if (m_fishingSystem != null)
        {
            m_fishingSystem.OnBattleStarted -= HandleBattleStarted;
            m_fishingSystem.OnFishCaught -= HandleFishCaught;
        }

        m_currentFishData = null;
        m_fishingSystem = null;
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

        Debug.Log($"[FishingWorldView] 물고기 모델 생성: {fish.Name}, key={modelKey}");
    }

    private void ClearCurrentModel()
    {
        if (m_currentModel != null)
        {
            Destroy(m_currentModel);
            m_currentModel = null;
        }
    }
}
