using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Domain.World
{
    internal class PathNode
    {
        public int X;
        public int Y;
        public int GCost;
        public int HCost;
        public PathNode Parent;
        public int FCost => GCost + HCost;

        public PathNode(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// WorldGridDomain 기반의 8방향 A* 최단 경로 탐색기 (순수 C# 알고리즘)
    /// </summary>
    public class WorldPathfinder
    {
        private static readonly int[] s_dirX = { 0, 0, -1, 1, -1, 1, -1, 1 };
        private static readonly int[] s_dirY = { 1, -1, 0, 0, 1, 1, -1, -1 };

        public List<Vector2Int> FindPath(Vector2Int startCell, Vector2Int targetCell, WorldGridDomain gridDomain, IAreaAccessPolicy accessPolicy = null)
        {
            if (gridDomain == null) return new List<Vector2Int>();

            // 출발/목표 셀이 만약 육지라면 주변 가장 가까운 물 타일로 스냅
            Vector2Int validStart = FindNearestWalkableWater(startCell, gridDomain, 15);
            Vector2Int validTarget = FindNearestWalkableWater(targetCell, gridDomain, 15);

            if (!gridDomain.IsWalkable(validStart) || !gridDomain.IsWalkable(validTarget))
            {
                Debug.LogWarning($"[WorldPathfinder] 경로 탐색 실패: 출발지({validStart}) 또는 목적지({validTarget})가 항해 불가능한 육지입니다.");
                return new List<Vector2Int>();
            }

            // 목적지 지역의 접근 해금 여부 검사
            if (accessPolicy != null && gridDomain.TryGetCell(validTarget, out var targetCellData))
            {
                if (!accessPolicy.CanEnterArea(targetCellData.AreaId, out string lockedReason))
                {
                    Debug.LogWarning($"[WorldPathfinder] 목적지 지역({targetCellData.AreaId})이 잠겨 있어 이동할 수 없습니다. 사유: {lockedReason}");
                    return new List<Vector2Int>();
                }
            }

            var openList = new List<PathNode>();
            var closedList = new HashSet<Vector2Int>();
            var allNodes = new Dictionary<Vector2Int, PathNode>();

            PathNode startNode = new PathNode(validStart.x, validStart.y);
            openList.Add(startNode);
            allNodes[validStart] = startNode;

            while (openList.Count > 0)
            {
                PathNode currentNode = openList[0];
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].FCost < currentNode.FCost ||
                       (openList[i].FCost == currentNode.FCost && openList[i].HCost < currentNode.HCost))
                    {
                        currentNode = openList[i];
                    }
                }

                openList.Remove(currentNode);
                closedList.Add(new Vector2Int(currentNode.X, currentNode.Y));

                // 목적지 도착!
                if (currentNode.X == validTarget.x && currentNode.Y == validTarget.y)
                {
                    var resultPath = RetracePath(startNode, currentNode);
                    Debug.Log($"<color=#3b82f6>[World-Path] 🗺️ 해로 탐색 완료!</color> 출발: ({validStart.x}, {validStart.y}) ➔ 도착: ({validTarget.x}, {validTarget.y}) | 총 <b>{resultPath.Count}</b>개 그리드 셀 경유");
                    return resultPath;
                }

                // 8방향 이웃 노드 탐색
                for (int i = 0; i < 8; i++)
                {
                    Vector2Int neighborPos = new Vector2Int(currentNode.X + s_dirX[i], currentNode.Y + s_dirY[i]);
                    if (closedList.Contains(neighborPos)) continue;

                    if (!gridDomain.IsWalkable(neighborPos)) continue;

                    // 이동 중 잠긴 지역 통과 차단
                    if (accessPolicy != null && gridDomain.TryGetCell(neighborPos, out var neighborCell))
                    {
                        if (!accessPolicy.CanEnterArea(neighborCell.AreaId, out _)) continue;
                    }

                    if (!allNodes.TryGetValue(neighborPos, out PathNode neighborNode))
                    {
                        neighborNode = new PathNode(neighborPos.x, neighborPos.y);
                        allNodes[neighborPos] = neighborNode;
                    }

                    int tentativeGCost = currentNode.GCost + GetDistance(currentNode, neighborNode);

                    if (tentativeGCost < neighborNode.GCost || !openList.Contains(neighborNode))
                    {
                        neighborNode.GCost = tentativeGCost;
                        neighborNode.HCost = GetDistance(neighborNode, new PathNode(validTarget.x, validTarget.y));
                        neighborNode.Parent = currentNode;

                        if (!openList.Contains(neighborNode))
                        {
                            openList.Add(neighborNode);
                        }
                    }
                }
            }

            Debug.LogWarning("[WorldPathfinder] A* 탐색 실패: 목적지까지 도달 가능한 해로가 장애물로 막혀 있습니다.");
            return new List<Vector2Int>();
        }

        private Vector2Int FindNearestWalkableWater(Vector2Int origin, WorldGridDomain grid, int maxRadius)
        {
            if (grid.IsWalkable(origin)) return origin;

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        if (Mathf.Abs(x) == r || Mathf.Abs(y) == r)
                        {
                            Vector2Int checkPos = new Vector2Int(origin.x + x, origin.y + y);
                            if (grid.IsWalkable(checkPos)) return checkPos;
                        }
                    }
                }
            }
            return origin;
        }

        private int GetDistance(PathNode nodeA, PathNode nodeB)
        {
            int dstX = Mathf.Abs(nodeA.X - nodeB.X);
            int dstY = Mathf.Abs(nodeA.Y - nodeB.Y);

            if (dstX > dstY) return 14 * dstY + 10 * (dstX - dstY);
            return 14 * dstX + 10 * (dstY - dstX);
        }

        private List<Vector2Int> RetracePath(PathNode startNode, PathNode endNode)
        {
            var path = new List<Vector2Int>();
            PathNode current = endNode;

            while (current != startNode)
            {
                path.Add(new Vector2Int(current.X, current.Y));
                current = current.Parent;
            }
            path.Reverse();
            return path;
        }
    }
}
