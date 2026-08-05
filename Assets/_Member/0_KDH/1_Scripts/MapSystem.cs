using DesktopCompanion.Systems;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DesktopCompanion.Systems
{
    public enum BiomeType
    {
        None = 0,
        River = 1,
        ColdSea = 2,
        Mudflat = 3
    }

    public class MapSystem : SystemBase
    {
        private bool[,] m_walkableGrid;
        private BiomeType[,] m_biomeGrid;

        private int m_gridWidth;
        private int m_gridHeight;
        private const int PIXELS_PER_GRID = 10;

        private const float MAP_WIDTH = 2048f;
        private const float MAP_HEIGHT = 1024f;

        public override void Initialize()
        {
            Texture2D walkableMapTex = UnityEngine.Resources.Load<Texture2D>("Map_01");
            if (walkableMapTex != null)
            {
                LoadMapFromTexture(walkableMapTex, null);
            }
        }

        public async Task LoadMapDataAsync(string walkableTexKey, string biomeTexKey)
        {
            AsyncOperationHandle<Texture2D> walkHandle = Addressables.LoadAssetAsync<Texture2D>(walkableTexKey);
            AsyncOperationHandle<Texture2D> biomeHandle = Addressables.LoadAssetAsync<Texture2D>(biomeTexKey);

            await Task.WhenAll(walkHandle.Task, biomeHandle.Task);

            Texture2D walkableMapTex = walkHandle.Result;
            Texture2D biomeMapTex = biomeHandle.Result;

            if (walkableMapTex != null) LoadMapFromTexture(walkableMapTex, biomeMapTex);

            Addressables.Release(walkHandle);
            Addressables.Release(biomeHandle);
        }

        public void LoadMapFromTexture(Texture2D walkableMapTex, Texture2D biomeMapTex)
        {
            m_gridWidth = walkableMapTex.width / PIXELS_PER_GRID;
            m_gridHeight = walkableMapTex.height / PIXELS_PER_GRID;

            m_walkableGrid = new bool[m_gridWidth, m_gridHeight];
            m_biomeGrid = new BiomeType[m_gridWidth, m_gridHeight];

            int texWidth = walkableMapTex.width;
            int texHeight = walkableMapTex.height;

            int seaCount = 0;
            int landCount = 0;

            for (int x = 0; x < m_gridWidth; x++)
            {
                for (int y = 0; y < m_gridHeight; y++)
                {
                    float u = (float)x / (m_gridWidth - 1);
                    float v = (float)y / (m_gridHeight - 1);

                    int pixelX = Mathf.Clamp(Mathf.FloorToInt(u * texWidth), 0, texWidth - 1);
                    int pixelY = Mathf.Clamp(Mathf.FloorToInt(v * texHeight), 0, texHeight - 1);

                    // 🚀 바다 색상 판정
                    Color walkColor = walkableMapTex.GetPixel(pixelX, pixelY);
                    m_walkableGrid[x, y] = walkColor.b > (walkColor.r + 0.15f);

                    if (m_walkableGrid[x, y]) seaCount++;
                    else landCount++;
                }
            }

            Debug.Log($"🗺️ [MapSystem] 맵 스캔 완료! 🌊바다: {seaCount}개 | ⛰️육지: {landCount}개");
        }

        private BiomeType DetermineBiomeByColor(Color color)
        {
            if (color.r > 0.8f && color.g < 0.2f) return BiomeType.River;
            if (color.b > 0.8f && color.r < 0.2f) return BiomeType.ColdSea;
            if (color.g > 0.8f && color.b < 0.2f) return BiomeType.Mudflat;
            return BiomeType.None;
        }

        public bool IsWalkable(Vector2 worldPosition)
        {
            Vector2Int gridPos = WorldToGridPosition(worldPosition);
            if (!IsValidGrid(gridPos)) return false;
            return m_walkableGrid[gridPos.x, gridPos.y];
        }

        public bool IsWalkable(Vector2Int gridPos)
        {
            if (!IsValidGrid(gridPos)) return false;
            return m_walkableGrid[gridPos.x, gridPos.y];
        }

        public BiomeType GetBiomeAt(Vector2 worldPosition)
        {
            Vector2Int gridPos = WorldToGridPosition(worldPosition);
            if (!IsValidGrid(gridPos)) return BiomeType.None;
            return m_biomeGrid[gridPos.x, gridPos.y];
        }

        public Vector2Int WorldToGridPosition(Vector2 gameDataPos)
        {
            // 들어오는 값은 무조건 GameData의 로컬 좌표 (-250, -50) 입니다.
            float normalizedX = (gameDataPos.x / MAP_WIDTH) + 0.5f;
            float normalizedY = (gameDataPos.y / MAP_HEIGHT) + 0.5f;

            int pixelX = Mathf.FloorToInt(normalizedX * m_gridWidth);
            int pixelY = Mathf.FloorToInt(normalizedY * m_gridHeight);

            pixelX = Mathf.Clamp(pixelX, 0, m_gridWidth - 1);
            pixelY = Mathf.Clamp(pixelY, 0, m_gridHeight - 1);

            return new Vector2Int(pixelX, pixelY);
        }

        public Vector2 GridToWorldPosition(Vector2Int gridPos)
        {
            float normalizedX = (float)gridPos.x / m_gridWidth;
            float normalizedY = (float)gridPos.y / m_gridHeight;

            // 길을 찾은 다음 배에게 돌려주는 좌표도 무조건 GameData 기준 로컬 좌표입니다!
            float localX = (normalizedX - 0.5f) * MAP_WIDTH;
            float localY = (normalizedY - 0.5f) * MAP_HEIGHT;

            return new Vector2(localX, localY);
        }

        public bool IsValidGrid(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < m_gridWidth && gridPos.y >= 0 && gridPos.y < m_gridHeight;
        }
    }
}