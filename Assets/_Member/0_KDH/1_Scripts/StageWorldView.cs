using UnityEngine;
using DesktopCompanion.Systems;
using DesktopCompanion.Core;
using DesktopCompanion.Controllers;
using DesktopCompanion.Data;
using System.Collections.Generic;

namespace DesktopCompanion.Views
{
    public class StageWorldView : UIViewBase
    {
        private readonly StageViewModel m_vm = new();
        public ShipController m_shipController;
        private readonly Dictionary<int, GameObject> m_spawnedStageInstances = new();
        private int m_loadedStageId = 0;

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            var voyageCtrl = SystemManager.GetSystem<VoyageSystem>();
            if (voyageCtrl != null)
            {
                voyageCtrl.OnVoyageStateChanged += HandleVoyageStateChanged;
                voyageCtrl.OnSpeedChanged += HandleSpeedChanged;
                voyageCtrl.OnBiomeChanged += HandleBiomeChanged;
            }

            var stageSys = SystemManager.GetSystem<StageSystem>();
            if (stageSys != null)
            {
                stageSys.OnTravelStarted += HandleTravelStarted;
                stageSys.OnStageChanged += HandleStageChanged;
            }

            // 🎯 초기 1회 1번째 맵 블루프린트 자동 생성 배치! (isFirstLoad: true)
            if (stageSys != null && stageSys.CurrentStageData != null)
            {
                LoadStageData(stageSys.CurrentStageData, isFirstLoad: true);
            }
            else
            {
                Vector2 currentPos = voyageCtrl != null ? voyageCtrl.CurrentLogicalPosition : Vector2.zero;
                var mapSys = SystemManager.GetSystem<MapSystem>();
                BiomeType currentBiome = mapSys != null ? mapSys.GetBiomeAt(currentPos) : BiomeType.None;
                LoadStageByBiome(currentBiome, isFirstLoad: true);
            }
        }

        public override void Unbind()
        {
            var voyageCtrl = SystemManager.GetSystem<VoyageSystem>();
            if (voyageCtrl != null)
            {
                voyageCtrl.OnVoyageStateChanged -= HandleVoyageStateChanged;
                voyageCtrl.OnSpeedChanged -= HandleSpeedChanged;
                voyageCtrl.OnBiomeChanged -= HandleBiomeChanged;
            }

            var stageSys = SystemManager.GetSystem<StageSystem>();
            if (stageSys != null)
            {
                stageSys.OnTravelStarted -= HandleTravelStarted;
                stageSys.OnStageChanged -= HandleStageChanged;
            }

            m_vm.Unbind();
            ClearAllStages();
        }

        // 🎯 항해 출발 시: 목적지 맵 블루프린트를 준비하고 (없으면 생성, 있으면 재사용), 스폰 트리거 세팅!
        private void HandleTravelStarted(int targetStageId, float totalTravelTime)
        {
            var stageSys = SystemManager?.GetSystem<StageSystem>();
            StageData targetStage = stageSys?.TargetStageData;
            if (targetStage == null && stageSys != null)
            {
                var allDatas = stageSys.GetAllStageDatas();
                if (allDatas != null)
                {
                    foreach (var data in allDatas)
                    {
                        if (data != null && data.ID == targetStageId)
                        {
                            targetStage = data;
                            break;
                        }
                    }
                }
            }

            if (targetStage != null)
            {
                PrepareNextStageData(targetStage);
            }
        }

        // 🎯 목적지 도착 정박 시: 맵 데이터를 파괴하지 않고 안전 유지 (롤백)!
        private void HandleStageChanged(int newStageId)
        {
            m_loadedStageId = newStageId;
        }

        private void HandleSpeedChanged(float newSpeed)
        {
            if (m_shipController != null)
            {
                m_shipController.m_speed = newSpeed;
            }
        }

        private void HandleBiomeChanged(BiomeType newBiome)
        {
            if (m_loadedStageId == 0 && m_spawnedStageInstances.Count == 0)
            {
                LoadStageByBiome(newBiome, isFirstLoad: true);
            }
        }

        private void HandleVoyageStateChanged(VoyageState newState)
        {
            if (m_shipController != null)
            {
                m_shipController.SetTraveling(newState != VoyageState.Anchored);
            }
        }

