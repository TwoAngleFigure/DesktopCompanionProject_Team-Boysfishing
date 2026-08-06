using UnityEngine;
using DesktopCompanion.Systems;
using DesktopCompanion.Core;
using DesktopCompanion.Controllers;
using DesktopCompanion.Data;
using DG.Tweening;

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

        private const float DEPARTURE_TIME = 30.0f;
        private const float ARRIVAL_TIME = 30.0f;
        private const float STOPPING_TIME = 9.0f;

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
            if (SystemManager != null)
            {
                var stageSystem = SystemManager.GetSystem<StageSystem>();
                if (stageSystem != null)
                {
                    stageSystem.OnVoyageStateChanged -= HandleVoyageStateChanged;
                    stageSystem.OnTravelStarted -= HandleTravelStarted;
                }
            }

            if (m_vm != null)
            {
                m_vm.Unbind();
            }

            DOTween.Kill(this);
            ClearCurrentStage();
        }

        private void Update()
        {
            if (m_vm == null || SystemManager == null) return;
            var stageSystem = SystemManager.GetSystem<StageSystem>();
            if (stageSystem == null) return;

            if (m_shipController != null) m_shipController.SetTraveling(stageSystem.IsTraveling);
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
                    else
                    {
                        ClearCurrentStage();
                    }
                    break;

                case VoyageState.Departing:
                    PlayDepartureSequence(stageSystem);
                    break;

                case VoyageState.Traveling:
                    if (stageSystem.TargetStageData != null) LoadStage(AssetKeys.Of(stageSystem.TargetStageData, AssetUsage.Model), isTarget: true);
                    break;

                case VoyageState.Arriving:
                    PlayArrivalSequence(stageSystem);
                    break;

                case VoyageState.Stopping:
                    ClearCurrentStage();
                    PlayStoppingSequence(stageSystem);
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

        private void PlayDepartureSequence(StageSystem stageSystem)
        {
            DOTween.Kill(this);
            float maxSpeed = stageSystem.MaxSpeed;

            if (m_shipController != null) m_shipController.m_speed = 0f;
            stageSystem.SyncSpeed(0f);

            DOVirtual.Float(0f, 1f, DEPARTURE_TIME, (v) =>
            {
                float currentSpeed = maxSpeed * v;
                if (m_shipController != null) m_shipController.m_speed = currentSpeed;
                stageSystem.SyncSpeed(currentSpeed);
            })
            .SetEase(Ease.InOutSine)
            .SetId(this)
            .OnComplete(() =>
            {
                if (m_shipController != null) m_shipController.m_speed = maxSpeed;
                stageSystem.SyncSpeed(maxSpeed);
                stageSystem.SequenceComplete_Departure();
            });
        }

        private void PlayArrivalSequence(StageSystem stageSystem)
        {
            DOTween.Kill(this);
            float maxSpeed = stageSystem.MaxSpeed;

            DOVirtual.Float(1f, 0f, ARRIVAL_TIME, (v) =>
            {
                float currentSpeed = maxSpeed * v;
                if (m_shipController != null) m_shipController.m_speed = currentSpeed;
                stageSystem.SyncSpeed(currentSpeed);
            })
            .SetEase(Ease.InOutSine)
            .SetId(this)
            .OnComplete(() =>
            {
                if (m_shipController != null) m_shipController.m_speed = 0f;
                stageSystem.SyncSpeed(0f);
                stageSystem.SequenceComplete_Arrival();
            });
        }

        private void PlayStoppingSequence(StageSystem stageSystem)
        {
            DOTween.Kill(this);
            float maxSpeed = stageSystem.MaxSpeed;
            float currentRatio = (m_shipController != null && maxSpeed > 0f) ? Mathf.Clamp01(m_shipController.m_speed / maxSpeed) : 1f;

            DOVirtual.Float(currentRatio, 0f, STOPPING_TIME, (v) =>
            {
                float currentSpeed = maxSpeed * v;
                if (m_shipController != null) m_shipController.m_speed = currentSpeed;
                stageSystem.SyncSpeed(currentSpeed);
            })
            .SetEase(Ease.InOutSine)
            .SetId(this)
            .OnComplete(() =>
            {
                if (m_shipController != null) m_shipController.m_speed = 0f;
                stageSystem.SyncSpeed(0f);
                stageSystem.SequenceComplete_Stop();
            });
        }

        private void LoadStage(string stageAssetKey, bool isTarget)
        {
            if (m_lastLoadedAssetKey == stageAssetKey) return;
            bool isFirstLoad = string.IsNullOrEmpty(m_lastLoadedAssetKey) && !isTarget;
            ClearCurrentStage();

            var stageSystem = SystemManager.GetSystem<StageSystem>();
            bool isActuallyAnchored = stageSystem != null && stageSystem.CurrentState == VoyageState.Anchored;

            if (isFirstLoad && m_shipController != null) m_shipController.ResetToOrigin();
            m_lastLoadedAssetKey = stageAssetKey;

            if (AssetProvider != null && AssetProvider.TryGet(stageAssetKey, out GameObject stagePrefab))
            {
                m_currentStageInstance = Instantiate(stagePrefab, Vector3.zero, Quaternion.identity, transform);
                StageBlueprint blueprint = m_currentStageInstance.GetComponent<StageBlueprint>();

                if (blueprint != null)
                {
                    float currentCamX = Camera.main != null ? Camera.main.transform.position.x : 0f;

                    if (!isTarget)
                    {
                        m_absoluteStartCamX = currentCamX;
                        m_absoluteTargetCamX = m_absoluteStartCamX;
                    }
                    else
                    {
                        float remainingDist = stageSystem != null ? stageSystem.RemainingDistance : 0f;
                        m_absoluteStartCamX = currentCamX;
                        m_absoluteTargetCamX = m_absoluteStartCamX - remainingDist;
                    }

                    blueprint.InitProvider(AssetProvider, m_absoluteStartCamX, m_absoluteTargetCamX, isFirstLoad);
                }
            }
        }

        private void ClearCurrentStage()
        {
            if (m_currentStageInstance != null) { Destroy(m_currentStageInstance); m_currentStageInstance = null; }
            m_lastLoadedAssetKey = "";
        }
    }
}