using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Domain.World
{
    /// <summary>
    /// WorldGridDomain 기반의 6방향 육각형(Odd-R Hex) + 이진 힙(Min-Heap) A* 최단 경로 탐색기
    /// 2픽셀(1024x512 = 약 52만 셀) 초정밀 그리드에서도 Zero-GC 배열 인덱싱과 수역 연결성(WaterGroupId) O(1) 판정을 통해
    /// 프레임 드랍 없이 즉각적인 해로 탐색을 보장합니다.
    /// </summary>
    public class WorldPathfinder
    {
        private int m_allocatedSize = 0;
        private int m_currentSearchId = 0;

        // 1차원 평탄화 노드 상태 버퍼 (GC Alloc 방지용 재사용 배열)
        private int[] m_gCost;
        private int[] m_hCost;
        private int[] m_parentIndex;
        private int[] m_openSearchId;
        private int[] m_closedSearchId;

        // 이진 최소 힙(Binary Min-Heap) 버퍼
        private int[] m_heap;
        private int[] m_heapPos;
        private int m_heapCount = 0;

        public List<Vector2Int> FindPath(Vector2Int startCell, Vector2Int targetCell, WorldGridDomain gridDomain, IAreaAccessPolicy accessPolicy = null)
        {
            if (gridDomain == null || gridDomain.GridWidth <= 0 || gridDomain.GridHeight <= 0)
                return new List<Vector2Int>();

            int width = gridDomain.GridWidth;
            int height = gridDomain.GridHeight;
            EnsureBuffers(width * height);

            // 1) 출발 셀이 육지라면 가장 가까운 물 타일로 스냅 (2픽셀 그리드 기준 반경 25칸 = 50픽셀 탐색)
            Vector2Int validStart = FindNearestWalkableWater(startCell, gridDomain, 25, requiredWaterGroup: 0);
            int startWaterGroup = gridDomain.GetWaterGroupId(validStart);

            // 2) 목표 셀이 육지라면 출발지와 연결된 동일 수역(startWaterGroup)의 물 타일을 우선 탐색
            Vector2Int validTarget = FindNearestWalkableWater(targetCell, gridDomain, 25, requiredWaterGroup: startWaterGroup);
            if (!gridDomain.IsWalkable(validTarget))
            {
                validTarget = FindNearestWalkableWater(targetCell, gridDomain, 25, requiredWaterGroup: 0);
            }

            if (!gridDomain.IsWalkable(validStart) || !gridDomain.IsWalkable(validTarget))
            {
                Debug.LogWarning($"[WorldPathfinder] 경로 탐색 실패: 출발지({validStart}) 또는 목적지({validTarget})가 항해 불가능한 육지입니다.");
                return new List<Vector2Int>();
            }

            // 3) [핵심 최적화] 연결된 수역 그룹(WaterGroupId) O(1) 사전 검증: 고립된 호수/막힌 지점 클릭 시 전수 탐색 프리징 100% 방지
            int targetWaterGroup = gridDomain.GetWaterGroupId(validTarget);
            if (startWaterGroup > 0 && targetWaterGroup > 0 && startWaterGroup != targetWaterGroup)
            {
                Debug.LogWarning($"[WorldPathfinder] 출발 수역(Group {startWaterGroup})과 목표 수역(Group {targetWaterGroup})이 육지로 단절되어 있어 도달할 수 없습니다.");
                return new List<Vector2Int>();
            }

            // 4) 목적지 지역의 접근 해금 여부 검사
            if (accessPolicy != null && gridDomain.TryGetCell(validTarget, out var targetCellData))
            {
                if (!accessPolicy.CanEnterArea(targetCellData.AreaId, out string lockedReason))
                {
                    Debug.LogWarning($"[WorldPathfinder] 목적지 지역({targetCellData.AreaId})이 잠겨 있어 이동할 수 없습니다. 사유: {lockedReason}");
                    return new List<Vector2Int>();
                }
            }

            if (validStart == validTarget)
            {
                return new List<Vector2Int> { validTarget };
            }

            // 5) 이진 힙(Min-Heap) 기반 6방향 육각형 A* 탐색 시작
            NextSearchId();
            m_heapCount = 0;

            int startIdx = validStart.y * width + validStart.x;
            int targetIdx = validTarget.y * width + validTarget.x;

            m_gCost[startIdx] = 0;
            m_hCost[startIdx] = gridDomain.GetHexDistanceCost(validStart, validTarget);
            m_parentIndex[startIdx] = -1;
            m_openSearchId[startIdx] = m_currentSearchId;
            HeapPush(startIdx);

            while (m_heapCount > 0)
            {
                int currentIdx = HeapPop();
                m_closedSearchId[currentIdx] = m_currentSearchId;

                if (currentIdx == targetIdx)
                {
                    var rawPath = RetracePath(startIdx, targetIdx, width);
                    var simplifiedPath = SimplifyHexPath(rawPath, gridDomain);
                    Debug.Log($"<color=#3b82f6>[World-Path] 🐝 육각(Hex) 2px 해로 탐색 완료!</color> 출발: {validStart} ➔ 도착: {validTarget} | 원본 {rawPath.Count}셀 ➔ 최적화 웨이포인트 <b>{simplifiedPath.Count}</b>개");
                    return simplifiedPath;
                }

                int cx = currentIdx % width;
                int cy = currentIdx / width;
                int currentG = m_gCost[currentIdx];

                // 육각형 그리드 6방향 이웃 탐색 (짝수/홀수 행 오프셋 자동 적용)
                Vector2Int[] hexOffsets = gridDomain.GetHexNeighborOffsets(cy);
                for (int i = 0; i < 6; i++)
                {
                    int nx = cx + hexOffsets[i].x;
                    int ny = cy + hexOffsets[i].y;

                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    int neighborIdx = ny * width + nx;
                    if (m_closedSearchId[neighborIdx] == m_currentSearchId)
                        continue;

                    Vector2Int neighborPos = new Vector2Int(nx, ny);
                    if (!gridDomain.IsWalkable(neighborPos))
                        continue;

                    if (accessPolicy != null && gridDomain.TryGetCell(neighborPos, out var neighborCell))
                    {
                        if (!accessPolicy.CanEnterArea(neighborCell.AreaId, out _))
                            continue;
                    }

                    // 육각형 인접 6방향은 모두 기본 거리 비용 10으로 동일 + 해안선 안전 여유 페널티 합산
                    int stepCost = 10 + gridDomain.GetShorePenaltyCost(neighborPos);
                    int tentativeG = currentG + stepCost;

                    bool isOpen = (m_openSearchId[neighborIdx] == m_currentSearchId);
                    if (!isOpen || tentativeG < m_gCost[neighborIdx])
                    {
                        m_gCost[neighborIdx] = tentativeG;
                        m_hCost[neighborIdx] = gridDomain.GetHexDistanceCost(neighborPos, validTarget);
                        m_parentIndex[neighborIdx] = currentIdx;

                        if (!isOpen)
                        {
                            m_openSearchId[neighborIdx] = m_currentSearchId;
                            HeapPush(neighborIdx);
                        }
                        else
                        {
                            HeapUpdateUp(m_heapPos[neighborIdx]);
                        }
                    }
                }
            }

            Debug.LogWarning("[WorldPathfinder] A* 탐색 실패: 목적지까지 도달 가능한 해로가 장애물로 막혀 있습니다.");
            return new List<Vector2Int>();
        }

        private void EnsureBuffers(int totalCells)
        {
            if (m_allocatedSize >= totalCells) return;

            m_allocatedSize = totalCells;
            m_gCost = new int[totalCells];
            m_hCost = new int[totalCells];
            m_parentIndex = new int[totalCells];
            m_openSearchId = new int[totalCells];
            m_closedSearchId = new int[totalCells];
            m_heap = new int[totalCells + 1];
            m_heapPos = new int[totalCells];
            m_currentSearchId = 0;
        }

        private void NextSearchId()
        {
            m_currentSearchId++;
            if (m_currentSearchId == int.MaxValue)
            {
                System.Array.Clear(m_openSearchId, 0, m_openSearchId.Length);
                System.Array.Clear(m_closedSearchId, 0, m_closedSearchId.Length);
                m_currentSearchId = 1;
            }
        }

        private void HeapPush(int nodeIdx)
        {
            m_heapCount++;
            m_heap[m_heapCount] = nodeIdx;
            m_heapPos[nodeIdx] = m_heapCount;
            HeapUpdateUp(m_heapCount);
        }

        private int HeapPop()
        {
            int rootNode = m_heap[1];
            int lastNode = m_heap[m_heapCount];
            m_heapCount--;

            if (m_heapCount > 0)
            {
                m_heap[1] = lastNode;
                m_heapPos[lastNode] = 1;
                HeapUpdateDown(1);
            }

            return rootNode;
        }

        private void HeapUpdateUp(int heapIdx)
        {
            int nodeIdx = m_heap[heapIdx];
            int nodeF = m_gCost[nodeIdx] + m_hCost[nodeIdx];
            int nodeH = m_hCost[nodeIdx];

            while (heapIdx > 1)
            {
                int parentHeapIdx = heapIdx >> 1;
                int parentNodeIdx = m_heap[parentHeapIdx];
                int parentF = m_gCost[parentNodeIdx] + m_hCost[parentNodeIdx];

                if (nodeF < parentF || (nodeF == parentF && nodeH < m_hCost[parentNodeIdx]))
                {
                    m_heap[heapIdx] = parentNodeIdx;
                    m_heapPos[parentNodeIdx] = heapIdx;
                    heapIdx = parentHeapIdx;
                }
                else
                {
                    break;
                }
            }

            m_heap[heapIdx] = nodeIdx;
            m_heapPos[nodeIdx] = heapIdx;
        }

        private void HeapUpdateDown(int heapIdx)
        {
            int nodeIdx = m_heap[heapIdx];
            int nodeF = m_gCost[nodeIdx] + m_hCost[nodeIdx];
            int nodeH = m_hCost[nodeIdx];

            while ((heapIdx << 1) <= m_heapCount)
            {
                int leftIdx = heapIdx << 1;
                int rightIdx = leftIdx + 1;
                int smallestIdx = leftIdx;

                int leftNode = m_heap[leftIdx];
                int bestF = m_gCost[leftNode] + m_hCost[leftNode];
                int bestH = m_hCost[leftNode];

                if (rightIdx <= m_heapCount)
                {
                    int rightNode = m_heap[rightIdx];
                    int rightF = m_gCost[rightNode] + m_hCost[rightNode];
                    int rightH = m_hCost[rightNode];

                    if (rightF < bestF || (rightF == bestF && rightH < bestH))
                    {
                        smallestIdx = rightIdx;
                        bestF = rightF;
                        bestH = rightH;
                    }
                }

                if (bestF < nodeF || (bestF == nodeF && bestH < nodeH))
                {
                    int childNode = m_heap[smallestIdx];
                    m_heap[heapIdx] = childNode;
                    m_heapPos[childNode] = heapIdx;
                    heapIdx = smallestIdx;
                }
                else
                {
                    break;
                }
            }

            m_heap[heapIdx] = nodeIdx;
            m_heapPos[nodeIdx] = heapIdx;
        }

        private Vector2Int FindNearestWalkableWater(Vector2Int origin, WorldGridDomain grid, int maxRadius, int requiredWaterGroup)
        {
            if (grid.IsWalkable(origin))
            {
                if (requiredWaterGroup <= 0 || grid.GetWaterGroupId(origin) == requiredWaterGroup)
                    return origin;
            }

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        if (Mathf.Abs(x) == r || Mathf.Abs(y) == r)
                        {
                            Vector2Int checkPos = new Vector2Int(origin.x + x, origin.y + y);
                            if (grid.IsWalkable(checkPos))
                            {
                                if (requiredWaterGroup <= 0 || grid.GetWaterGroupId(checkPos) == requiredWaterGroup)
                                    return checkPos;
                            }
                        }
                    }
                }
            }
            return origin;
        }

        private List<Vector2Int> RetracePath(int startIdx, int endIdx, int width)
        {
            var path = new List<Vector2Int>(256);
            int current = endIdx;

            while (current != startIdx && current >= 0)
            {
                path.Add(new Vector2Int(current % width, current / width));
                current = m_parentIndex[current];
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// 2픽셀 초고해상도 육각(Odd-R Hex) 경로에서 홀수 행 오프셋(+0.5칸)으로 인한 미세 지그재그 흔들림과
        /// 불필요한 일직선 중복 셀들을 가시선(Line-of-Sight) 기반으로 매끄럽게 압축하되,
        /// 해역(AreaId) 경계 진입 셀과 곡선 변곡점은 100% 보존하여 부드러운 이동과 정확한 이벤트 발생을 보장합니다.
        /// </summary>
        private List<Vector2Int> SimplifyHexPath(List<Vector2Int> rawPath, WorldGridDomain grid)
        {
            if (rawPath == null || rawPath.Count <= 2)
                return rawPath;

            var result = new List<Vector2Int>(Mathf.Max(16, rawPath.Count / 3));
            result.Add(rawPath[0]);

            int anchorIndex = 0;
            const int MAX_SEGMENT_STEPS = 8; // 최대 8칸(약 16픽셀) 단위로 웨이포인트를 유지하여 히스테리시스 및 곡선 정밀도 보장

            while (anchorIndex < rawPath.Count - 1)
            {
                int furthestVisible = anchorIndex + 1;
                int maxCandidate = Mathf.Min(rawPath.Count - 1, anchorIndex + MAX_SEGMENT_STEPS);

                grid.TryGetCell(rawPath[anchorIndex], out var anchorCell);
                int anchorAreaId = anchorCell.AreaId;

                for (int candidate = anchorIndex + 2; candidate <= maxCandidate; candidate++)
                {
                    grid.TryGetCell(rawPath[candidate], out var candCell);

                    // 해역(AreaId)이 바뀌는 경계 지점은 절대 건너뛰지 않고 즉시 웨이포인트로 보존
                    if (candCell.AreaId != anchorAreaId)
                    {
                        break;
                    }

                    if (HasCleanHexLineOfSight(rawPath[anchorIndex], rawPath[candidate], anchorAreaId, grid))
                    {
                        furthestVisible = candidate;
                    }
                    else
                    {
                        break;
                    }
                }

                result.Add(rawPath[furthestVisible]);
                anchorIndex = furthestVisible;
            }

            return result;
        }

        /// <summary>
        /// 두 육각형 셀의 월드 중심을 잇는 직선 구간이 육지나 해안선 위험 구역, 다른 해역(AreaId)을 침범하지 않는지 검증합니다.
        /// 이를 통해 Odd-R 육각 격자의 수직/사선 이동 시 발생하는 1픽셀 좌우 지그재그를 완벽한 직선으로 펴줍니다.
        /// </summary>
        private bool HasCleanHexLineOfSight(Vector2Int startCoord, Vector2Int endCoord, int expectedAreaId, WorldGridDomain grid)
        {
            Vector2 startWorld = grid.CellToWorld(startCoord);
            Vector2 endWorld = grid.CellToWorld(endCoord);
            float dist = Vector2.Distance(startWorld, endWorld);
            float sampleStep = Mathf.Max(0.5f, grid.CellSize * 0.45f);
            int samples = Mathf.CeilToInt(dist / sampleStep);

            for (int s = 1; s < samples; s++)
            {
                float t = (float)s / samples;
                Vector2 samplePos = Vector2.Lerp(startWorld, endWorld, t);
                Vector2Int cellCoord = grid.WorldToCell(samplePos);

                if (!grid.TryGetCell(cellCoord, out var cellData) || !cellData.IsNavigable)
                    return false;

                if (cellData.AreaId != expectedAreaId)
                    return false;

                if (grid.GetShorePenaltyCost(cellCoord) > 0 && grid.GetShorePenaltyCost(startCoord) == 0 && grid.GetShorePenaltyCost(endCoord) == 0)
                    return false;
            }

            return true;
        }
    }
}