        private void PrepareNextStageData(StageData targetStage)
        {
            if (targetStage == null) return;

            float startCamX = Camera.main != null ? Camera.main.transform.position.x : 0f;
            float targetCamX = targetStage.MapPosition.x;

            // 이미 생성되어 존재하는 맵 데이터면 파괴 없이 재사용!
            if (m_spawnedStageInstances.TryGetValue(targetStage.ID, out GameObject existingInstance) && existingInstance != null)
            {
                var blueprint = existingInstance.GetComponent<StageBlueprint>();
                if (blueprint != null)
                {
                    blueprint.InitProvider(AssetProvider, startCamX, targetCamX, isFirstLoad: false);
                    Debug.Log($"[StageWorldView] 🎨 기존 맵 에셋({targetStage.ID}) 파괴 없이 재사용 및 트리거 재초기화 완료!");
                }
                return;
            }

            var mapSys = SystemManager.GetSystem<MapSystem>();
            BiomeType biome = mapSys != null ? mapSys.GetBiomeAt(targetStage.MapPosition) : BiomeType.None;

            List<string> candidateKeys = new List<string>
            {
                $"StageData_{targetStage.ID}_Model",
                $"StageData_{targetStage.ID}",
                $"Stage_{targetStage.ID}",
                $"StageBlueprint_{targetStage.ID}",
                GetAssetKeyByBiome(biome),
                "StageBlueprint_Default"
            };

            GameObject prefab = FindPrefabFromKeys(candidateKeys, out string matchedKey);
            if (prefab != null)
            {
                GameObject newInstance = Instantiate(prefab, transform);
                m_spawnedStageInstances[targetStage.ID] = newInstance;

                var blueprint = newInstance.GetComponent<StageBlueprint>();
                if (blueprint != null)
                {
                    blueprint.InitProvider(AssetProvider, startCamX, targetCamX, isFirstLoad: false);
                    Debug.Log($"[StageWorldView] 🎨 목적지 맵 '{matchedKey}' 신규 생성 및 트리거 세팅 완료!");
                }
            }
        }

        private void LoadStageData(StageData stageData, bool isFirstLoad)
        {
            if (stageData == null) return;

            float startCamX = Camera.main != null ? Camera.main.transform.position.x : 0f;
            float targetCamX = stageData.MapPosition.x;

            if (m_spawnedStageInstances.TryGetValue(stageData.ID, out GameObject existingInstance) && existingInstance != null)
            {
                var blueprint = existingInstance.GetComponent<StageBlueprint>();
                if (blueprint != null)
                {
                    blueprint.InitProvider(AssetProvider, startCamX, targetCamX, isFirstLoad);
                }
                m_loadedStageId = stageData.ID;
                return;
            }

            var mapSys = SystemManager.GetSystem<MapSystem>();
            BiomeType biome = mapSys != null ? mapSys.GetBiomeAt(stageData.MapPosition) : BiomeType.None;

            List<string> candidateKeys = new List<string>
            {
                $"StageData_{stageData.ID}_Model",
                $"StageData_{stageData.ID}",
                $"Stage_{stageData.ID}",
                $"StageBlueprint_{stageData.ID}",
                GetAssetKeyByBiome(biome),
                "StageBlueprint_Default"
            };

            GameObject prefab = FindPrefabFromKeys(candidateKeys, out string matchedKey);
            if (prefab != null)
            {
                GameObject newInstance = Instantiate(prefab, transform);
                m_spawnedStageInstances[stageData.ID] = newInstance;
                m_loadedStageId = stageData.ID;

                var blueprint = newInstance.GetComponent<StageBlueprint>();
                if (blueprint != null)
                {
                    blueprint.InitProvider(AssetProvider, startCamX, targetCamX, isFirstLoad);
                }
            }
        }

        private void LoadStageByBiome(BiomeType biome, bool isFirstLoad)
        {
            string key = GetAssetKeyByBiome(biome);
            GameObject prefab = FindPrefabFromKeys(new List<string> { key, "StageBlueprint_Default" }, out string matchedKey);
            if (prefab != null)
            {
                ClearAllStages();
                GameObject newInstance = Instantiate(prefab, transform);
                m_spawnedStageInstances[0] = newInstance;
                var blueprint = newInstance.GetComponent<StageBlueprint>();
                if (blueprint != null)
                {
                    float startCamX = Camera.main != null ? Camera.main.transform.position.x : 0f;
                    blueprint.InitProvider(AssetProvider, startCamX, startCamX, isFirstLoad);
                }
            }
        }

        private GameObject FindPrefabFromKeys(List<string> keys, out string matchedKey)
        {
            matchedKey = null;
            if (AssetProvider == null) return null;

            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                if (AssetProvider.TryGet<GameObject>(key, out GameObject prefab) && prefab != null)
                {
                    matchedKey = key;
                    return prefab;
                }
            }
            return null;
        }

        private void ClearAllStages()
        {
            foreach (var kvp in m_spawnedStageInstances)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            m_spawnedStageInstances.Clear();
            m_loadedStageId = 0;
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