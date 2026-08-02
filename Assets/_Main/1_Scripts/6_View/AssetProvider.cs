using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// View 계층 에셋 서비스(D18·§16.3): Addressables 래핑 — 부팅 프리로드 → 동기 조회.
    /// GameManager(조립 루트)가 생성해 UIManager/WorldManager에만 주입한다(System에는 주지 않음 — 계층 유지).
    ///
    /// 현재 전략: "preload" 라벨 전량 프리로드 후 상주(§16.5 현재 단계).
    /// 후속 최적화(라벨 스코프 해제·참조 카운팅)를 위해 핸들을 보관하며 ReleaseAll 훅을 둔다.
    ///
    /// 키가 아예 없는 경우 기본(대체) 에셋으로 대신 응답한다(UseFallback). 대체 우선순위는
    /// "Default_{용도}" → "Default" → 절차적 플레이스홀더(Sprite/Texture2D 한정) 순이다.
    /// </summary>
    public class AssetProvider
    {
        public const string PreloadLabel = "preload";

        // 주소 키 → 로드된 에셋들(같은 주소에 Texture2D/Sprite 등 복수 타입 위치가 있을 수 있어 리스트)
        private readonly Dictionary<string, List<Object>> m_cache = new();
        private readonly List<AsyncOperationHandle> m_handles = new();
        // 누락 키 로그 1회 제한 — 프레임마다 갱신되는 슬롯 UI에서 로그가 폭주하는 것을 막는다.
        private readonly HashSet<string> m_reportedKeys = new();
        // 타입별 절차적 플레이스홀더(런타임 생성, ReleaseAll에서 파기)
        private readonly Dictionary<System.Type, Object> m_placeholders = new();

        public bool IsPreloaded { get; private set; }

        /// <summary>키 누락 시 기본 에셋으로 대체할지 여부. 끄면 종전대로 null/false를 돌려준다.</summary>
        public bool UseFallback { get; set; } = true;

        /// <summary>"preload" 라벨의 모든 에셋을 주소 키로 캐시한다. 부팅 시 1회(GameManager).</summary>
        public async Task PreloadAsync()
        {
            AsyncOperationHandle<IList<IResourceLocation>> locationsHandle =
                Addressables.LoadResourceLocationsAsync(PreloadLabel);
            IList<IResourceLocation> locations = await locationsHandle.Task;

            if (locationsHandle.Status != AsyncOperationStatus.Succeeded ||
                locations == null || locations.Count == 0)
            {
                Debug.Log("[AssetProvider] preload 라벨 에셋 없음 — 프리로드 생략");
                Addressables.Release(locationsHandle);
                IsPreloaded = true;
                return;
            }

            // 병렬 로드: 모든 핸들을 '먼저' 시작한 뒤 한꺼번에 대기한다.
            // (직렬 await는 다음 로드를 이전 완료까지 미뤄 로딩 시간이 합산됨 → 벽시계 시간 급증)
            var handles = new List<AsyncOperationHandle<Object>>(locations.Count);
            var tasks = new List<Task>(locations.Count);
            foreach (IResourceLocation location in locations)
            {
                AsyncOperationHandle<Object> handle = Addressables.LoadAssetAsync<Object>(location);
                handles.Add(handle);
                tasks.Add(handle.Task);
            }
            await Task.WhenAll(tasks);

            for (int i = 0; i < handles.Count; i++)
            {
                AsyncOperationHandle<Object> handle = handles[i];
                IResourceLocation location = locations[i];

                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                {
                    if (!m_cache.TryGetValue(location.PrimaryKey, out List<Object> list))
                    {
                        m_cache[location.PrimaryKey] = list = new List<Object>();
                    }
                    list.Add(handle.Result);
                    m_handles.Add(handle);
                }
                else
                {
                    Debug.LogError($"[AssetProvider] 프리로드 실패: {location.PrimaryKey}");
                    Addressables.Release(handle);
                }
            }

            Addressables.Release(locationsHandle);
            IsPreloaded = true;
            Debug.Log($"[AssetProvider] 프리로드 완료: {m_cache.Count}개 키");
        }

        /// <summary>프리로드된 에셋 조회(동기). 키가 없으면 기본 에셋, 그마저 없으면 에러 로그 + null.</summary>
        public T Get<T>(string key) where T : Object
        {
            if (TryGet(key, out T asset))
            {
                return asset;
            }
            ReportOnce(key, $"[AssetProvider] 미탑재 에셋 키: '{key}' ({typeof(T).Name}) — 주소/preload 라벨 확인 (검증 툴: Tools/DesktopCompanion/에셋 키 검증)", true);
            return null;
        }

        /// <summary>
        /// 에셋 조회. 키가 카탈로그에 아예 없으면 기본 에셋으로 대체한다(UseFallback).
        /// 키는 있으나 요청 타입이 다른 경우(예: Sprite 요청 · Texture2D 캐시)에는 대체하지 않는다 —
        /// 호출부의 자체 변환 경로를 가로채지 않기 위함이다.
        /// </summary>
        public bool TryGet<T>(string key, out T asset) where T : Object
        {
            if (TryGetExact(key, out asset))
            {
                return true;
            }

            if (UseFallback == false || string.IsNullOrEmpty(key) || m_cache.ContainsKey(key))
            {
                return false;
            }

            if (TryGetFallback(key, out asset))
            {
                ReportOnce(key, $"[AssetProvider] 에셋 키 누락: '{key}' ({typeof(T).Name}) — 기본 에셋으로 대체", false);
                return true;
            }
            return false;
        }

        /// <summary>대체 없이 정확히 그 키·타입만 조회한다(대체본을 받으면 곤란한 호출부용).</summary>
        public bool TryGetExact<T>(string key, out T asset) where T : Object
        {
            if (string.IsNullOrEmpty(key) == false && m_cache.TryGetValue(key, out List<Object> list))
            {
                foreach (Object cached in list)
                {
                    if (cached is T typed)
                    {
                        asset = typed;
                        return true;
                    }
                }
            }
            asset = null;
            return false;
        }

        /// <summary>전체 해제 훅(§16.5 후속 최적화용 — 현 단계에서는 종료 외 호출 없음).</summary>
        public void ReleaseAll()
        {
            foreach (AsyncOperationHandle handle in m_handles)
            {
                Addressables.Release(handle);
            }
            m_handles.Clear();
            m_cache.Clear();
            m_reportedKeys.Clear();

            foreach (Object placeholder in m_placeholders.Values)
            {
                if (placeholder != null)
                {
                    Object.Destroy(placeholder);
                }
            }
            m_placeholders.Clear();
            IsPreloaded = false;
        }

        // ─────────────────────────────── 기본(대체) 에셋 ───────────────────────────────

        /// <summary>"Default_{용도}" → "Default" → 절차적 플레이스홀더 순으로 대체본을 찾는다.</summary>
        private bool TryGetFallback<T>(string key, out T asset) where T : Object
        {
            // 1) 용도별 기본 에셋 — 아티스트가 "Default_Icon" 주소만 등록하면 그대로 교체된다.
            string usage = AssetKeys.UsageOf(key);
            if (string.IsNullOrEmpty(usage) == false &&
                TryGetExact(AssetKeys.DefaultOf(usage), out asset))
            {
                return true;
            }

            // 2) 용도 무관 기본 에셋
            if (TryGetExact(AssetKeys.DefaultPrefix, out asset))
            {
                return true;
            }

            // 3) 등록된 기본 에셋조차 없을 때의 최후 수단(에셋 준비 전 단계 대비)
            return TryGetPlaceholder(out asset);
        }

        /// <summary>절차적 플레이스홀더. 프리팹 등은 안전한 생성본이 없으므로 Sprite/Texture2D만 만든다.</summary>
        private bool TryGetPlaceholder<T>(out T asset) where T : Object
        {
            System.Type type = typeof(T);
            if (m_placeholders.TryGetValue(type, out Object cached) && cached != null)
            {
                asset = (T)cached;
                return true;
            }

            Object created = null;
            if (type == typeof(Sprite))
            {
                created = CreatePlaceholderSprite();
            }
            else if (type == typeof(Texture2D))
            {
                created = CreatePlaceholderTexture();
            }

            if (created == null)
            {
                asset = null;
                return false;
            }

            m_placeholders[type] = created;
            asset = (T)created;
            return true;
        }

        private Sprite CreatePlaceholderSprite()
        {
            // 텍스처도 캐시에 올려 재사용하고, 파기 대상을 m_placeholders 한 곳으로 모은다.
            TryGetPlaceholder(out Texture2D texture);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "AssetProvider_Placeholder_Sprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>누락을 한눈에 알아보도록 체크무늬(마젠타)로 그린다.</summary>
        private static Texture2D CreatePlaceholderTexture()
        {
            const int Size = 32;
            const int CellSize = 8;

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "AssetProvider_Placeholder_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color magenta = new(0.85f, 0.15f, 0.85f, 1f);
            Color dark = new(0.16f, 0.16f, 0.20f, 1f);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool even = (x / CellSize + y / CellSize) % 2 == 0;
                    pixels[y * Size + x] = even ? magenta : dark;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void ReportOnce(string key, string message, bool isError)
        {
            if (m_reportedKeys.Add(key ?? string.Empty) == false)
            {
                return;
            }
            if (isError)
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }
    }
}
