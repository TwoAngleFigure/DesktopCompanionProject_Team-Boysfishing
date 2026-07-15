using UnityEngine;
using DesktopCompanion.Systems;
using DesktopCompanion.Core;
using DesktopCompanion.Controllers;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    public class StageWorldView : UIViewBase
    {
        private readonly StageViewModel m_vm = new();

        [Header("배 컨트롤러 연결")]
        public ShipController m_shipController;

        private GameObject m_currentStageInstance;
        private string m_lastLoadedAssetKey = "";

        private bool m_hasSpawnedTarget = false;
        private StageData m_activeJourneyTargetData = null;

        private string m_visualDepartureAssetKey = "";

        private float m_absoluteStartCamX = 0f;
        private float m_absoluteTargetCamX = 0f;

        private const float CLEAR_START_MAP_DISTANCE = 35f;

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();
        }

        public override void Unbind()
        {
            m_vm.Unbind();
            ClearCurrentStage();
        }

        private void Update()
        {
            if (m_vm == null || SystemManager == null) return;

            if (m_shipController != null)
            {
                m_shipController.SetTraveling(m_vm.IsTraveling);
            }

            var stageSystem = SystemManager.GetSystem<StageSystem>();
            if (stageSystem == null) return;

            if (stageSystem.IsTraveling)
            {
                if (m_activeJourneyTargetData != stageSystem.TargetStageData)
                {
                    m_activeJourneyTargetData = stageSystem.TargetStageData;
                    m_hasSpawnedTarget = false;

                    m_visualDepartureAssetKey = string.IsNullOrEmpty(m_lastLoadedAssetKey)
                        ? AssetKeys.Of(stageSystem.CurrentStageData, AssetUsage.Model)
                        : m_lastLoadedAssetKey;

                    m_absoluteStartCamX = Camera.main != null ? Camera.main.transform.position.x : 0f;

                    float totalDuration = stageSystem.RemainingTravelTime;
                    float shipSpeed = m_shipController != null ? m_shipController.m_speed : 5f;

                    m_absoluteTargetCamX = m_absoluteStartCamX - (totalDuration * shipSpeed);
                }

                float currentCamX = Camera.main != null ? Camera.main.transform.position.x : 0f;
                float traveledDistance = m_absoluteStartCamX - currentCamX;
                bool shouldShowTargetMap = traveledDistance >= CLEAR_START_MAP_DISTANCE;

                if (shouldShowTargetMap)
                {
                    if (!m_hasSpawnedTarget)
                    {
                        string targetAssetKey = AssetKeys.Of(stageSystem.TargetStageData, AssetUsage.Model);
                        LoadStage(targetAssetKey);
                        m_hasSpawnedTarget = true;
                    }
                }
                else
                {
                    if (m_lastLoadedAssetKey != m_visualDepartureAssetKey)
                    {
                        LoadStage(m_visualDepartureAssetKey);
                    }
                }
            }
            else
            {
                m_activeJourneyTargetData = null;
                m_hasSpawnedTarget = false;
                m_visualDepartureAssetKey = "";

                if (stageSystem.CurrentStageData != null)
                {
                    bool isArrived = Vector2.Distance(stageSystem.CurrentLogicalPosition, stageSystem.CurrentStageData.MapPosition) <= 0.001f;

                    if (isArrived)
                    {
                        string currentAssetKey = AssetKeys.Of(stageSystem.CurrentStageData, AssetUsage.Model);
                        if (m_lastLoadedAssetKey != currentAssetKey)
                        {
                            LoadStage(currentAssetKey);
                        }
                    }
                }
            }
        }

        private void LoadStage(string stageAssetKey)
        {
            bool isFirstLoad = string.IsNullOrEmpty(m_lastLoadedAssetKey);

            ClearCurrentStage();

            if (isFirstLoad)
            {
                if (m_shipController != null)
                {
                    m_shipController.ResetToOrigin();
                }
            }

            m_lastLoadedAssetKey = stageAssetKey;

            if (AssetProvider != null && AssetProvider.TryGet(stageAssetKey, out GameObject stagePrefab))
            {
                m_currentStageInstance = Instantiate(stagePrefab, Vector3.zero, Quaternion.identity, transform);

                StageBlueprint blueprint = m_currentStageInstance.GetComponent<StageBlueprint>();
                if (blueprint != null)
                {
                    float startX = m_absoluteStartCamX;
                    float targetX = m_absoluteTargetCamX;

                    if (isFirstLoad)
                    {
                        startX = Camera.main != null ? Camera.main.transform.position.x : 0f;
                        targetX = startX;
                    }

                    blueprint.InitProvider(AssetProvider, startX, targetX, isFirstLoad);
                    Debug.Log($"[StageWorldView] {stageAssetKey} 맵 로드 성공! (구간: {startX} ➡️ {targetX})");
                }
            }
        }

        private void ClearCurrentStage()
        {
            if (m_currentStageInstance != null)
            {
                Destroy(m_currentStageInstance);
                m_currentStageInstance = null;
            }
            m_lastLoadedAssetKey = "";
        }
    }
}