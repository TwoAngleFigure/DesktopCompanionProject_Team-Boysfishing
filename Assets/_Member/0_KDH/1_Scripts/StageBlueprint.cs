using UnityEngine;
using System.Collections.Generic;
using DesktopCompanion.Core;

namespace DesktopCompanion.Views
{
    public enum LayerDepth { Near, Mid, Far }

    [System.Serializable]
    public class BlueprintData
    {
        public string m_assetKey;
        public LayerDepth m_layerDepth;

        [Header("생성 트리거 X좌표 (카메라가 이 위치를 지날 때 생성)")]
        [Tooltip("배가 왼쪽으로 가니까 음수 값(-150 등)이 들어갑니다.")]
        public float m_spawnTriggerX;

        [Header("오브젝트가 정렬될 최종 월드 X좌표")]
        [Tooltip("배가 멈추는 위치인 -300 등을 입력하세요.")]
        public float m_targetWorldX;

        [Header("정렬되는 순간의 최종 카메라 X좌표")]
        [Tooltip("배가 멈췄을 때 카메라의 실제 월드 X좌표를 입력하세요.")]
        public float m_targetCameraX;

        [Header("레이어 내 최종 로컬 Y좌표 (높이)")]
        public float m_targetLocalY = 0f;

        [HideInInspector] public bool m_isSpawned = false;
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

        public void InitProvider(AssetProvider provider)
        {
            m_assetProvider = provider;

            ForceInitialSpawnCheck();
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

        private void ForceInitialSpawnCheck()
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
                if (!data.m_isSpawned && currentCamX <= data.m_spawnTriggerX)
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

            Debug.Log($"{data.m_assetKey} (카메라위치: {m_cameraTransform.position.x})");
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