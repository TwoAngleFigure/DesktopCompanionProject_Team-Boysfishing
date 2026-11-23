using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Domain.World
{
    /// <summary>
    /// 월드맵의 2차원 육각형(Odd-R Pointy-Top Hex) 그리드 셀 데이터를 관리하고
    /// O(1) 질의, 해안선 여유 가중치, 연결 수역(WaterGroupId) 판정을 전담하는 순수 C# 도메인 엔진
    /// </summary>
    public class WorldGridDomain
    {
        // Odd-R 가로 정렬 육각형 그리드의 6방향 이웃 오프셋 테이블
        // 짝수 행 (y % 2 == 0)
        private static readonly Vector2Int[] s_evenRowNeighbors = new Vector2Int[6]
        {
            new Vector2Int(-1,  0), // 좌 (W)
            new Vector2Int(+1,  0), // 우 (E)
            new Vector2Int(-1, +1), // 좌상 (NW)
            new Vector2Int( 0, +1), // 우상 (NE)
            new Vector2Int(-1, -1), // 좌하 (SW)
            new Vector2Int( 0, -1)  // 우하 (SE)
        };

        // 홀수 행 (y % 2 == 1) -> 오른쪽으로 +0.5칸 오프셋된 줄
        private static readonly Vector2Int[] s_oddRowNeighbors = new Vector2Int[6]
        {
            new Vector2Int(-1,  0), // 좌 (W)
            new Vector2Int(+1,  0), // 우 (E)
            new Vector2Int( 0, +1), // 좌상 (NW)
            new Vector2Int(+1, +1), // 우상 (NE)
            new Vector2Int( 0, -1), // 좌하 (SW)
            new Vector2Int(+1, -1)  // 우하 (SE)
        };

        private WorldCellData[,] m_grid;
        private int[,] m_waterGroupIds;
        private byte[,] m_shorePenalty;

        private int m_gridWidth;
        private int m_gridHeight;
        private float m_cellSize;
        private Vector2 m_worldOrigin;

        // 히스테리시스(완충) 필터용 변수
        private int m_confirmedAreaId = 0;
        private int m_candidateAreaId = 0;
        private int m_candidateConsecutiveCount = 0;
        private const int HYSTERESIS_THRESHOLD = 2; // 최소 2셀 연속으로 새 영역을 밟아야 최종 영역 전환 승인

        public int GridWidth => m_gridWidth;
        public int GridHeight => m_gridHeight;
        public float CellSize => m_cellSize;
        public Vector2 WorldOrigin => m_worldOrigin;
        public int ConfirmedAreaId => m_confirmedAreaId;

        public void Initialize(int width, int height, float cellSize, Vector2 worldOrigin, WorldCellData[,] gridData, int initialAreaId = 0)
        {
            m_gridWidth = width;
            m_gridHeight = height;
            m_cellSize = cellSize > 0f ? cellSize : 2f;
            m_worldOrigin = worldOrigin;
            m_grid = gridData;

            m_confirmedAreaId = initialAreaId;
            m_candidateAreaId = initialAreaId;
            m_candidateConsecutiveCount = 0;

            BuildTopologyMetadata();
        }

        /// <summary>
        /// 월드 물리 좌표(Vector2)를 육각형(Odd-R Hex) 그리드 정수 좌표(Vector2Int)로 O(1) 변환
        /// </summary>
        public Vector2Int WorldToCell(Vector2 worldPos)
        {
            if (m_cellSize <= 0f || m_gridWidth <= 0 || m_gridHeight <= 0)
                return Vector2Int.zero;

            int approxY = Mathf.Clamp(Mathf.FloorToInt((worldPos.y - m_worldOrigin.y) / m_cellSize), 0, m_gridHeight - 1);

            Vector2Int bestCell = Vector2Int.zero;
            float bestDistSq = float.MaxValue;

            // 육각형 상하 맞물림 경계를 정확히 판정하기 위해 인접 행(approxY - 1 ~ approxY + 1)의 후보 육각 중심 거리를 비교 (O(1))
            int minY = Mathf.Max(0, approxY - 1);
            int maxY = Mathf.Min(m_gridHeight - 1, approxY + 1);

            for (int y = minY; y <= maxY; y++)
            {
                float rowOffset = ((y & 1) == 1) ? 0.5f * m_cellSize : 0f;
                int approxX = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - m_worldOrigin.x - rowOffset) / m_cellSize), 0, m_gridWidth - 1);

                int minX = Mathf.Max(0, approxX - 1);
                int maxX = Mathf.Min(m_gridWidth - 1, approxX + 1);

                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 center = CellToWorld(new Vector2Int(x, y));
                    float distSq = (worldPos - center).sqrMagnitude;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestCell = new Vector2Int(x, y);
                    }
                }
            }

            return bestCell;
        }

        /// <summary>
        /// 육각형(Odd-R Hex) 그리드 정수 좌표(Vector2Int)의 정중앙 월드 물리 좌표(Vector2)로 O(1) 변환
        /// 홀수 행(y % 2 == 1)은 오른쪽으로 0.5칸 오프셋되어 벌집 격자를 형성합니다.
        /// </summary>
        public Vector2 CellToWorld(Vector2Int cellCoord)
        {
            float rowOffset = ((cellCoord.y & 1) == 1) ? 0.5f * m_cellSize : 0f;
            float worldX = m_worldOrigin.x + (cellCoord.x + 0.5f) * m_cellSize + rowOffset;
            float worldY = m_worldOrigin.y + (cellCoord.y + 0.5f) * m_cellSize;
            return new Vector2(worldX, worldY);
        }

        /// <summary>
        /// 현재 육각형 셀의 6방향 이웃 오프셋 배열을 반환합니다.
        /// </summary>
        public Vector2Int[] GetHexNeighborOffsets(int rowY)
        {
            return ((rowY & 1) == 1) ? s_oddRowNeighbors : s_evenRowNeighbors;
        }

        /// <summary>
        /// 두 육각형(Odd-R) 셀 사이의 정확한 육각 격자 거리(Step 수 * 10)를 계산합니다.
        /// </summary>
        public int GetHexDistanceCost(Vector2Int a, Vector2Int b)
        {
            // Odd-R Offset 좌표를 Axial/Cube 좌표(q, r, s)로 변환
            int aq = a.x - ((a.y - (a.y & 1)) >> 1);
            int ar = a.y;
            int bq = b.x - ((b.y - (b.y & 1)) >> 1);
            int br = b.y;

            int dq = aq - bq;
            int dr = ar - br;
            int ds = -dq - dr;

            int steps = (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) >> 1;
            return steps * 10;
        }

        /// <summary>
        /// 특정 그리드 좌표의 셀 데이터를 O(1)로 취득
        /// </summary>
        public bool TryGetCell(Vector2Int coord, out WorldCellData cell)
        {
            if (m_grid != null && coord.x >= 0 && coord.x < m_gridWidth && coord.y >= 0 && coord.y < m_gridHeight)
            {
                cell = m_grid[coord.x, coord.y];
                return true;
            }

            cell = default;
            return false;
        }

        /// <summary>
        /// 특정 그리드 좌표가 배가 항해 가능한 물 타일인지 검사
        /// </summary>
        public bool IsWalkable(Vector2Int coord)
        {
            if (m_grid != null && coord.x >= 0 && coord.x < m_gridWidth && coord.y >= 0 && coord.y < m_gridHeight)
            {
                return m_grid[coord.x, coord.y].IsNavigable;
            }
            return false;
        }

        /// <summary>
        /// 연결된 물 덩어리 ID를 반환합니다 (0이면 육지, 1 이상이면 같은 번호끼리만 항해 도달 가능).
        /// </summary>
        public int GetWaterGroupId(Vector2Int coord)
        {
            if (m_waterGroupIds != null && coord.x >= 0 && coord.x < m_gridWidth && coord.y >= 0 && coord.y < m_gridHeight)
            {
                return m_waterGroupIds[coord.x, coord.y];
            }
            return 0;
        }

        /// <summary>
        /// 해안선에 너무 바짝 붙지 않도록 부여되는 추가 이동 페널티 비용을 반환합니다.
        /// </summary>
        public int GetShorePenaltyCost(Vector2Int coord)
        {
            if (m_shorePenalty != null && coord.x >= 0 && coord.x < m_gridWidth && coord.y >= 0 && coord.y < m_gridHeight)
            {
                return m_shorePenalty[coord.x, coord.y];
            }
            return 0;
        }

        /// <summary>
        /// 그리드 초기화 시 1회 실행:
        /// 1) 해안선 인접 물 타일에 안전 가중치(Shore Penalty) 부여
        /// 2) 연결된 수역 그룹(WaterGroupId)을 Flood-Fill로 태깅하여 고립된 물 클릭 시 O(1) 즉시 차단 보장
        /// </summary>
        private void BuildTopologyMetadata()
        {
            if (m_grid == null || m_gridWidth <= 0 || m_gridHeight <= 0) return;

            m_waterGroupIds = new int[m_gridWidth, m_gridHeight];
            m_shorePenalty = new byte[m_gridWidth, m_gridHeight];

            // 1단계: 육지와 바로 맞닿은 해안선 1~2칸 물 타일에 안전 가중치 부여 (좁은 수로는 통과하되 넓은 곳에선 해안선 회피)
            for (int y = 0; y < m_gridHeight; y++)
            {
                Vector2Int[] offsets = GetHexNeighborOffsets(y);
                for (int x = 0; x < m_gridWidth; x++)
                {
                    if (!m_grid[x, y].IsNavigable) continue;

                    bool touchesLand = false;
                    for (int i = 0; i < 6; i++)
                    {
                        int nx = x + offsets[i].x;
                        int ny = y + offsets[i].y;
                        if (nx < 0 || nx >= m_gridWidth || ny < 0 || ny >= m_gridHeight || !m_grid[nx, ny].IsNavigable)
                        {
                            touchesLand = true;
                            break;
                        }
                    }

                    if (touchesLand)
                    {
                        m_shorePenalty[x, y] = 12; // 육지 바로 옆 1칸 페널티
                    }
                }
            }

            // 2단계: 연결된 물 그룹(WaterGroupId) BFS 라벨링
            int nextGroupId = 1;
            var queue = new Queue<Vector2Int>(4096);

            for (int y = 0; y < m_gridHeight; y++)
            {
                for (int x = 0; x < m_gridWidth; x++)
                {
                    if (!m_grid[x, y].IsNavigable || m_waterGroupIds[x, y] != 0)
                        continue;

                    int groupId = nextGroupId++;
                    m_waterGroupIds[x, y] = groupId;
                    queue.Enqueue(new Vector2Int(x, y));

                    while (queue.Count > 0)
                    {
                        Vector2Int cur = queue.Dequeue();
                        Vector2Int[] offsets = GetHexNeighborOffsets(cur.y);
                        for (int i = 0; i < 6; i++)
                        {
                            int nx = cur.x + offsets[i].x;
                            int ny = cur.y + offsets[i].y;
                            if (nx >= 0 && nx < m_gridWidth && ny >= 0 && ny < m_gridHeight)
                            {
                                if (m_grid[nx, ny].IsNavigable && m_waterGroupIds[nx, ny] == 0)
                                {
                                    m_waterGroupIds[nx, ny] = groupId;
                                    queue.Enqueue(new Vector2Int(nx, ny));
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 완충 히스테리시스 필터를 적용하여 영역 전환 여부를 안정적으로 판정
        /// 경계선 부근에서 좌우로 미세하게 흔들릴 때 이벤트가 폭주하는 버그를 100% 방지
        /// </summary>
        public bool EvaluateAreaWithHysteresis(int rawAreaId, out int stableAreaId)
        {
            if (rawAreaId <= 0 || rawAreaId == m_confirmedAreaId)
            {
                m_candidateAreaId = m_confirmedAreaId;
                m_candidateConsecutiveCount = 0;
                stableAreaId = m_confirmedAreaId;
                return false;
            }

            if (rawAreaId == m_candidateAreaId)
            {
                m_candidateConsecutiveCount++;
            }
            else
            {
                m_candidateAreaId = rawAreaId;
                m_candidateConsecutiveCount = 1;
            }

            if (m_candidateConsecutiveCount >= HYSTERESIS_THRESHOLD)
            {
                m_confirmedAreaId = m_candidateAreaId;
                m_candidateConsecutiveCount = 0;
                stableAreaId = m_confirmedAreaId;
                return true;
            }

            stableAreaId = m_confirmedAreaId;
            return false;
        }
    }
}
