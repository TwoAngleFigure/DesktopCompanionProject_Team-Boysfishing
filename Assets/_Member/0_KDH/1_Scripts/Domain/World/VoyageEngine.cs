using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Domain.World
{
    /// <summary>
    /// 배의 항해 상태머신, 연속 논리 이동 및 프레임 틱 연산을 총괄하는 순수 C# 도메인 항해 엔진
    /// </summary>
    public class VoyageEngine : IWorldDomainEvents
    {
        public event Action<WorldCellData> OnCellEntered;
        public event Action<int, EnvironmentType> OnAreaChanged;
        public event Action<VoyageState> OnVoyageStateChanged;
        public event Action<AnchorType, WorldCellData> OnAnchored;

        public VoyageState CurrentState { get; private set; } = VoyageState.Anchored;
        public Vector2 CurrentLogicalPosition { get; private set; }
        public WorldCellData CurrentCell { get; private set; }

        public float MaxSpeed { get; set; } = 5f;
        public float CurrentSpeed => m_currentSpeed;
        public float RemainingTravelTime => m_remainingTravelTime;
        public float TotalTravelTime => m_totalTravelTime;
        public float TravelProgress => (CurrentState != VoyageState.Anchored && m_totalTravelTime > 0f) ? Mathf.Clamp01(1f - (m_remainingTravelTime / m_totalTravelTime)) : 0f;

        private float m_currentSpeed = 0f;
        private float m_departureDuration = 5f; // 가속 시간
        private float m_arrivalDuration = 5f;   // 감속 시간
        private float m_stoppingDuration = 3f;  // 중단 시간

        private float m_stateTimer = 0f;
        private float m_remainingTravelTime = 0f;
        private float m_totalTravelTime = 0f;

        private List<Vector2> m_activeWaypoints = new List<Vector2>();
        private int m_waypointIndex = 0;

        public void SetInitialPosition(Vector2 worldPos, WorldGridDomain gridDomain)
        {
            CurrentLogicalPosition = worldPos;
            Vector2Int coord = gridDomain.WorldToCell(worldPos);
            if (gridDomain.TryGetCell(coord, out var cell))
            {
                CurrentCell = cell;
            }
            m_currentSpeed = 0f;
            CurrentState = VoyageState.Anchored;
        }

        public void ConfigureTimings(float departureTime, float arrivalTime, float stoppingTime, float maxSpeed)
        {
            m_departureDuration = Mathf.Max(0.1f, departureTime);
            m_arrivalDuration = Mathf.Max(0.1f, arrivalTime);
            m_stoppingDuration = Mathf.Max(0.1f, stoppingTime);
            MaxSpeed = Mathf.Max(0.5f, maxSpeed);
        }

        public void StartVoyage(List<Vector2Int> cellPath, WorldGridDomain gridDomain)
        {
            if (cellPath == null || cellPath.Count == 0) return;

            m_activeWaypoints.Clear();
            for (int i = 0; i < cellPath.Count; i++)
            {
                m_activeWaypoints.Add(gridDomain.CellToWorld(cellPath[i]));
            }

            m_waypointIndex = 0;

            // 전체 물리 이동 거리 계산
            float totalDistance = 0f;
            Vector2 prev = CurrentLogicalPosition;
            for (int i = 0; i < m_activeWaypoints.Count; i++)
            {
                totalDistance += Vector2.Distance(prev, m_activeWaypoints[i]);
                prev = m_activeWaypoints[i];
            }

            // 가감속 시간을 반영한 총 예상 항해 시간 산정
            float accelDist = 0.5f * MaxSpeed * m_departureDuration;
            float decelDist = 0.5f * MaxSpeed * m_arrivalDuration;
            float cruiseDist = Mathf.Max(0f, totalDistance - accelDist - decelDist);

            m_totalTravelTime = m_departureDuration + (cruiseDist / MaxSpeed) + m_arrivalDuration;
            m_remainingTravelTime = m_totalTravelTime;

            Debug.Log($"<color=#38bdf8>[World-Voyage] 🚀 항해 시작!</color> 총거리: {totalDistance:F1}m | 예상시간: {m_totalTravelTime:F1}s | 최고속도: {MaxSpeed:F1} | 경로점: {m_activeWaypoints.Count}개");
            ChangeState(VoyageState.Departing);
        }

        public void CancelVoyage()
        {
            if (CurrentState == VoyageState.Anchored || CurrentState == VoyageState.Stopping) return;

            Debug.Log("<color=#f59e0b>[World-Voyage] ⚠️ 유저 항해 중단 요청 수신 -> 감속 정지(Stopping) 진입</color>");
            m_stateTimer = 0f;
            ChangeState(VoyageState.Stopping);
        }

        public void Tick(float dt, WorldGridDomain gridDomain)
        {
            if (CurrentState == VoyageState.Anchored) return;

            // 1. 남은 항해 시간 갱신
            if (CurrentState != VoyageState.Stopping)
            {
                m_remainingTravelTime = Mathf.Max(0f, m_remainingTravelTime - dt);
            }

            // 2. 상태별 속도 커브 연산
            UpdateSpeedLogic(dt);

            // 3. 논리적 좌표 연속 이동 (순수 수학 연산)
            if (m_activeWaypoints != null && m_waypointIndex < m_activeWaypoints.Count)
            {
                Vector2 targetPos = m_activeWaypoints[m_waypointIndex];
                CurrentLogicalPosition = Vector2.MoveTowards(CurrentLogicalPosition, targetPos, m_currentSpeed * dt);

                // 셀 및 영역 진입 판정
                Vector2Int cellCoord = gridDomain.WorldToCell(CurrentLogicalPosition);
                if (cellCoord.x != CurrentCell.X || cellCoord.y != CurrentCell.Y)
                {
                    if (gridDomain.TryGetCell(cellCoord, out var newCell))
                    {
                        CurrentCell = newCell;
                        OnCellEntered?.Invoke(newCell);

                        if (gridDomain.EvaluateAreaWithHysteresis(newCell.AreaId, out int stableAreaId))
                        {
                            Debug.Log($"<color=#10b981>[World-Area] 🌊 소속 지역 변경 확정!</color> [Area {stableAreaId}] (Env: {newCell.EnvType}) | 셀: ({newCell.X}, {newCell.Y})");
                            OnAreaChanged?.Invoke(stableAreaId, newCell.EnvType);
                        }
                    }
                }

                // 웨이포인트 통과 여부 검사
                if (Vector2.Distance(CurrentLogicalPosition, targetPos) <= 0.001f)
                {
                    m_waypointIndex++;
                    if (m_waypointIndex >= m_activeWaypoints.Count)
                    {
                        // 최종 목표 셀 도달 정박
                        CompleteVoyage(isInterrupted: false);
                    }
                }
            }

            // 4. 도착 감속(Arriving) 전이 판정
            if (CurrentState == VoyageState.Traveling && m_remainingTravelTime <= m_arrivalDuration)
            {
                m_stateTimer = 0f;
                ChangeState(VoyageState.Arriving);
            }
        }

        private void UpdateSpeedLogic(float dt)
        {
            m_stateTimer += dt;

            switch (CurrentState)
            {
                case VoyageState.Departing:
                    float departRatio = Mathf.Clamp01(m_stateTimer / m_departureDuration);
                    m_currentSpeed = Mathf.Lerp(0f, MaxSpeed, departRatio);
                    if (departRatio >= 1f)
                    {
                        m_currentSpeed = MaxSpeed;
                        ChangeState(VoyageState.Traveling);
                    }
                    break;

                case VoyageState.Traveling:
                    m_currentSpeed = MaxSpeed;
                    break;

                case VoyageState.Arriving:
                    float arriveRatio = Mathf.Clamp01(m_stateTimer / m_arrivalDuration);
                    m_currentSpeed = Mathf.Lerp(MaxSpeed, 0.5f, arriveRatio);
                    break;

                case VoyageState.Stopping:
                    float stopRatio = Mathf.Clamp01(m_stateTimer / m_stoppingDuration);
                    m_currentSpeed = Mathf.Lerp(MaxSpeed, 0f, stopRatio);
                    if (stopRatio >= 1f || m_currentSpeed <= 0.01f)
                    {
                        CompleteVoyage(isInterrupted: true);
                    }
                    break;
            }
        }

        private void CompleteVoyage(bool isInterrupted)
        {
            m_currentSpeed = 0f;
            m_remainingTravelTime = 0f;
            ChangeState(VoyageState.Anchored);

            AnchorType anchorType;
            if (isInterrupted)
            {
                anchorType = AnchorType.Interrupted;
            }
            else
            {
                anchorType = (CurrentCell.PortId > 0) ? AnchorType.DestinationPort : AnchorType.FreePoint;
            }

            Debug.Log($"<color=#38bdf8>[World-Voyage] ⚓ 항해 완료 및 정박!</color> 형태: {anchorType} | 정박셀: ({CurrentCell.X}, {CurrentCell.Y}) | PortId: {CurrentCell.PortId}");
            OnAnchored?.Invoke(anchorType, CurrentCell);
        }

        private void ChangeState(VoyageState newState)
        {
            var oldState = CurrentState;
            CurrentState = newState;
            m_stateTimer = 0f;
            Debug.Log($"<color=#a855f7>[World-Voyage] 🔄 상태 전이:</color> {oldState} ➔ <b>{newState}</b> (현재속도: {m_currentSpeed:F1})");
            OnVoyageStateChanged?.Invoke(CurrentState);
        }
    }
}
