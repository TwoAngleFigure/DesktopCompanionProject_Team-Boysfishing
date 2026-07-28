using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
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

        [Header("Fish Size")]
        [Tooltip("개체 Size에 곱하는 전역 배율. 최종 스케일 = 프리팹 기준 스케일 × clamp(Size) × 이 값.")]
        [SerializeField] private float m_sizeScale = 1f;
        [Tooltip("개체 Size 클램프 최소(너무 작아지지 않게).")]
        [SerializeField] private float m_sizeMin = 0.5f;
        [Tooltip("개체 Size 클램프 최대(너무 커지지 않게).")]
        [SerializeField] private float m_sizeMax = 2f;

        private AquariumSystem m_aquarium;
        private int m_layer = -1;
        private readonly Dictionary<EntityHandle, GameObject> m_spawned = new();   // 개체 핸들 → 인스턴스(1:1)

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
            foreach (var go in m_spawned.Values)
            {
                if (go != null) Destroy(go);
            }
            m_spawned.Clear();
            m_aquarium = null;
        }

        private void Reconcile()
        {
            if (m_aquarium == null) return;

            // 슬롯별 (핸들·dataId·개체 Size). 핸들 기준 1:1 매핑이라 인덱스가 밀려도 인스턴스가 보존된다.
            var placed = m_aquarium.GetPlacedFish();

            var alive = new HashSet<EntityHandle>();
            foreach (var pf in placed)
            {
                alive.Add(pf.Handle);
                if (m_spawned.TryGetValue(pf.Handle, out var existing) && existing != null)
                {
                    existing.GetComponent<AquariumFishAgent>()?.SetSize(EffectiveSize(pf.Size));   // 크기만 갱신(종 불변)
                    continue;
                }
                GameObject go = Spawn(pf.DataId, pf.Size);
                if (go != null) m_spawned[pf.Handle] = go;
            }

            // 사라진(회수된) 핸들 디스폰
            var remove = new List<EntityHandle>();
            foreach (var kv in m_spawned)
            {
                if (!alive.Contains(kv.Key))
                {
                    if (kv.Value != null) Destroy(kv.Value);
                    remove.Add(kv.Key);
                }
            }
            for (int i = 0; i < remove.Count; i++) m_spawned.Remove(remove[i]);
        }

        private GameObject Spawn(int dataId, float size)
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

            Transform space = m_tank != null ? m_tank.TankSpace : transform;
            Bounds local = m_tank != null ? m_tank.LocalBounds : new Bounds(Vector3.zero, Vector3.one * 3f);
            Vector3 spawnLocal = new Vector3(
                Random.Range(local.min.x, local.max.x),
                Random.Range(local.min.y, local.max.y),
                Random.Range(local.min.z, local.max.z));

            Transform parent = m_fishRoot != null ? m_fishRoot : transform;   // ※ m_fishRoot는 회전 루트 하위여야 함
            GameObject go = Instantiate(prefab, space.TransformPoint(spawnLocal), Quaternion.identity, parent);
            if (m_layer >= 0) SetLayerRecursive(go, m_layer);

            var agent = go.AddComponent<AquariumFishAgent>();
            agent.Init(space, local, go.transform.localScale, EffectiveSize(size), m_swimSpeed);   // 프리팹 원본 스케일=baseScale
            return go;
        }

        /// <summary>개체 Size를 인스펙터 클램프(min/max)·전역 배율로 보정한 최종 스케일 계수.</summary>
        private float EffectiveSize(float rawSize)
        {
            float s = rawSize > 0f ? rawSize : 1f;                 // 0/음수 방어
            s = Mathf.Clamp(s, m_sizeMin, m_sizeMax);              // 인스펙터 클램프
            return s * Mathf.Max(0.0001f, m_sizeScale);            // 전역 배율
        }

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
