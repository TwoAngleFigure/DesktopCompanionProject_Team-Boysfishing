using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Core;
using DesktopCompanion.Systems;
using DesktopCompanion.Domain.World;
using DesktopCompanion.Data;
using DesktopCompanion.Controllers;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 도메인 계층의 이벤트를 구독하여 화면 암전(Fade) 없이 오브젝트 큐(Queue) 방식으로 배경/프랍을 순환시키는 뷰 컴포넌트
    /// 팝인(Pop-in) 원천 차단 및 항구 씬 / 일반 수면 정박을 완벽 분기
    /// </summary>
    public class StageCycleWorldView : WorldViewBase
    {
        [Header("배 컨트롤러 연결 (선택)")]
        public ShipController m_shipController;

        [Header("레이어 컨테이너 (Near / Mid / Far)")]
        public Transform m_nearLayer;
        public Transform m_midLayer;
        public Transform m_farLayer;

        [Header("팀 확정 패럴랙스 계수")]
        [Range(0f, 1f)] public float m_nearParallax = 0.0f;
        [Range(0f, 1f)] public float m_midParallax = 0.35f;
        [Range(0f, 1f)] public float m_farParallax = 0.65f;

        [Header("디지털식(연속 Depth) 패럴랙스 설정")]
        [Tooltip("체크 시 3단 레이어 분리 없이 Z좌표에 따라 연속적인 시차를 자동 연산합니다.")]
        public bool m_useContinuousDepthParallax = true;
        [Tooltip("시차 계산의 기준 깊이 상수 (Z = 14일 때 약 0.35 시차 형성)")]
        public float m_referenceDepth = 26f;

        private WorldSystemBridge m_worldBridge;
        private Transform m_mainCameraTransform;

        // 현재 활성화된 프랍 인스턴스들
        private readonly List<GameObject> m_activeProps = new List<GameObject>();
        private readonly Queue<string> m_pendingAssetQueue = new Queue<string>();

        private int m_currentRenderedAreaId = 0;
        private bool m_isTransitionPending = false;

        public override void Bind()
        {
            if (Camera.main != null)
            {
                m_mainCameraTransform = Camera.main.transform;
            }

            m_worldBridge = SystemManager?.GetSystem<WorldSystemBridge>();
            if (m_worldBridge != null)
            {
                m_worldBridge.OnAreaChanged += HandleAreaChanged;
                m_worldBridge.OnVoyageStateChanged += HandleVoyageStateChanged;
                m_worldBridge.OnAnchored += HandleAnchored;

                if (m_worldBridge.CurrentStageData != null)
                {
                    m_currentRenderedAreaId = m_worldBridge.CurrentStageData.ID;
                    LoadInitialAreaProps(m_currentRenderedAreaId);
                }
            }
            else
            {
                var stageSystem = SystemManager?.GetSystem<StageSystem>();
                if (stageSystem != null)
                {
                    stageSystem.OnAreaChanged += HandleAreaChanged;
                    stageSystem.OnVoyageStateChanged += HandleVoyageStateChanged;
                    stageSystem.OnAnchored += HandleAnchored;

                    if (stageSystem.CurrentStageData != null)
                    {
                        m_currentRenderedAreaId = stageSystem.CurrentStageData.ID;
                        LoadInitialAreaProps(m_currentRenderedAreaId);
                    }
                }
            }
        }

        public override void Unbind()
        {
            if (m_worldBridge != null)
            {
                m_worldBridge.OnAreaChanged -= HandleAreaChanged;
                m_worldBridge.OnVoyageStateChanged -= HandleVoyageStateChanged;
                m_worldBridge.OnAnchored -= HandleAnchored;
            }

            var stageSystem = SystemManager?.GetSystem<StageSystem>();
            if (stageSystem != null)
            {
                stageSystem.OnAreaChanged -= HandleAreaChanged;
                stageSystem.OnVoyageStateChanged -= HandleVoyageStateChanged;
                stageSystem.OnAnchored -= HandleAnchored;
            }

            ClearAllProps();
        }

        private void Update()
        {
            if (m_mainCameraTransform == null)
            {
                if (Camera.main != null) m_mainCameraTransform = Camera.main.transform;
                else return;
            }

            float camX = m_mainCameraTransform.position.x;
            UpdateParallaxLayers(camX);

            // 배 이동 애니메이션 동기화
            if (m_shipController == null)
            {
                m_shipController = FindAnyObjectByType<ShipController>();
            }

            if (m_shipController != null)
            {
                bool isTraveling = m_worldBridge != null ? m_worldBridge.IsTraveling : (SystemManager?.GetSystem<StageSystem>()?.IsTraveling ?? false);
                float speed = m_worldBridge != null ? m_worldBridge.VoyageEngine.CurrentSpeed : (SystemManager?.GetSystem<StageSystem>()?.CurrentSpeed ?? 0f);

                m_shipController.SetTraveling(isTraveling);
                m_shipController.m_speed = speed;
            }

            // 시야 밖으로 완전히 벗어난 프랍 소멸 및 큐 교체 검사
            CheckAndCleanupProps(camX);
        }

        private void UpdateParallaxLayers(float camX)
        {
            if (m_useContinuousDepthParallax)
            {
                // 디지털식(연속 Depth) 패럴랙스: 활성 프랍들의 Z 깊이에 따라 유기적으로 시차 적용
                for (int i = 0; i < m_activeProps.Count; i++)
                {
                    var prop = m_activeProps[i];
                    if (prop == null) continue;

                    float zDepth = Mathf.Max(0f, prop.transform.position.z);
                    float parallaxFactor = CalculateContinuousParallax(zDepth);

                    // X 위치 = 기본 시작 X + (카메라 이동량 * 패럴랙스 계수)
                    float targetX = camX * parallaxFactor;
                    prop.transform.position = new Vector3(targetX, prop.transform.position.y, prop.transform.position.z);
                }
            }
            else
            {
                // 기존 레거시 3단 레이어 분리 방식 호환 유지
                if (m_nearLayer) m_nearLayer.position = new Vector3(camX * m_nearParallax, 0f, m_nearLayer.position.z);
                if (m_midLayer) m_midLayer.position = new Vector3(camX * m_midParallax, 0f, m_midLayer.position.z);
                if (m_farLayer) m_farLayer.position = new Vector3(camX * m_farParallax, 0f, m_farLayer.position.z);
            }
        }

        /// <summary>
        /// Z 깊이(Depth)로부터 시차 비율(Parallax Factor)을 연속 계산하는 디지털 함수
        /// </summary>
        private float CalculateContinuousParallax(float zDepth)
        {
            if (zDepth <= 3f) return 0.0f; // Near 레이어 구간 (완전 고정)
            float factor = 1.0f - (m_referenceDepth / (m_referenceDepth + zDepth));
            return Mathf.Clamp(factor, 0f, 0.95f);
        }

        private void HandleAreaChanged(int newAreaId, EnvironmentType envType)
        {
            if (newAreaId == m_currentRenderedAreaId) return;

            Debug.Log($"<color=#ec4899>[World-View] 📦 뷰 지역 큐 전환 감지!</color> [Area {m_currentRenderedAreaId}] ➔ <b>[Area {newAreaId}]</b> | 이전 프랍 퇴장 후 신규 지형 유입 대기");
            m_currentRenderedAreaId = newAreaId;
            m_isTransitionPending = true;

            PreparePendingQueue(newAreaId);
        }

        private void HandleVoyageStateChanged(VoyageState state)
        {
            // 항해 상태머신에 따른 뷰 피드백
        }

        private void HandleAnchored(AnchorType anchorType, WorldCellData cell)
        {
            Debug.Log($"[StageCycleWorldView] 정박 이벤트 수신: {anchorType} (Port:{cell.PortId})");

            if (anchorType == AnchorType.DestinationPort)
            {
                // 공식 지정 항구 정박 시 항구 고유 정박 씬 정렬 배치
                LoadPortAnchorScene(cell.AreaId);
            }
            else
            {
                // 바다/강 임의 지점 또는 유저 중단 정박 시: 기존 수면 프랍 상태를 온전히 유지
            }
        }

        private void PreparePendingQueue(int areaId)
        {
            m_pendingAssetQueue.Clear();
            var stageData = GetStageData(areaId);
            if (stageData != null && !string.IsNullOrEmpty(stageData.AssetKey))
            {
                m_pendingAssetQueue.Enqueue(stageData.AssetKey);
            }
        }

        private void CheckAndCleanupProps(float camX)
        {
            float halfWidth = 20f;
            if (Camera.main != null)
            {
                halfWidth = Camera.main.orthographicSize * Camera.main.aspect;
            }

            for (int i = m_activeProps.Count - 1; i >= 0; i--)
            {
                var prop = m_activeProps[i];
                if (prop == null)
                {
                    m_activeProps.RemoveAt(i);
                    continue;
                }

                // 화면 우측 밖으로 완전히 벗어난 프랍 안전 소멸
                float propCamRelativeX = prop.transform.position.x - camX;
                if (propCamRelativeX > halfWidth + 25f)
                {
                    Destroy(prop);
                    m_activeProps.RemoveAt(i);
                }
            }

            // 이전 지역의 프랍이 모두 퇴장하고 대기 큐가 준비된 경우, 새 지역 프랍 유입 시작
            if (m_isTransitionPending && m_activeProps.Count == 0)
            {
                m_isTransitionPending = false;
                SpawnNextQueueProps();
            }
        }

        private void SpawnNextQueueProps()
        {
            while (m_pendingAssetQueue.Count > 0)
            {
                string assetKey = m_pendingAssetQueue.Dequeue();
                SpawnPropAsset(assetKey, isLeftEntry: true);
            }
        }

        private void LoadInitialAreaProps(int areaId)
        {
            ClearAllProps();
            var stageData = GetStageData(areaId);
            if (stageData != null && !string.IsNullOrEmpty(stageData.AssetKey))
            {
                SpawnPropAsset(stageData.AssetKey, isLeftEntry: false);
            }
        }

        private StageData GetStageData(int areaId)
        {
            if (m_worldBridge != null) return m_worldBridge.GetStageData(areaId);
            return SystemManager?.GetSystem<StageSystem>()?.GetStageData(areaId);
        }

        private void LoadPortAnchorScene(int areaId)
        {
            // 항구 정박 전용 오브젝트 배치
            LoadInitialAreaProps(areaId);
        }

        private void SpawnPropAsset(string assetKey, bool isLeftEntry)
        {
            if (AssetProvider != null && AssetProvider.TryGet<GameObject>(assetKey, out var prefab))
            {
                Transform parent = m_midLayer != null ? m_midLayer : transform;
                GameObject instance = Instantiate(prefab, parent);

                if (isLeftEntry && m_mainCameraTransform != null)
                {
                    float halfWidth = Camera.main != null ? Camera.main.orthographicSize * Camera.main.aspect : 15f;
                    instance.transform.position = new Vector3(m_mainCameraTransform.position.x - halfWidth - 5f, 0f, parent.position.z);
                }

                m_activeProps.Add(instance);
            }
        }

        private void ClearAllProps()
        {
            for (int i = 0; i < m_activeProps.Count; i++)
            {
                if (m_activeProps[i] != null) Destroy(m_activeProps[i]);
            }
            m_activeProps.Clear();
            m_pendingAssetQueue.Clear();
            m_isTransitionPending = false;
        }
    }
}
