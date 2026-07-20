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

        private float m_absoluteStartCamX = 0f;
        private float m_absoluteTargetCamX = 0f;

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            var stageSystem = SystemManager.GetSystem<StageSystem>();
            if (stageSystem != null)
            {
                stageSystem.OnVoyageStateChanged += HandleVoyageStateChanged;
                stageSystem.OnTravelStarted += HandleTravelStarted;

                HandleVoyageStateChanged(stageSystem.CurrentState);
            }
        }

        public override void Unbind()
        {
            var stageSystem = SystemManager.GetSystem<StageSystem>();
            if (stageSystem != null)
            {
                stageSystem.OnVoyageStateChanged -= HandleVoyageStateChanged;
                stageSystem.OnTravelStarted -= HandleTravelStarted;
            }

            m_vm.Unbind();
            ClearCurrentStage();
        }

        private void Update()
        {
            if (m_vm == null || SystemManager == null) return;
            var stageSystem = SystemManager.GetSystem<StageSystem>();
            if (stageSystem == null) return;

            if (m_shipController != null)
            {
                m_shipController.SetTraveling(stageSystem.IsTraveling);
            }
        }

        private void HandleVoyageStateChanged(VoyageState newState)
        {
            var stageSystem = SystemManager.GetSystem<StageSystem>();
            if (stageSystem == null) return;

            switch (newState)
            {
                case VoyageState.Anchored:
                    if (stageSystem.CurrentStageData != null)
                    {
                        LoadStage(AssetKeys.Of(stageSystem.CurrentStageData, AssetUsage.Model), isTarget: false);
                    }
                    break;

                case VoyageState.Departing:
                    break;

                case VoyageState.Traveling:
                    if (stageSystem.TargetStageData != null)
                    {
                        LoadStage(AssetKeys.Of(stageSystem.TargetStageData, AssetUsage.Model), isTarget: true);
                    }
                    break;

                case VoyageState.Arriving:
                    break;
            }
        }

        private void HandleTravelStarted(int targetDataId, float duration)
        {
            var stageSystem = SystemManager.GetSystem<StageSystem>();
            if (stageSystem == null) return;

            if (stageSystem.CurrentState == VoyageState.Traveling && stageSystem.TargetStageData != null)
            {
                LoadStage(AssetKeys.Of(stageSystem.TargetStageData, AssetUsage.Model), isTarget: true);
            }
        }

        private void LoadStage(string stageAssetKey, bool isTarget)
        {
            if (m_lastLoadedAssetKey == stageAssetKey) return;

            bool isFirstLoad = string.IsNullOrEmpty(m_lastLoadedAssetKey);
            ClearCurrentStage();

            if (isFirstLoad && m_shipController != null)
            {
                m_shipController.ResetToOrigin();
            }

            m_lastLoadedAssetKey = stageAssetKey;

            if (AssetProvider != null && AssetProvider.TryGet(stageAssetKey, out GameObject stagePrefab))
            {
                m_currentStageInstance = Instantiate(stagePrefab, Vector3.zero, Quaternion.identity, transform);

                StageBlueprint blueprint = m_currentStageInstance.GetComponent<StageBlueprint>();
                if (blueprint != null)
                {
                    var stageSystem = SystemManager.GetSystem<StageSystem>();
                    float shipSpeed = m_shipController != null ? m_shipController.m_speed : 5f;
                    float currentCamX = Camera.main != null ? Camera.main.transform.position.x : 0f;

                    if (!isTarget)
                    {
                        m_absoluteStartCamX = currentCamX;
                        m_absoluteTargetCamX = m_absoluteStartCamX;
                    }
                    else
                    {
                        float remainingTime = stageSystem != null ? stageSystem.RemainingTravelTime : 0f;

                        m_absoluteStartCamX = currentCamX;
                        m_absoluteTargetCamX = m_absoluteStartCamX - (remainingTime * shipSpeed);
                    }

                    blueprint.InitProvider(AssetProvider, m_absoluteStartCamX, m_absoluteTargetCamX, isFirstLoad);
                    Debug.Log($"[StageWorldView] {stageAssetKey} 맵 로드! (구분: {(isTarget ? "목적지" : "출발지")}, 거리 적용: {m_absoluteTargetCamX})");
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