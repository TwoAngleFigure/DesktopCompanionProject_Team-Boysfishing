using System;
using UnityEngine;
using DesktopCompanion.Systems;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    public class VoyageSystem : SystemBase, ITickable
    {
        private VoyageState m_currentState = VoyageState.Anchored;

        public VoyageState CurrentState => m_currentState;

        private float m_currentSpeed = 0f;
        private float m_maxSpeed = 5f;
        private const float DEPARTURE_TIME = 21.0f;
        private const float ARRIVAL_TIME = 21.0f;
        private const float STOPPING_TIME = 9.0f; // 🎯 원래 감속 시간인 9초로 복원
        private int m_pathIndex = 0;

        // 논리적 좌표 및 경로
        private Vector2 m_currentLogicalPosition;
        private List<Vector2> m_currentPath = new List<Vector2>();

        // 지역(Biome) 상태
        private BiomeType m_currentBiome = BiomeType.None;

        // UI 중계용 시간 및 거리 트래킹 변수 추가
        private float m_remainingTravelTime = 0f;
        private float m_totalTravelTime = 0f;
        private float m_totalPathDistance = 0f;
        public bool IsCanceled { get; private set; } = false;

        // View들이 구독할 이벤트들 ✅
        public event Action<VoyageState> OnVoyageStateChanged;
        public event Action<float> OnSpeedChanged;
        public event Action<BiomeType> OnBiomeChanged;

        // UI에서 읽어갈 Getter들 추가
        public float MaxSpeed => m_maxSpeed;
        public float RemainingTravelTime => m_remainingTravelTime;
        public Vector2 CurrentLogicalPosition => m_currentLogicalPosition;
        public float TravelProgress => (m_currentState != VoyageState.Anchored && m_totalTravelTime > 0f) ? (1f - (m_remainingTravelTime / m_totalTravelTime)) : 0f;
        public float RemainingDistance
        {
            get
            {
                if (m_currentPath == null || m_pathIndex >= m_currentPath.Count) return 0f;
                float dist = Vector2.Distance(m_currentLogicalPosition, m_currentPath[m_pathIndex]);
                for (int i = m_pathIndex; i < m_currentPath.Count - 1; i++)
                {
                    dist += Vector2.Distance(m_currentPath[i], m_currentPath[i + 1]);
                }
                return dist;
            }
        }

        public override void Initialize() { }

        // 초기 위치 세팅용
        public void SetLogicalPosition(Vector2 pos)
        {
            m_currentLogicalPosition = pos;
        }

        // 항해 취소(브레이크) 기능 추가
        public void CancelVoyage()
        {
            if (m_currentState == VoyageState.Anchored) return;
            IsCanceled = true;
            ChangeState(VoyageState.Stopping);
        }

        // 목적지를 받아 항해 시작 (시간 파라미터 추가)
        public void StartVoyage(List<Vector2> path, float totalTime)
        {
            m_currentPath = path;
            m_pathIndex = 0;
            m_totalTravelTime = totalTime;
            m_remainingTravelTime = totalTime;
            IsCanceled = false;

            // 전체 물리 거리 계산
            m_totalPathDistance = 0f;
            if (m_currentPath != null && m_currentPath.Count > 0)
            {
                Vector2 prev = m_currentLogicalPosition;
                for (int i = 0; i < m_currentPath.Count; i++)
                {
                    m_totalPathDistance += Vector2.Distance(prev, m_currentPath[i]);
                    prev = m_currentPath[i];
                }
            }

            m_currentSpeed = 0f; // 🎯 새 항해 시작 시 이전 중단 속도 잔여 영향 차단 (0부터 출발)
            ChangeState(VoyageState.Departing);
        }

        public void Tick(float dt)
        {
            if (m_currentState == VoyageState.Anchored) return;

            // 남은 시간 계산 로직 추가
            if (m_currentState == VoyageState.Departing || m_currentState == VoyageState.Traveling || m_currentState == VoyageState.Arriving)
            {
                m_remainingTravelTime -= dt;
                if (m_remainingTravelTime <= 0f) m_remainingTravelTime = 0f;
            }

            UpdateSpeedLogic(dt);
            OnSpeedChanged?.Invoke(m_currentSpeed);

            // 실제 논리적 좌표 이동 로직
            if (m_currentPath != null && m_pathIndex < m_currentPath.Count)
            {
                Vector2 targetWaypoint = m_currentPath[m_pathIndex];

                // 🎯 100% 순수 물리 이동: 위치 강제 대입 없이 오직 MoveTowards로만 연속 이동!
                m_currentLogicalPosition = Vector2.MoveTowards(m_currentLogicalPosition, targetWaypoint, m_currentSpeed * dt);

                bool isFinalNode = (m_pathIndex == m_currentPath.Count - 1);

                // MoveTowards 특성상 목표 좌표에 다다르면 Distance가 0.001f 이내로 일치하게 됨
                if (Vector2.Distance(m_currentLogicalPosition, targetWaypoint) <= 0.001f)
                {
                    if (!isFinalNode)
                    {
                        m_pathIndex++;
                    }
                    else
                    {
                        // 🎯 강제 대입 0%! 오직 MoveTowards 결과로 노드 정중앙에 100% 안착한 프레임에 정박!
                        m_currentSpeed = 0f;
                        ChangeState(VoyageState.Anchored);
                    }
                }

                // 지역(Biome) 변경 감지
                CheckBiomeChange();
            }

            // 경로의 마지막 인덱스에 도달하거나, 남은 거리/시간이 도착 감속 구간에 다다르면 도착 연출 시작
            if (m_currentState == VoyageState.Traveling)
            {
                float decelDistance = m_maxSpeed * ARRIVAL_TIME * 0.5f; // 🎯 감속 구간 거리 (22.5 unit)
                float remDist = RemainingDistance;

                if ((m_currentPath != null && m_pathIndex >= m_currentPath.Count) || remDist <= decelDistance || m_remainingTravelTime <= ARRIVAL_TIME)
                {
                    ChangeState(VoyageState.Arriving);
                }
            }
        }

        private void CheckBiomeChange()
        {
            var mapManager = SystemManager.GetSystem<MapSystem>();
            if (mapManager == null) return;

            BiomeType detectedBiome = mapManager.GetBiomeAt(m_currentLogicalPosition);

            if (detectedBiome != m_currentBiome)
            {
                m_currentBiome = detectedBiome;
                OnBiomeChanged?.Invoke(m_currentBiome); // 배경 바꾸라고 View에게 방송!
            }
        }

        private void ChangeState(VoyageState newState)
        {
            m_currentState = newState;
            if (m_currentState == VoyageState.Anchored)
            {
                m_remainingTravelTime = 0f;
                m_totalTravelTime = 0f;
            }
            OnVoyageStateChanged?.Invoke(m_currentState);
        }

        private void UpdateSpeedLogic(float dt)
        {
            if (m_currentState == VoyageState.Departing)
            {
                m_currentSpeed += (m_maxSpeed / DEPARTURE_TIME) * dt;

                if (m_currentSpeed >= m_maxSpeed)
                {
                    m_currentSpeed = m_maxSpeed;
                    ChangeState(VoyageState.Traveling);
                }
            }
            else if (m_currentState == VoyageState.Traveling)
            {
                m_currentSpeed = m_maxSpeed;
            }
            else if (m_currentState == VoyageState.Arriving)
            {
                // 🎯 시각적으로 확연히 체감되는 거리 비례 부드러운 감속 커브 적용
                float decelDistance = m_maxSpeed * ARRIVAL_TIME * 0.5f; // 22.5 unit
                float remDist = RemainingDistance;

                float ratio = (decelDistance > 0f) ? Mathf.Clamp01(remDist / decelDistance) : 0f;

                // 🎯 최저 안착 속도(1.5f) 보장: 속도가 무한히 0으로 줄어들지 않고,
                // 1.5f 속도로 목적지 노드 정중앙(targetWaypoint)까지 MoveTowards가 깔끔히 밀고 들어갈 수 있게 함!
                float calculatedSpeed = m_maxSpeed * ratio;
                m_currentSpeed = Mathf.Max(calculatedSpeed, 1.5f);

                // 오직 MoveTowards 결과로 목적지 노드 정중앙(0.0001f 미만)을 완전히 밟은 순간 정박!
                if (remDist <= 0.0001f)
                {
                    m_currentSpeed = 0f;
                    ChangeState(VoyageState.Anchored);
                }
            }
            else if (m_currentState == VoyageState.Stopping)
            {
                // 🎯 중단 시 부드럽게 감속 브레이크 후 정박
                m_currentSpeed -= (m_maxSpeed / STOPPING_TIME) * dt;

                if (m_currentSpeed <= 0f)
                {
                    m_currentSpeed = 0f;
                    ChangeState(VoyageState.Anchored);
                }
            }
        }
    }
}