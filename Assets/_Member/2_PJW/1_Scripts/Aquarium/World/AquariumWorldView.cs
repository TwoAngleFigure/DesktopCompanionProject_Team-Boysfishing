using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 배치 물고기를 3D 모델로 스폰해 탱크에서 헤엄치게 하는 월드 뷰.
    /// 오션/배 월드와 다른 'Aquarium' 레이어에 스폰 → 전용 카메라만 렌더(RT) → UI 레벨 합성.
    /// OnAquariumChanged마다 reconcile(부족분 스폰/초과분 디스폰)로 전체 재생성 없이 반영한다.
    /// </summary>
    public class AquariumWorldView : WorldViewBase
    {
        [SerializeField] private AquariumTankRenderer m_tank;
        [Tooltip("종별 Model 어드레서블이 없을 때 스폰할 공용 프리팹")]
        [SerializeField] private GameObject m_placeholderPrefab;
        [Tooltip("스폰 물고기의 부모(비우면 이 오브젝트)")]
        [SerializeField] private Transform m_fishRoot;
        [SerializeField] private string m_layerName = "Aquarium";
        [SerializeField] private float m_swimSpeed = 0.6f;

        private AquariumSystem m_aquarium;
        private int m_layer = -1;
        private readonly Dictionary<int, List<GameObject>> m_spawned = new();   // dataId → 인스턴스들

        public override void Bind()
        {
            m_layer = LayerMask.NameToLayer(m_layerName);
            if (m_layer < 0)
            {
                Debug.LogWarning($"[AquariumWorldView] '{m_layerName}' 레이어가 없습니다 — 아쿠아리움 물고기가 월드 카메라에 보일 수 있습니다. 레이어를 추가하세요.");
            }

            m_aquarium = SystemManager.GetSystem<AquariumSystem>();
            if (m_aquarium == null)
            {
                Debug.LogWarning("[AquariumWorldView] AquariumSystem 없음");
                return;
            }
            m_aquarium.OnAquariumChanged += Reconcile;
            Reconcile();
        }

        public override void Unbind()
        {
            if (m_aquarium != null) m_aquarium.OnAquariumChanged -= Reconcile;
            foreach (var list in m_spawned.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null) Destroy(list[i]);
                }
            }
            m_spawned.Clear();
            m_aquarium = null;
        }

        private void Reconcile()
        {
            if (m_aquarium == null) return;

            // 원하는 다중집합(dataId → 개수)
            var want = new Dictionary<int, int>();
            var ids = m_aquarium.FishDataIds;
            for (int i = 0; i < ids.Count; i++)
            {
                want[ids[i]] = (want.TryGetValue(ids[i], out int c) ? c : 0) + 1;
            }

            // 초과분 디스폰
            foreach (var kv in m_spawned)
            {
                int keep = want.TryGetValue(kv.Key, out int w) ? w : 0;
                var list = kv.Value;
                for (int i = list.Count - 1; i >= keep; i--)
                {
                    if (list[i] != null) Destroy(list[i]);
                    list.RemoveAt(i);
                }
            }

            // 부족분 스폰
            foreach (var kv in want)
            {
                if (!m_spawned.TryGetValue(kv.Key, out var list))
                {
                    m_spawned[kv.Key] = list = new List<GameObject>();
                }
                while (list.Count < kv.Value)
                {
                    GameObject go = Spawn(kv.Key);
                    if (go == null) break;   // 스폰 실패 시 무한 루프 방지
                    list.Add(go);
                }
            }
        }

        private GameObject Spawn(int dataId)
        {
            ItemData_Fish data = m_aquarium.GetFishData(dataId);
            GameObject prefab = null;
            if (data != null)
            {
                AssetProvider.TryGet<GameObject>(AssetKeys.Of(data, AssetUsage.Model), out prefab);
            }
            if (prefab == null) prefab = m_placeholderPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[AquariumWorldView] 스폰할 프리팹 없음 dataId={dataId} (Model 어드레서블·placeholder 모두 없음)");
                return null;
            }

            Bounds b = m_tank != null ? m_tank.TankBounds : new Bounds(transform.position, Vector3.one * 3f);
            Transform parent = m_fishRoot != null ? m_fishRoot : transform;

            GameObject go = Instantiate(prefab, RandomInBounds(b), Quaternion.identity, parent);
            if (m_layer >= 0) SetLayerRecursive(go, m_layer);

            var agent = go.AddComponent<AquariumFishAgent>();
            agent.Init(b, m_swimSpeed);
            return go;
        }

        private static Vector3 RandomInBounds(Bounds b)
            => new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                Random.Range(b.min.z, b.max.z));

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}
