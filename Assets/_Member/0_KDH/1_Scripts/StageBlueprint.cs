using UnityEngine;
using System.Collections.Generic;
using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    public enum LayerDepth { Near, Mid, Far, None }

    [System.Serializable]
    public class BlueprintData
    {
        public string m_assetKey;
        public LayerDepth m_layerDepth;

        [Header("절대 거리 스폰 설정")]
        [Tooltip("목적지 도착 몇 유닛(물리 거리) 전에 이 오브젝트를 스폰할 것인가? (Far=55, Mid=40, Near=25)")]
        public float m_spawnDistanceTrigger = 40f;

        [Tooltip("배가 멈췄을 때 카메라 중심으로부터의 거리 오차(Offset). (화면 정중앙은 0, 화면 좌측 끝 정렬은 대략 -15)")]
        public float m_targetWorldOffsetFromCamera = 0f;

        [Header("레이어 내 최종 로컬 Y좌표 (높이)")]
        public float m_targetLocalY = 0f;

        [HideInInspector] public bool m_isSpawned = false;

        [HideInInspector] public float m_spawnTriggerX;
        [HideInInspector] public float m_targetWorldX;
        [HideInInspector] public float m_targetCameraX;
    }

    public class StageBlueprint : MonoBehaviour
    {
        [Header("레이어 컨테이너 할당")]
        public Transform m_nearLayer;
        public Transform m_midLayer;
        public Transform m_farLayer;

        [Header("원근감(Parallax) 설정")]
        [Tooltip("1에 가까울수록 카메라를 똑같이 따라감(아주 멀어 보임). 0이면 안 따라감(땅처럼 보임)")]
        public float m_nearParallax = 0f;
        public float m_midParallax = 0.5f;
        public float m_farParallax = 0.8f;

        [Header("스폰 청사진 리스트")]
        public List<BlueprintData> m_blueprintList = new List<BlueprintData>();

        private Transform m_cameraTransform;

        private AssetProvider m_assetProvider;

        public void InitProvider(AssetProvider provider, float startCamX, float targetCamX, bool isFirstLoad)
        {
            m_assetProvider = provider;

            float totalDistance = targetCamX - startCamX;

            foreach (var data in m_blueprintList)
            {
                data.m_isSpawned = false;

                if (isFirstLoad)
                {
                    data.m_targetCameraX = startCamX;
                    data.m_targetWorldX = startCamX + data.m_targetWorldOffsetFromCamera;
                    data.m_spawnTriggerX = startCamX;
                }
                else
                {
                    data.m_spawnTriggerX = targetCamX + data.m_spawnDistanceTrigger;
                    data.m_targetCameraX = targetCamX;
                    data.m_targetWorldX = targetCamX + data.m_targetWorldOffsetFromCamera;
                }
            }
            ForceInitialSpawnCheck(isFirstLoad);
        }
        private void Start()
        {
            if (Camera.main != null)
                m_cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (m_cameraTransform == null) return;

            float currentCamX = m_cameraTransform.position.x;

            UpdateParallax(currentCamX);

            foreach (var data in m_blueprintList)
            {
                if (data.m_isSpawned) continue;

                if (currentCamX <= data.m_spawnTriggerX)
                {
                    SpawnProp(data);
                    data.m_isSpawned = true;
                }
            }
        }

        private void ForceInitialSpawnCheck(bool isFirstLoad)
        {
            if (m_cameraTransform == null && Camera.main != null)
            {
                m_cameraTransform = Camera.main.transform;
            }

            if (m_cameraTransform == null) return;

            float currentCamX = m_cameraTransform.position.x;

            UpdateParallax(currentCamX);

            foreach (var data in m_blueprintList)
            {
                if (!data.m_isSpawned && (isFirstLoad || currentCamX <= data.m_spawnTriggerX))
                {
                    SpawnProp(data);
                    data.m_isSpawned = true;
                }
            }
        }

        private void UpdateParallax(float camX)
        {
            if (m_nearLayer) m_nearLayer.position = new Vector3(camX * m_nearParallax, 0f, m_nearLayer.position.z);
            if (m_midLayer) m_midLayer.position = new Vector3(camX * m_midParallax, 0f, m_midLayer.position.z);
            if (m_farLayer) m_farLayer.position = new Vector3(camX * m_farParallax, 0f, m_farLayer.position.z);
        }

        private void SpawnProp(BlueprintData data)
        {
            Transform parentLayer = GetLayerTransform(data.m_layerDepth);

            float parallaxFactor = GetParallaxFactor(data.m_layerDepth);

            if (m_assetProvider != null && m_assetProvider.TryGet(data.m_assetKey, out GameObject prefab))
            {
                GameObject obj = Instantiate(prefab, parentLayer);

                float convertedLocalX = data.m_targetWorldX - (data.m_targetCameraX * parallaxFactor);

                obj.transform.localPosition = new Vector3(convertedLocalX, data.m_targetLocalY, 0f);

                WorldProp prop = obj.GetComponent<WorldProp>();
                if (prop != null)
                {
                    prop.Init();
                }
                else
                {
                    Debug.LogWarning($"[경고] {data.m_assetKey} 프리팹에 WorldProp 스크립트가 없습니다!");
                }
            }
            else
            {
                if (m_assetProvider == null)
                {
                    Debug.LogError($"[SpawnProp 에러] m_assetProvider가 NULL입니다! 의존성 주입을 확인하세요.");
                }
                else
                {
                    Debug.LogError($"[SpawnProp 에러] AssetProvider 메모리에 '{data.m_assetKey}' 프리팹이 존재하지 않습니다! (Preload 누락 또는 키 이름 불일치)");
                }
            }

        }

        private Transform GetLayerTransform(LayerDepth depth)
        {
            switch (depth)
            {
                case LayerDepth.Near: return m_nearLayer;
                case LayerDepth.Mid: return m_midLayer;
                case LayerDepth.Far: return m_farLayer;
                default: return m_midLayer;
            }
        }

        private float GetParallaxFactor(LayerDepth depth)
        {
            switch (depth)
            {
                case LayerDepth.Near: return m_nearParallax;
                case LayerDepth.Mid: return m_midParallax;
                case LayerDepth.Far: return m_farParallax;
                default: return 0f;
            }
        }
    }
}