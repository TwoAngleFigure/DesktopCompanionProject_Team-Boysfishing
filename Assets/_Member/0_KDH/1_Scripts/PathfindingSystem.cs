using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Systems
{
    public class PathNode
    {
        public int x;
        public int y;
        public int gCost;
        public int hCost;
        public PathNode parent;
        public int FCost => gCost + hCost;

        public PathNode(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public class PathfindingSystem : SystemBase
    {
        public override void Initialize() { }

        public List<Vector2> FindPath(Vector2 startWorldPos, Vector2 targetWorldPos)
        {
            var mapManager = SystemManager.GetSystem<MapSystem>();
            if (mapManager == null) return new List<Vector2>();

            Vector2Int startGridRaw = mapManager.WorldToGridPosition(startWorldPos);
            Vector2Int targetGridRaw = mapManager.WorldToGridPosition(targetWorldPos);

            // ✨ 반경 15칸(150px 범위)까지 유연하게 가장 가까운 바다로 스냅 보정
            Vector2Int startGrid = FindNearestWalkableWater(startGridRaw, mapManager, 15);
            Vector2Int targetGrid = FindNearestWalkableWater(targetGridRaw, mapManager, 15);

            Debug.Log($"[PathfindingSystem] 길찾기 시작 - 출발 월드:{startWorldPos} -> 그리드:{startGridRaw}(스냅후:{startGrid}, 바다?:{mapManager.IsWalkable(startGrid)}) | 목적 월드:{targetWorldPos} -> 그리드:{targetGridRaw}(스냅후:{targetGrid}, 바다?:{mapManager.IsWalkable(targetGrid)})");

            if (!mapManager.IsWalkable(targetGrid))
            {
                Debug.LogWarning($"[PathfindingSystem] 목적지({targetGrid})가 갈 수 없는 육지입니다. (원래 grid:{targetGridRaw})");
                return new List<Vector2>();
            }

            if (!mapManager.IsWalkable(startGrid))
            {
                Debug.LogWarning($"[PathfindingSystem] 출발지({startGrid})가 갈 수 없는 육지입니다. (원래 grid:{startGridRaw})");
                return new List<Vector2>();
            }

            List<PathNode> openList = new List<PathNode>();
            HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();
            Dictionary<Vector2Int, PathNode> allNodes = new Dictionary<Vector2Int, PathNode>();

            PathNode startNode = new PathNode(startGrid.x, startGrid.y);
            openList.Add(startNode);
            allNodes.Add(startGrid, startNode);

            while (openList.Count > 0)
            {
                PathNode currentNode = openList[0];
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].FCost < currentNode.FCost ||
                       (openList[i].FCost == currentNode.FCost && openList[i].hCost < currentNode.hCost))
                    {
                        currentNode = openList[i];
                    }
                }

                openList.Remove(currentNode);
                closedList.Add(new Vector2Int(currentNode.x, currentNode.y));

                // 목적지 도착!
                if (currentNode.x == targetGrid.x && currentNode.y == targetGrid.y)
                {
                    return RetracePath(startNode, currentNode, mapManager, targetWorldPos);
                }

                // 8방향 이웃 노드 탐색
                foreach (PathNode neighbor in GetNeighbors(currentNode, mapManager, allNodes))
                {
                    Vector2Int neighborPos = new Vector2Int(neighbor.x, neighbor.y);
                    if (closedList.Contains(neighborPos)) continue;

                    int tentativeGCost = currentNode.gCost + GetDistance(currentNode, neighbor);

                    if (tentativeGCost < neighbor.gCost || !openList.Contains(neighbor))
                    {
                        neighbor.gCost = tentativeGCost;
                        neighbor.hCost = GetDistance(neighbor, new PathNode(targetGrid.x, targetGrid.y));
                        neighbor.parent = currentNode;

                        if (!openList.Contains(neighbor))
                            openList.Add(neighbor);
                    }
                }
            }

            Debug.LogWarning("[PathfindingSystem] A* 탐색 실패! 갈 수 있는 길이 막혀있습니다.");
            return new List<Vector2>();
        }

        // 🚀 [완전히 새로운 방식] 큐(Queue)를 버리고, 동심원 형태로 1칸, 2칸씩 외곽선을 훑으며 가장 가까운 바다를 100% 확실하게 찾아냅니다.
        private Vector2Int FindNearestWalkableWater(Vector2Int gridPos, MapSystem map, int maxRadius)
        {
            // 1. 이미 바다면 그대로 통과
            if (map.IsValidGrid(gridPos) && map.IsWalkable(gridPos))
                return gridPos;

            // 2. 육지라면 중심점에서부터 1칸, 2칸... 사각형 형태로 점점 넓혀가며 탐색
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        // 현재 탐색 반경의 '테두리' 부분만 검사합니다.
                        if (Mathf.Abs(x) == radius || Mathf.Abs(y) == radius)
                        {
                            Vector2Int checkPos = new Vector2Int(gridPos.x + x, gridPos.y + y);

                            if (map.IsValidGrid(checkPos) && map.IsWalkable(checkPos))
                            {
                                Debug.Log($"🛠️ [길찾기 보정] 픽셀 압축 오차 수정: 육지({gridPos}) -> {radius}칸 옆 바다({checkPos})로 스냅!");
                                return checkPos;
                            }
                        }
                    }
                }
            }

            // 지정한 반경(2칸)을 다 뒤져도 바다가 없으면 진짜 내륙 한가운데인 것임
            return gridPos;
        }

        private List<PathNode> GetNeighbors(PathNode node, MapSystem mapManager, Dictionary<Vector2Int, PathNode> allNodes)
        {
            List<PathNode> neighbors = new List<PathNode>();
            int[] dirX = { 0, 0, -1, 1, -1, 1, -1, 1 };
            int[] dirY = { 1, -1, 0, 0, 1, 1, -1, -1 };

            for (int i = 0; i < 8; i++)
            {
                int checkX = node.x + dirX[i];
                int checkY = node.y + dirY[i];
                Vector2Int checkPos = new Vector2Int(checkX, checkY);

                if (mapManager.IsValidGrid(checkPos) && mapManager.IsWalkable(checkPos))
                {
                    if (!allNodes.ContainsKey(checkPos))
                        allNodes.Add(checkPos, new PathNode(checkX, checkY));
                    neighbors.Add(allNodes[checkPos]);
                }
            }
            return neighbors;
        }

        private int GetDistance(PathNode nodeA, PathNode nodeB)
        {
            int dstX = Mathf.Abs(nodeA.x - nodeB.x);
            int dstY = Mathf.Abs(nodeA.y - nodeB.y);

            if (dstX > dstY) return 14 * dstY + 10 * (dstX - dstY);
            return 14 * dstX + 10 * (dstY - dstX);
        }

        private List<Vector2> RetracePath(PathNode startNode, PathNode endNode, MapSystem mapManager, Vector2 targetWorldPos)
        {
            List<Vector2> path = new List<Vector2>();
            PathNode currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(mapManager.GridToWorldPosition(new Vector2Int(currentNode.x, currentNode.y)));
                currentNode = currentNode.parent;
            }
            path.Reverse();

            // 🎯 스냅된 바다 위치에서 실제 목적지 노드 중심 좌표(targetWorldPos)로 마지막 마침표 추가!
            if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], targetWorldPos) > 0.01f)
            {
                path.Add(targetWorldPos);
            }

            return path;
        }
    }
}