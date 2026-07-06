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
    /// </summary>
    public class AssetProvider
    {
        public const string PreloadLabel = "preload";

        // 주소 키 → 로드된 에셋들(같은 주소에 Texture2D/Sprite 등 복수 타입 위치가 있을 수 있어 리스트)
        private readonly Dictionary<string, List<Object>> m_cache = new();
        private readonly List<AsyncOperationHandle> m_handles = new();

        public bool IsPreloaded { get; private set; }

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

            foreach (IResourceLocation location in locations)
            {
                AsyncOperationHandle<Object> handle = Addressables.LoadAssetAsync<Object>(location);
                Object asset = await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded && asset != null)
                {
                    if (!m_cache.TryGetValue(location.PrimaryKey, out List<Object> list))
                    {
                        m_cache[location.PrimaryKey] = list = new List<Object>();
                    }
                    list.Add(asset);
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

        /// <summary>프리로드된 에셋 조회(동기). 없으면 에러 로그 + null.</summary>
        public T Get<T>(string key) where T : Object
        {
            if (TryGet(key, out T asset))
            {
                return asset;
            }
            Debug.LogError($"[AssetProvider] 미탑재 에셋 키: '{key}' ({typeof(T).Name}) — 주소/preload 라벨 확인 (검증 툴: Tools/DesktopCompanion/에셋 키 검증)");
            return null;
        }

        public bool TryGet<T>(string key, out T asset) where T : Object
        {
            if (m_cache.TryGetValue(key, out List<Object> list))
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
            IsPreloaded = false;
        }
    }
}
