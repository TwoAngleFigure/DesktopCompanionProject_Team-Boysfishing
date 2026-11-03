using UnityEngine;

namespace DesktopCompanion.Domain.World
{
    /// <summary>
    /// 월드맵의 2차원 그리드 셀 데이터를 관리하고 O(1) 질의 및 좌표 변환을 전담하는 순수 C# 도메인 엔진
    /// </summary>
    public class WorldGridDomain
    {
        private WorldCellData[,] m_grid;
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
        public int ConfirmedAreaId => m_confirmedAreaId;

        public void Initialize(int width, int height, float cellSize, Vector2 worldOrigin, WorldCellData[,] gridData, int initialAreaId = 0)
        {
            m_gridWidth = width;
            m_gridHeight = height;
            m_cellSize = cellSize > 0f ? cellSize : 1f;
            m_worldOrigin = worldOrigin;
            m_grid = gridData;

            m_confirmedAreaId = initialAreaId;
            m_candidateAreaId = initialAreaId;
            m_candidateConsecutiveCount = 0;
        }

        /// <summary>
        /// 월드 물리 좌표(Vector2)를 그리드 정수 좌표(Vector2Int)로 O(1) 변환
        /// </summary>
        public Vector2Int WorldToCell(Vector2 worldPos)
        {
            if (m_cellSize <= 0f) return Vector2Int.zero;

            int x = Mathf.FloorToInt((worldPos.x - m_worldOrigin.x) / m_cellSize);
            int y = Mathf.FloorToInt((worldPos.y - m_worldOrigin.y) / m_cellSize);

            x = Mathf.Clamp(x, 0, m_gridWidth - 1);
            y = Mathf.Clamp(y, 0, m_gridHeight - 1);

            return new Vector2Int(x, y);
        }

        /// <summary>
        /// 그리드 정수 좌표(Vector2Int)의 셀 정중앙 월드 물리 좌표(Vector2)로 O(1) 변환
        /// </summary>
        public Vector2 CellToWorld(Vector2Int cellCoord)
        {
            float worldX = m_worldOrigin.x + (cellCoord.x + 0.5f) * m_cellSize;
            float worldY = m_worldOrigin.y + (cellCoord.y + 0.5f) * m_cellSize;
            return new Vector2(worldX, worldY);
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
            if (TryGetCell(coord, out var cell))
            {
                return cell.IsNavigable;
            }
            return false;
        }

        /// <summary>
        /// 완충 히스테리시스 필터를 적용하여 영역 전환 여부를 안정적으로 판정
        /// 경계선 부근에서 좌우로 미세하게 흔들릴 때 이벤트가 폭주하는 버그를 100% 방지
        /// </summary>
        /// <param name="rawAreaId">현재 밟은 셀의 소속 AreaId</param>
        /// <param name="stableAreaId">안정적으로 확정된 AreaId</param>
        /// <returns>최종 확정 영역이 이전과 다르게 변경되었는지 여부</returns>
        public bool EvaluateAreaWithHysteresis(int rawAreaId, out int stableAreaId)
        {
            if (rawAreaId == m_confirmedAreaId)
            {
                // 이미 확정된 영역과 일치하면 후보 카운트 리셋
                m_candidateAreaId = rawAreaId;
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

            // 임계치(2회 이상 연속) 도달 시 비로소 새 영역으로 공식 인정
            if (m_candidateConsecutiveCount >= HYSTERESIS_THRESHOLD)
            {
                m_confirmedAreaId = m_candidateAreaId;
                m_candidateConsecutiveCount = 0;
                stableAreaId = m_confirmedAreaId;
                return true; // 영역 변경 공식 승인!
            }

            stableAreaId = m_confirmedAreaId;
            return false;
        }
    }
}
