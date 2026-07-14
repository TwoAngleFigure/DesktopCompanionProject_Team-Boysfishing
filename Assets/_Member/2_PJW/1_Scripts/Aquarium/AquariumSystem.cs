using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 아쿠아리움 시스템. 배치된 물고기가 재료를 자동 생산한다.
    /// - Plan A: 물고기를 '개체(EntityHandle)'로 보유한다. 배치=인벤토리에서 개체를 빼 옴, 회수=인벤토리로 되돌림.
    ///   → 보유 수만큼만 배치 가능(수용량과 별개로 소유 제한), 개체의 Size/Quality 보존.
    /// - 물고기별 주기(성급으로 단축)마다 재료 포인트 적립 → 재료 요구 포인트 도달 시 재료 1개 생산.
    ///   서로 다른 물고기가 같은 재료를 생산하면 포인트 풀을 공유한다.
    /// - 타임스탬프 정산으로 오프라인(앱 종료) 시간도 반영한다.
    /// - 생산물은 InventorySystem으로 즉시 지급, 가득 차면 보류 후 재지급.
    /// - 수용량 상한은 AquariumUpgradeData(레벨=id), 업그레이드 재료 비용은 InventorySystem으로 소비.
    /// ※ 생산 주기 감소는 '종 정의(ItemData_Fish.Star)'를 사용한다(개체 Quality가 아님 — 08 결정 유지).
    /// R1: 자기 원시 상태는 Initialize, 타 System(InventorySystem) 참조·개체 복원·오프라인 정산은 PostInitialize.
    /// </summary>
    public class AquariumSystem : SystemBase, ISaveable, ITickable
    {
        private const float MinCycleSeconds = 1f;         // 유효 주기 하한(0/음수 방지)
        private const float SettleIntervalSeconds = 1f;   // 온라인 Tick 정산 간격

        private readonly List<EntityHandle> m_fishHandles = new();   // 배치된 물고기 개체
        private readonly List<float> m_fishProgress = new();         // 개체별 주기 진행 잔여(초) — m_fishHandles와 병렬
        private readonly Dictionary<int, int> m_materialPoints = new();
        private readonly Dictionary<int, int> m_pendingDeliver = new();

        private int m_upgradeLevel = 1;
        private DateTime m_lastSettleUtc = DateTime.UtcNow;
        private float m_tickAccum;

        private InventorySystem m_inventory;
        private AquariumSave m_loadedSave;

        /// <summary>배치/제거/정산/업그레이드 등 상태 변화 시 발행(UI 갱신용).</summary>
        public event Action OnAquariumChanged;

        // ── 읽기 API(View용) ──

        /// <summary>배치된 물고기의 종 dataId 목록(개체 순서와 병렬). 월드 스폰·미러 회수 인덱스에 사용.</summary>
        public IReadOnlyList<int> FishDataIds
        {
            get
            {
                var list = new List<int>(m_fishHandles.Count);
                for (int i = 0; i < m_fishHandles.Count; i++)
                {
                    var e = FishEntity(m_fishHandles[i]);
                    list.Add(e != null ? e.DataId : 0);
                }
                return list;
            }
        }

        public int UpgradeLevel => m_upgradeLevel;

        public int UsedCapacity
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < m_fishHandles.Count; i++)
                {
                    var e = FishEntity(m_fishHandles[i]);
                    if (e != null) sum += e.ItemData.AquariumCapacity;
                }
                return sum;
            }
        }

        public int MaxCapacity
        {
            get
            {
                var d = DataManager.GetData<AquariumUpgradeData>(m_upgradeLevel);
                return d != null ? d.MaxCapacity : 0;
            }
        }

        /// <summary>다음 업그레이드 레벨 정의(비용 조회용). 최대 레벨이면 null.</summary>
        public AquariumUpgradeData NextUpgrade => DataManager.GetData<AquariumUpgradeData>(m_upgradeLevel + 1);

        // ── 생명주기 ──
        public override void Initialize()
        {
            m_upgradeLevel = 1;                  // 원시 기본값. 저장이 있으면 복원이 덮어씀.
            m_lastSettleUtc = DateTime.UtcNow;
        }

        public override void PostInitialize()
        {
            m_inventory = SystemManager.GetSystem<InventorySystem>();
            if (m_inventory == null)
            {
                Debug.LogWarning("[AquariumSystem] InventorySystem 미발견 — 배치/회수·생산물 지급이 제한됩니다.");
            }

            if (m_loadedSave != null)
            {
                ApplyLoadedSave(m_loadedSave);
                m_loadedSave = null;
            }

            // 오프라인 방치분 일괄 정산. 경과 초를 로그로 남겨 오프라인 누적이 실제로 반영되는지 확인 가능.
            double offlineSeconds = (DateTime.UtcNow - m_lastSettleUtc).TotalSeconds;
            Debug.Log($"[AquariumSystem] 오프라인 경과 {offlineSeconds:0.0}s 정산 (배치 {m_fishHandles.Count}마리)");
            Settle(DateTime.UtcNow);
        }

        // ── 배치/회수 (인벤토리 소비/반환) ──

        /// <summary>
        /// 인벤토리에서 해당 종(dataId)의 물고기 1개체를 빼 아쿠아리움에 배치한다.
        /// 수용량 초과 또는 보유 개체 없음이면 실패.
        /// </summary>
        public bool AddFish(int fishDataId)
        {
            var data = FishData(fishDataId);
            if (data == null)
            {
                Debug.LogWarning($"[AquariumSystem] AddFish 실패 — ItemData_Fish 없음 id={fishDataId}");
                return false;
            }
            if (m_inventory == null)
            {
                Debug.LogWarning("[AquariumSystem] AddFish 실패 — InventorySystem 없음");
                return false;
            }
            if (UsedCapacity + data.AquariumCapacity > MaxCapacity)
            {
                return false;   // 수용량 초과
            }
            if (!TryTakeFishFromInventory(fishDataId, out EntityHandle handle))
            {
                return false;   // 보유 개체 없음
            }

            m_fishHandles.Add(handle);
            m_fishProgress.Add(0f);
            OnAquariumChanged?.Invoke();
            return true;
        }

        /// <summary>배치 물고기를 회수해 인벤토리로 되돌린다. 인벤토리가 가득 차면 회수 취소(개체 유지).</summary>
        public bool RemoveFishAt(int index)
        {
            if (index < 0 || index >= m_fishHandles.Count)
            {
                return false;
            }
            EntityHandle handle = m_fishHandles[index];

            if (m_inventory != null)
            {
                if (!m_inventory.AddItem(handle))
                {
                    Debug.LogWarning("[AquariumSystem] 회수 실패 — 인벤토리 가득 참(공간 확보 후 재시도).");
                    return false;   // 회수 취소: 개체 손실 없이 아쿠아리움에 유지
                }
            }
            else
            {
                EntityManager.Destroy(handle);   // 인벤토리 없음(예외) → 개체 누수 방지
            }

            m_fishHandles.RemoveAt(index);
            m_fishProgress.RemoveAt(index);
            OnAquariumChanged?.Invoke();
            return true;
        }

        // 인벤토리 Fish 슬롯에서 dataId 일치 개체 1개를 찾아 슬롯에서 뺀다(개체는 파괴하지 않음).
        private bool TryTakeFishFromInventory(int dataId, out EntityHandle handle)
        {
            handle = default;
            EntityHandle[] slots = m_inventory.GetSlots(ItemType.Fish);
            for (int i = 0; i < slots.Length; i++)
            {
                var e = EntityManager.Get<Entity_Fish>(slots[i]);
                if (e != null && e.DataId == dataId)
                {
                    handle = slots[i];
                    m_inventory.RemoveAt(ItemType.Fish, i, destroyEntity: false);
                    return true;
                }
            }
            return false;
        }

        // ── 업그레이드 ──
        public bool TryUpgrade()
        {
            var next = NextUpgrade;
            if (next == null || m_inventory == null)
            {
                return false;
            }

            MaterialCost[] costs = next.MaterialCosts;
            if (costs != null)
            {
                foreach (var c in costs)   // 1) 검증
                {
                    if (c?.Material == null ||
                        m_inventory.GetTotalQuantityByDataId(ItemType.Materials, c.Material.ID) < c.Count)
                    {
                        return false;
                    }
                }
                foreach (var c in costs)   // 2) 소비
                {
                    m_inventory.ConsumeItemByDataId(ItemType.Materials, c.Material.ID, c.Count);
                }
            }

            m_upgradeLevel++;
            OnAquariumChanged?.Invoke();
            return true;
        }

        // ── Tick(온라인) ──
        public void Tick(float deltaTime)
        {
            m_tickAccum += deltaTime;
            if (m_tickAccum < SettleIntervalSeconds)
            {
                return;
            }
            m_tickAccum = 0f;
            Settle(DateTime.UtcNow);
        }

        // ── 정산(온라인/오프라인 공통) ──
        private void Settle(DateTime nowUtc)
        {
            double elapsed = (nowUtc - m_lastSettleUtc).TotalSeconds;
            if (elapsed <= 0)
            {
                return;   // 시계 역행/동일 시각 보호
            }
            m_lastSettleUtc = nowUtc;

            // 1) 물고기별 주기 경과 → 재료 포인트 적립
            for (int i = 0; i < m_fishHandles.Count; i++)
            {
                var e = FishEntity(m_fishHandles[i]);
                if (e == null) continue;
                var f = e.ItemData;

                float cycle = EffectiveCycle(f);
                double total = m_fishProgress[i] + elapsed;
                int batches = (int)(total / cycle);
                m_fishProgress[i] = (float)(total - batches * cycle);

                if (batches > 0 && f.AquariumMaterial != null)
                {
                    AddPoints(f.AquariumMaterial.ID, batches * f.AquariumProduceAmount);
                }
            }

            // 2) 재료 포인트 → 재료 생산(보류 큐에 적립)
            var matIds = new List<int>(m_materialPoints.Keys);
            foreach (int matId in matIds)
            {
                var mat = DataManager.GetData<ItemData_Materials>(matId);
                if (mat == null) continue;

                int produced = m_materialPoints[matId] / mat.AquariumRequiredPoints;
                if (produced <= 0) continue;

                m_materialPoints[matId] -= produced * mat.AquariumRequiredPoints;
                m_pendingDeliver[matId] = (m_pendingDeliver.TryGetValue(matId, out int p) ? p : 0) + produced;
            }

            DeliverPending();
            OnAquariumChanged?.Invoke();
        }

        // ── 인벤토리 지급(실패 시 보류 유지) ──
        private void DeliverPending()
        {
            if (m_inventory == null || m_pendingDeliver.Count == 0)
            {
                return;
            }

            var ids = new List<int>(m_pendingDeliver.Keys);
            foreach (int matId in ids)
            {
                int count = m_pendingDeliver[matId];
                if (count <= 0)
                {
                    m_pendingDeliver.Remove(matId);
                    continue;
                }

                var handle = EntityManager.Create<ItemData_Materials>(matId);
                var e = EntityManager.Get<Entity_Materials>(handle);
                if (e == null)
                {
                    EntityManager.Destroy(handle);
                    continue;
                }
                e.SetQuantity(count);

                if (m_inventory.AddItem(handle))
                {
                    m_pendingDeliver.Remove(matId);   // 지급 성공 → 보류 해제
                }
                else
                {
                    EntityManager.Destroy(handle);    // 만석 → 보류 유지, 다음 정산에 재시도
                }
            }
        }

        private void AddPoints(int matId, int amount)
            => m_materialPoints[matId] = (m_materialPoints.TryGetValue(matId, out int v) ? v : 0) + amount;

        private ItemData_Fish FishData(int id) => DataManager.GetData<ItemData_Fish>(id);
        private Entity_Fish FishEntity(EntityHandle h) => EntityManager.Get<Entity_Fish>(h);

        /// <summary>성급으로 단축된 유효 생산 주기(초). 하한 <see cref="MinCycleSeconds"/>.</summary>
        private float EffectiveCycle(ItemData_Fish fish)
            => Mathf.Max(MinCycleSeconds, fish.AquariumProduceTime - ((int)fish.Star - 1) * fish.AquariumDecreaseCount);

        private static ItemQuality NormalizeQuality(int q) => (ItemQuality)Mathf.Clamp(q, 1, 5);

        // ── View용 Data/지표 접근자(additive) ──

        /// <summary>종 정의(ItemData_Fish) 조회(뷰가 dataId로 종 정보를 얻을 때).</summary>
        public ItemData_Fish GetFishData(int id) => FishData(id);

        /// <summary>재료 정의(ItemData_Materials) 조회(아이콘 키 등).</summary>
        public ItemData_Materials GetMaterialData(int id) => DataManager.GetData<ItemData_Materials>(id);

        /// <summary>시간당 생산 포인트(표시용). 성급 단축 반영: produceAmount × 3600 / 유효주기.</summary>
        public float PointsPerHour(ItemData_Fish fish)
            => fish == null ? 0f : fish.AquariumProduceAmount * 3600f / EffectiveCycle(fish);

        // ── 표시용 상태 구조 ──

        public readonly struct FishSlotStatus
        {
            public readonly int Index;
            public readonly int FishDataId;
            public readonly string FishName;
            public readonly int MaterialId;
            public readonly string MaterialName;
            public readonly int ProduceAmount;
            public readonly float CycleSeconds;
            public readonly float RemainingSeconds;

            public FishSlotStatus(int index, int fishDataId, string fishName, int materialId,
                string materialName, int produceAmount, float cycleSeconds, float remainingSeconds)
            {
                Index = index; FishDataId = fishDataId; FishName = fishName;
                MaterialId = materialId; MaterialName = materialName; ProduceAmount = produceAmount;
                CycleSeconds = cycleSeconds; RemainingSeconds = remainingSeconds;
            }
        }

        public readonly struct MaterialStatus
        {
            public readonly int MaterialId;
            public readonly string MaterialName;
            public readonly int Points;
            public readonly int Required;
            public readonly int Pending;

            public MaterialStatus(int materialId, string materialName, int points, int required, int pending)
            {
                MaterialId = materialId; MaterialName = materialName;
                Points = points; Required = required; Pending = pending;
            }
        }

        /// <summary>배치된 물고기별 상태(이름·생산 재료·배치당 생산량·다음 생산까지 남은 시간).</summary>
        public List<FishSlotStatus> GetFishStatuses()
        {
            var list = new List<FishSlotStatus>(m_fishHandles.Count);
            double sinceSettle = Math.Max(0.0, (DateTime.UtcNow - m_lastSettleUtc).TotalSeconds);
            for (int i = 0; i < m_fishHandles.Count; i++)
            {
                var e = FishEntity(m_fishHandles[i]);
                if (e == null) continue;
                var f = e.ItemData;

                float cycle = EffectiveCycle(f);
                float remaining = (float)Math.Max(0.0, cycle - (m_fishProgress[i] + sinceSettle));
                var mat = f.AquariumMaterial;
                list.Add(new FishSlotStatus(i, f.ID, f.Name,
                    mat != null ? mat.ID : 0, mat != null ? mat.Name : "-",
                    f.AquariumProduceAmount, cycle, remaining));
            }
            return list;
        }

        /// <summary>아쿠아리움이 생산 중/보류 중인 모든 재료의 상태(누적·요구 포인트·보류 개수).</summary>
        public List<MaterialStatus> GetMaterialStatuses()
        {
            var ids = new HashSet<int>();
            for (int i = 0; i < m_fishHandles.Count; i++)
            {
                var e = FishEntity(m_fishHandles[i]);
                if (e?.ItemData.AquariumMaterial != null) ids.Add(e.ItemData.AquariumMaterial.ID);
            }
            foreach (int k in m_materialPoints.Keys) ids.Add(k);
            foreach (int k in m_pendingDeliver.Keys) ids.Add(k);

            var list = new List<MaterialStatus>(ids.Count);
            foreach (int id in ids)
            {
                var mat = DataManager.GetData<ItemData_Materials>(id);
                if (mat == null) continue;
                int points = m_materialPoints.TryGetValue(id, out int p) ? p : 0;
                int pending = m_pendingDeliver.TryGetValue(id, out int pd) ? pd : 0;
                list.Add(new MaterialStatus(id, mat.Name, points, mat.AquariumRequiredPoints, pending));
            }
            return list;
        }

        // ── Save ──
        public string SaveId => "aquarium";
        public Type StateType => typeof(AquariumSave);

        public object CaptureState()
        {
            var save = new AquariumSave
            {
                lastSettleUtc = m_lastSettleUtc.ToString("o", CultureInfo.InvariantCulture),
                upgradeLevel = m_upgradeLevel,
            };

            for (int i = 0; i < m_fishHandles.Count; i++)
            {
                var e = FishEntity(m_fishHandles[i]);
                if (e == null) continue;   // 유실 개체는 저장에서 제외
                save.fish.Add(new AquariumSave.FishEntry
                {
                    handle = m_fishHandles[i].ToString(),
                    dataId = e.DataId,
                    size = e.Size,
                    quality = (int)e.Quality,
                    progress = i < m_fishProgress.Count ? m_fishProgress[i] : 0f,
                });
            }

            foreach (var kv in m_materialPoints)
            {
                save.pointMatIds.Add(kv.Key);
                save.pointValues.Add(kv.Value);
            }
            foreach (var kv in m_pendingDeliver)
            {
                save.pendingMatIds.Add(kv.Key);
                save.pendingCounts.Add(kv.Value);
            }
            return save;
        }

        public void RestoreState(object state)
        {
            if (state is AquariumSave save)
            {
                m_loadedSave = save;   // 적용은 PostInitialize(개체 복원 + 오프라인 정산)
            }
        }

        private void ApplyLoadedSave(AquariumSave s)
        {
            m_upgradeLevel = Mathf.Max(1, s.upgradeLevel);

            if (DateTime.TryParse(s.lastSettleUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                m_lastSettleUtc = parsed;
            }
            else
            {
                m_lastSettleUtc = DateTime.UtcNow;   // 파싱 실패 시 방치분 없음 처리
            }

            // 배치 물고기 개체 복원(인벤토리 슬롯 복원과 동형: 저장 handle로 Entity 재등록 + 롤값 주입)
            m_fishHandles.Clear();
            m_fishProgress.Clear();
            if (s.fish != null)
            {
                foreach (var fe in s.fish)
                {
                    if (!EntityHandle.TryParse(fe.handle, out EntityHandle h)) continue;
                    if (DataManager.GetData<ItemData_Fish>(fe.dataId) == null) continue;

                    EntityHandle restored = EntityManager.Restore<ItemData_Fish>(h, fe.dataId);
                    var e = EntityManager.Get<Entity_Fish>(restored);
                    if (e == null) continue;

                    e.SetRollResult(Mathf.Max(0f, fe.size), NormalizeQuality(fe.quality));
                    m_fishHandles.Add(restored);
                    m_fishProgress.Add(fe.progress);
                }
            }

            m_materialPoints.Clear();
            if (s.pointMatIds != null && s.pointValues != null)
            {
                int n = Mathf.Min(s.pointMatIds.Count, s.pointValues.Count);
                for (int i = 0; i < n; i++) m_materialPoints[s.pointMatIds[i]] = s.pointValues[i];
            }

            m_pendingDeliver.Clear();
            if (s.pendingMatIds != null && s.pendingCounts != null)
            {
                int n = Mathf.Min(s.pendingMatIds.Count, s.pendingCounts.Count);
                for (int i = 0; i < n; i++) m_pendingDeliver[s.pendingMatIds[i]] = s.pendingCounts[i];
            }
        }
    }
}
