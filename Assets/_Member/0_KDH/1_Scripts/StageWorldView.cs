using UnityEngine;
using DesktopCompanion.Systems;
using DesktopCompanion.Core;
using DesktopCompanion.Controllers;
using DesktopCompanion.Data;
using DG.Tweening;
using System.Collections.Generic;

namespace DesktopCompanion.Views
{
    public class StageWorldView : UIViewBase
    {
        private readonly StageViewModel m_vm = new();

        [Header("배 컨트롤러 연결")]
        public ShipController m_shipController;

        private readonly List<GameObject> m_activeStageInstances = new();
        private GameObject m_currentStageInstance;
        private string m_lastLoadedAssetKey = "";

        private float m_absoluteStartCamX = 0f;
        private float m_absoluteTargetCamX = 0f;

        private const float DEPARTURE_TIME = 21.0f;
        private const float ARRIVAL_TIME = 21.0f;
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
            ClearAllStages();
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
                    ClearOldStagesExceptCurrent();

                    var voyage = SystemManager.GetSystem<VoyageSystem>();
                    bool isCanceled = voyage != null && voyage.IsCanceled;

                    // 항해 중단(취소) 정박일 경우 순간이동 로드(LoadStage)를 부르지 않고 현 위치에 자연 정박!
                    if (!isCanceled && stageSystem.CurrentStageData != null)
                    {
                        LoadStage(GetStageAssetKey(stageSystem.CurrentStageData), isTarget: false);
                    }
                    break;

                case VoyageState.Departing:
                    PlayDepartureSequence(stageSystem);
                    break;

                case VoyageState.Traveling:
                    if (stageSystem.TargetStageData != null) LoadStage(GetStageAssetKey(stageSystem.TargetStageData), isTarget: true);
                    break;

                case VoyageState.Arriving:
                    PlayArrivalSequence(stageSystem);
                    break;

                case VoyageState.Stopping:
                    // Stopping 중단 9초 동안 물과 맵 배경을 절대 파괴하지 않고 유지!
                    PlayStoppingSequence(stageSystem);
                    break;
            }
        }

        private void HandleTravelStarted(int targetDataId, float duration)
        {
            var stageSystem = SystemManager.GetSystem<StageSystem>();
            if (stageSystem == null) return;

            if (stageSystem.TargetStageData != null)
            {
                LoadStage(GetStageAssetKey(stageSystem.TargetStageData), isTarget: true);
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

        private GameObject m_targetStageInstance;
        private bool m_hasInitialResetDone = false;

        private void LoadStage(string stageAssetKey, bool isTarget)
        {
            var stageSystem = SystemManager.GetSystem<StageSystem>();
            bool isAnchored = (stageSystem != null && stageSystem.CurrentState == VoyageState.Anchored);

            if (m_lastLoadedAssetKey == stageAssetKey && !isTarget && !isAnchored)
            {
                return;
            }

            bool isFirstLoad = string.IsNullOrEmpty(m_lastLoadedAssetKey) && !isTarget;

            if (!m_hasInitialResetDone && m_shipController != null)
            {
                m_shipController.ResetToOrigin();
                m_hasInitialResetDone = true;
            }
            m_lastLoadedAssetKey = stageAssetKey;

            GameObject stagePrefab = FindStagePrefab(stageAssetKey);
            if (stagePrefab != null)
            {
                if (isTarget)
                {
                    if (m_targetStageInstance != null)
                    {
                        Destroy(m_targetStageInstance);
                        m_activeStageInstances.Remove(m_targetStageInstance);
                    }
                    m_targetStageInstance = Instantiate(stagePrefab, Vector3.zero, Quaternion.identity, transform);
                    m_currentStageInstance = m_targetStageInstance;
                    m_activeStageInstances.Add(m_targetStageInstance);
                }
                else
                {
                    m_currentStageInstance = Instantiate(stagePrefab, Vector3.zero, Quaternion.identity, transform);
                    m_activeStageInstances.Add(m_currentStageInstance);
                }

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

        private void ClearOldStagesExceptCurrent()
        {
            for (int i = m_activeStageInstances.Count - 1; i >= 0; i--)
            {
                GameObject inst = m_activeStageInstances[i];
                if (inst != null && inst != m_currentStageInstance)
                {
                    Destroy(inst);
                    m_activeStageInstances.RemoveAt(i);
                }
            }
        }

        private void ClearAllStages()
        {
            foreach (GameObject inst in m_activeStageInstances)
            {
                if (inst != null)
                {
                    Destroy(inst);
                }
            }
            m_activeStageInstances.Clear();
            m_currentStageInstance = null;
            m_lastLoadedAssetKey = "";
        }

        private string GetStageAssetKey(StageData stageData)
        {
            if (stageData == null) return "StageBlueprint_Default";

            // StageData.ID 기준 정직 탐색 (예: 첫번째 맵 600001 -> StageData_600001_Model)
            string keyWithModel = $"StageData_{stageData.ID}_Model";
            if (AssetProvider != null && AssetProvider.TryGet<GameObject>(keyWithModel, out GameObject _))
            {
                return keyWithModel;
            }

            string keySimple = $"StageData_{stageData.ID}";
            if (AssetProvider != null && AssetProvider.TryGet<GameObject>(keySimple, out GameObject _))
            {
                return keySimple;
            }

            return AssetKeys.Of(stageData, AssetUsage.Model);
        }

        private GameObject FindStagePrefab(string key)
        {
            if (AssetProvider != null && AssetProvider.TryGet(key, out GameObject prefab))
            {
                return prefab;
            }
            return null;
        }

        private string GetAssetKeyByBiome(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.River: return "StageBlueprint_River";
                case BiomeType.ColdSea: return "StageBlueprint_ColdSea";
                case BiomeType.Mudflat: return "StageBlueprint_Mudflat";
                default: return "StageBlueprint_Default";
            }
        }
    }
}