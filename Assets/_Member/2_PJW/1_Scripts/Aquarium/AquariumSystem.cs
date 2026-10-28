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
    /// 배치된 물고기가 재료를 자동 생산하는 아쿠아리움 시스템.
    /// - 물고기를 개체(EntityHandle)로 보유한다. 배치는 인벤토리에서 개체를 빼 오고, 회수는 되돌린다.
    /// - 물고기별 생산 주기(개체 성급만큼 단축)마다 재료 포인트를 적립하고,
    ///   요구 포인트에 도달하면 재료 1개를 생산한다. 같은 재료를 생산하는 물고기끼리 포인트 풀을 공유한다.
    /// - 정산은 타임스탬프 기준이라 앱 종료 중 경과 시간도 반영된다.
    /// - 생산물은 InventorySystem으로 지급하고, 지급에 실패하면 보류했다가 다음 정산에 재시도한다.
    /// - 수용량 상한은 AquariumUpgradeData(레벨=id)가 정하며, 강화 비용은 골드와 재료다.
    /// 초기화는 자기 원시 상태를 Initialize에서, 타 System 참조·개체 복원·오프라인 정산을 PostInitialize에서 한다.
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
        private PlayerSystem m_player;
        private AquariumSave m_loadedSave;

        /// <summary>
        /// 구조가 바뀔 때(배치·회수·강화) 발행한다. 목록 재구성이 필요한 쪽이 구독한다.
        /// </summary>
        public event Action OnAquariumChanged;

        /// <summary>1초 정산으로 수치(포인트·보류·남은 시간)만 바뀔 때 발행한다. 목록 구조는 유지된다.</summary>
        public event Action OnAquariumProgress;

        // ── 읽기 API(View·World용) ──

        /// <summary>배치 물고기 1마리의 월드 표현 정보(핸들·종·개체 크기).</summary>
        public readonly struct PlacedFishView
        {
            public readonly EntityHandle Handle;
            public readonly int DataId;
            public readonly float Size;   // 개체 롤값 Size(같은 종도 개체마다 다름)

            public PlacedFishView(EntityHandle handle, int dataId, float size)
            {
                Handle = handle; DataId = dataId; Size = size;
            }
        }

        /// <summary>배치된 모든 물고기의 (핸들·dataId·개체 Size)를 반환한다. 월드 뷰의 스폰·크기 적용에 쓴다.</summary>
        public List<PlacedFishView> GetPlacedFish()
        {
            var list = new List<PlacedFishView>(m_fishHandles.Count);
            for (int i = 0; i < m_fishHandles.Count; i++)
            {
                var e = FishEntity(m_fishHandles[i]);
                list.Add(new PlacedFishView(
                    m_fishHandles[i],
                    e != null ? e.DataId : 0,
                    e != null ? e.Size : 1f));
            }
            return list;
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

        /// <summary>현재 등급 정의. 등급 이름·수용 한도 조회에 쓴다.</summary>
        public AquariumUpgradeData CurrentUpgrade => DataManager.GetData<AquariumUpgradeData>(m_upgradeLevel);

        /// <summary>
        /// 다음 등급 정의. 최대 등급이면 null이다.
        /// 다음 id의 행이 없을 때뿐 아니라 행이 있어도 내용이 비어 있으면 null로 취급한다.
        /// </summary>
        public AquariumUpgradeData NextUpgrade
        {
            get
            {
                var next = DataManager.GetData<AquariumUpgradeData>(m_upgradeLevel + 1);
                return IsDefinedUpgrade(next) ? next : null;
            }
        }

        /// <summary>등급 정의가 유효한 내용을 가졌는지. 수용 한도가 없는 등급은 강화 대상이 아니다.</summary>
        private static bool IsDefinedUpgrade(AquariumUpgradeData data) => data != null && data.MaxCapacity > 0;

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

            m_player = SystemManager.GetSystem<PlayerSystem>();
            if (m_player == null)
            {
                Debug.LogWarning("[AquariumSystem] PlayerSystem 미발견 — 골드 비용 검증·차감이 불가합니다.");
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

        /// <summary>핸들로 지목한 개체를 인벤토리에서 빼 아쿠아리움에 배치한다.</summary>
        public bool AddFish(EntityHandle handle)
        {
            if (CanPlaceFish(handle, out string reason) == false)
            {
                Debug.Log($"[AquariumSystem] 배치 불가 — {reason}");
                return false;
            }
            if (TryTakeFromInventory(handle) == false)
            {
                Debug.LogWarning($"[AquariumSystem] 배치 실패 — 인벤토리에 없는 핸들 handle={handle}");
                return false;
            }

            m_fishHandles.Add(handle);
            m_fishProgress.Add(0f);
            OnAquariumChanged?.Invoke();
            return true;
        }

        /// <summary>이 개체를 배치할 수 있는지 검사한다(보유·중복·수용량). 불가 시 사유를 함께 반환한다.</summary>
        public bool CanPlaceFish(EntityHandle handle, out string reason)
        {
            reason = string.Empty;

            var e = FishEntity(handle);
            if (e == null)
            {
                reason = "물고기 개체를 찾을 수 없습니다";
                return false;
            }
            if (m_inventory == null)
            {
                reason = "인벤토리를 사용할 수 없습니다";
                return false;
            }
            if (m_fishHandles.Contains(handle))
            {
                reason = "이미 배치된 물고기입니다";
                return false;
            }

            int capacity = e.ItemData.AquariumCapacity;
            if (UsedCapacity + capacity > MaxCapacity)
            {
                reason = $"수용량 부족 (필요 {capacity} · 남은 {MaxCapacity - UsedCapacity})";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 종(dataId)으로 배치한다. 보유 개체 중 첫 번째를 골라 <see cref="AddFish(EntityHandle)"/>에 위임한다.
        /// 어느 개체가 배치될지 통제할 수 없으므로 디버그·자동화 경로에서 쓴다.
        /// </summary>
        public bool AddFish(int fishDataId)
        {
            if (m_inventory == null)
            {
                Debug.LogWarning("[AquariumSystem] AddFish 실패 — InventorySystem 없음");
                return false;
            }
            EntityHandle[] slots = m_inventory.GetSlots(ItemType.Fish);
            for (int i = 0; i < slots.Length; i++)
            {
                var e = EntityManager.Get<Entity_Fish>(slots[i]);
                if (e != null && e.DataId == fishDataId)
                {
                    return AddFish(slots[i]);
                }
            }
            return false;   // 보유 개체 없음
        }

        /// <summary>핸들로 지목한 배치 개체를 회수한다.</summary>
        public bool RemoveFish(EntityHandle handle) => RemoveFishAt(m_fishHandles.IndexOf(handle));

        /// <summary>
        /// 지정 인덱스의 배치 물고기를 회수해 인벤토리로 되돌린다. 선검사에 걸리면 회수하지 않는다.
        /// 인덱스는 내부 저장 순서이며 표시 목록의 행 순서와 무관하다.
        /// </summary>
        public bool RemoveFishAt(int index)
        {
            if (index < 0 || index >= m_fishHandles.Count)
            {
                return false;
            }
            EntityHandle handle = m_fishHandles[index];

            // UI를 거치지 않는 호출 경로 방어 — 회수가 판매로 뒤바뀌는 것을 여기서 최종 차단한다.
            if (CanRetrieveFish(handle, out string reason) == false)
            {
                Debug.LogWarning($"[AquariumSystem] 회수 차단 — {reason}");
                return false;
            }

            // 선검사를 통과했으므로 m_inventory는 non-null이다(CanRetrieveFish가 null을 차단).
            if (m_inventory.AddItem(handle) == false)
            {
                Debug.LogWarning("[AquariumSystem] 회수 실패 — 인벤토리가 받지 않음(공간 확보 후 재시도).");
                return false;   // 회수 취소: 개체 손실 없이 아쿠아리움에 유지
            }

            m_fishHandles.RemoveAt(index);
            m_fishProgress.RemoveAt(index);
            OnAquariumChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 이 개체가 회수 시 판매되지 않고 인벤토리로 돌아오는지 선검사한다. 불가 시 사유를 함께 반환한다.
        /// 판정 기준은 InventorySystem의 자동판매 조건(물고기 창고 만석 · 자동판매 필터 대상)이며,
        /// 그 정책이 바뀌면 이 함수도 함께 갱신해야 한다.
        /// </summary>
        public bool CanRetrieveFish(EntityHandle handle, out string reason)
        {
            reason = string.Empty;

            var e = FishEntity(handle);
            if (e == null)
            {
                reason = "물고기 개체를 찾을 수 없습니다";
                return false;
            }
            if (m_inventory == null)
            {
                reason = "인벤토리를 사용할 수 없습니다";
                return false;
            }

            // (1) 물고기 창고 만석 — AddItem은 슬롯 확장보다 먼저 판매 판정을 한다.
            if (m_inventory.GetUsedSlotCount(ItemType.Fish) >= m_inventory.GetMaxSlotCount(ItemType.Fish))
            {
                reason = "물고기 창고가 가득 찼습니다";
                return false;
            }

            // (2) 자동판매 필터 대상
            if (m_inventory.AutoSellEnabled
                && e.Quality <= m_inventory.MaxAutoSellQuality
                && e.Rarity <= m_inventory.MaxAutoSellRarity)
            {
                reason = "자동판매 대상이라 회수 시 판매됩니다 — 자동판매 설정을 조정하세요";
                return false;
            }
            return true;
        }

        // 인벤토리 Fish 슬롯에서 그 핸들을 찾아 슬롯에서 뺀다(개체는 파괴하지 않음).
        private bool TryTakeFromInventory(EntityHandle handle)
        {
            EntityHandle[] slots = m_inventory.GetSlots(ItemType.Fish);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Equals(handle))
                {
                    return m_inventory.RemoveAt(ItemType.Fish, i, destroyEntity: false);
                }
            }
            return false;
        }

        // ── 업그레이드 ──

        /// <summary>강화 재료 1종의 요구량과 보유량.</summary>
        public readonly struct MaterialRequirement
        {
            public readonly int MaterialId;
            public readonly string MaterialName;
            public readonly int Required;
            public readonly int Owned;

            public MaterialRequirement(int materialId, string materialName, int required, int owned)
            {
                MaterialId = materialId; MaterialName = materialName; Required = required; Owned = owned;
            }

            public bool IsSatisfied => Owned >= Required;
        }

        /// <summary>다음 등급 강화의 가능 여부·비용·보유량·차단 사유.</summary>
        public readonly struct AquariumUpgradeInfo
        {
            public readonly bool HasNext;
            public readonly bool CanUpgrade;
            public readonly int NextLevel;
            public readonly string NextName;    // 다음 등급 이름(AquariumUpgradeData.Name)
            public readonly int NextMaxCapacity;
            public readonly int GoldCost;
            public readonly int GoldOwned;
            public readonly IReadOnlyList<MaterialRequirement> Materials;
            public readonly string BlockReason;   // 표시용(빈 문자열 = 가능)

            public AquariumUpgradeInfo(bool hasNext, bool canUpgrade, int nextLevel, string nextName,
                int nextMaxCapacity, int goldCost, int goldOwned,
                IReadOnlyList<MaterialRequirement> materials, string blockReason)
            {
                HasNext = hasNext; CanUpgrade = canUpgrade;
                NextLevel = nextLevel; NextName = nextName; NextMaxCapacity = nextMaxCapacity;
                GoldCost = goldCost; GoldOwned = goldOwned;
                Materials = materials; BlockReason = blockReason;
            }

            public static AquariumUpgradeInfo None(string reason)
                => new AquariumUpgradeInfo(false, false, 0, string.Empty, 0, 0, 0, s_noMaterials, reason);
        }

        private static readonly MaterialRequirement[] s_noMaterials = Array.Empty<MaterialRequirement>();

        /// <summary>다음 등급 강화 정보를 만든다. 최대 등급이면 HasNext=false로 반환한다.</summary>
        public AquariumUpgradeInfo GetUpgradeInfo()
        {
            var next = NextUpgrade;
            if (next == null)
            {
                return AquariumUpgradeInfo.None("최대 등급");
            }

            int gold = CurrentGold();
            bool canUpgrade = gold >= next.UpgradeCost;
            string reason = canUpgrade ? string.Empty : "골드 부족";

            var materials = new List<MaterialRequirement>();
            if (next.MaterialCosts != null)
            {
                foreach (var cost in next.MaterialCosts)
                {
                    if (cost?.Material == null) continue;

                    int owned = m_inventory != null
                        ? m_inventory.GetTotalQuantityByDataId(ItemType.Materials, cost.Material.ID)
                        : 0;
                    materials.Add(new MaterialRequirement(cost.Material.ID, cost.Material.Name, cost.Count, owned));

                    if (owned < cost.Count)
                    {
                        canUpgrade = false;
                        if (reason.Length == 0) reason = "재료 부족";
                    }
                }
            }

            if (m_player == null && next.UpgradeCost > 0)
            {
                canUpgrade = false;
                reason = "플레이어 정보 없음";
            }

            return new AquariumUpgradeInfo(true, canUpgrade, m_upgradeLevel + 1, next.Name, next.MaxCapacity,
                next.UpgradeCost, gold, materials, canUpgrade ? string.Empty : reason);
        }

        /// <summary>다음 등급으로 강화한다. 골드·재료를 모두 검증한 뒤에만 소비하므로 부분 차감이 없다.</summary>
        public bool TryUpgrade()
        {
            AquariumUpgradeInfo info = GetUpgradeInfo();
            if (info.HasNext == false || info.CanUpgrade == false)
            {
                return false;
            }

            var next = NextUpgrade;
            if (next == null)
            {
                return false;
            }

            if (info.GoldCost > 0)
            {
                Entity_Player player = CurrentPlayer();
                if (player == null)
                {
                    return false;
                }
                player.AddGold(-info.GoldCost);
            }

            if (next.MaterialCosts != null && m_inventory != null)
            {
                foreach (var cost in next.MaterialCosts)
                {
                    if (cost?.Material == null) continue;
                    m_inventory.ConsumeItemByDataId(ItemType.Materials, cost.Material.ID, cost.Count);
                }
            }

            m_upgradeLevel++;
            OnAquariumChanged?.Invoke();
            return true;
        }

        private Entity_Player CurrentPlayer()
            => m_player != null ? EntityManager.Get<Entity_Player>(m_player.PlayerHandle) : null;

        private int CurrentGold()
        {
            Entity_Player player = CurrentPlayer();
            return player != null ? player.Gold : 0;
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

                float cycle = EffectiveCycle(f, e.Quality);   // 개체 성급 반영
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
            OnAquariumProgress?.Invoke();   // 수치만 변경 — 목록 재빌드 없음
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

        /// <summary>
        /// 개체 성급만큼 단축된 유효 생산 주기(초)를 반환한다. 하한은 <see cref="MinCycleSeconds"/>다.
        /// </summary>
        private float EffectiveCycle(ItemData_Fish fish, ItemQuality quality)
            => Mathf.Max(MinCycleSeconds, fish.AquariumProduceTime - ((int)quality - 1) * fish.AquariumDecreaseCount);

        private static ItemQuality NormalizeQuality(int q) => (ItemQuality)Mathf.Clamp(q, 1, 5);

        // ── View용 Data/지표 접근자 ──

        /// <summary>dataId로 물고기 종 정의(ItemData_Fish)를 조회한다.</summary>
        public ItemData_Fish GetFishData(int id) => FishData(id);

        /// <summary>dataId로 재료 정의(ItemData_Materials)를 조회한다.</summary>
        public ItemData_Materials GetMaterialData(int id) => DataManager.GetData<ItemData_Materials>(id);

        /// <summary>개체 성급 단축을 반영한 시간당 생산 포인트를 반환한다.</summary>
        public float PointsPerHour(ItemData_Fish fish, ItemQuality quality)
            => fish == null ? 0f : fish.AquariumProduceAmount * 3600f / EffectiveCycle(fish, quality);

        /// <summary>개체 성급 단축을 반영한 분당 생산 포인트를 반환한다.</summary>
        public float PointsPerMinute(ItemData_Fish fish, ItemQuality quality)
            => fish == null ? 0f : fish.AquariumProduceAmount * 60f / EffectiveCycle(fish, quality);

        /// <summary>그 재료의 시간당 생산 개수를 반환한다. 배치된 개체들의 시간당 포인트 합을 요구 포인트로 나눈 값이다.</summary>
        public float MaterialsPerHour(int materialId)
        {
            var mat = DataManager.GetData<ItemData_Materials>(materialId);
            if (mat == null)
            {
                return 0f;
            }

            float pointsPerHour = 0f;
            for (int i = 0; i < m_fishHandles.Count; i++)
            {
                var e = FishEntity(m_fishHandles[i]);
                if (e == null || e.ItemData.AquariumMaterial == null) continue;
                if (e.ItemData.AquariumMaterial.ID != materialId) continue;

                pointsPerHour += PointsPerHour(e.ItemData, e.Quality);
            }
            return pointsPerHour / mat.AquariumRequiredPoints;
        }

        // ── 표시용 상태 구조 ──

        /// <summary>
        /// 물고기 1개체의 표시 정보. 인벤토리 개체와 배치 개체에 함께 쓴다.
        /// <see cref="RemainingSeconds"/>는 배치 개체에서만 유효하고 인벤토리 개체는 0이다.
        /// </summary>
        public readonly struct AquariumFishInfo
        {
            public readonly EntityHandle Handle;
            public readonly int DataId;
            public readonly string Name;
            public readonly int Tier;
            public readonly ItemRarity Rarity;
            public readonly ItemQuality Star;   // 개체 롤값(Entity_Fish.Quality)
            public readonly float Size;         // 개체 롤값
            public readonly int MaterialId;
            public readonly string MaterialName;
            public readonly float PointsPerMinute;
            public readonly float CycleSeconds;
            public readonly float RemainingSeconds;
            public readonly int Capacity;       // 이 개체가 차지하는 수용량

            public AquariumFishInfo(EntityHandle handle, int dataId, string name, int tier, ItemRarity rarity,
                ItemQuality star, float size, int materialId, string materialName,
                float pointsPerMinute, float cycleSeconds, float remainingSeconds, int capacity)
            {
                Handle = handle; DataId = dataId; Name = name; Tier = tier; Rarity = rarity;
                Star = star; Size = size; MaterialId = materialId; MaterialName = materialName;
                PointsPerMinute = pointsPerMinute; CycleSeconds = cycleSeconds;
                RemainingSeconds = remainingSeconds; Capacity = capacity;
            }

            /// <summary>유효한 개체로부터 만들어졌는지. 조회에 실패하면 default가 반환된다.</summary>
            public bool IsValid => DataId != 0;
        }

        public readonly struct MaterialStatus
        {
            public readonly int MaterialId;
            public readonly string MaterialName;
            public readonly int Points;
            public readonly int Required;
            public readonly int Pending;
            public readonly float PerHour;   // 시간당 생산 개수

            public MaterialStatus(int materialId, string materialName, int points, int required, int pending, float perHour)
            {
                MaterialId = materialId; MaterialName = materialName;
                Points = points; Required = required; Pending = pending; PerHour = perHour;
            }
        }

        /// <summary>개체 1마리의 표시 정보를 만든다. 배치되지 않은 인벤토리 개체도 조회할 수 있다.</summary>
        public AquariumFishInfo BuildFishInfo(EntityHandle handle)
            => BuildFishInfo(handle, m_fishHandles.IndexOf(handle), SecondsSinceSettle());

        /// <summary>배치된 물고기 전체의 표시 정보를 만든다. 다음 생산까지 남은 시간이 포함된다.</summary>
        public List<AquariumFishInfo> GetPlacedFishInfos()
        {
            var list = new List<AquariumFishInfo>(m_fishHandles.Count);
            double sinceSettle = SecondsSinceSettle();
            for (int i = 0; i < m_fishHandles.Count; i++)
            {
                AquariumFishInfo info = BuildFishInfo(m_fishHandles[i], i, sinceSettle);
                if (info.IsValid) list.Add(info);
            }
            return list;
        }

        // placedIndex < 0 이면 인벤토리 개체(남은 시간 없음). sinceSettle은 호출자가 1회만 계산해 넘긴다.
        private AquariumFishInfo BuildFishInfo(EntityHandle handle, int placedIndex, double sinceSettle)
        {
            var e = FishEntity(handle);
            if (e == null)
            {
                return default;
            }
            var f = e.ItemData;
            var mat = f.AquariumMaterial;

            float cycle = EffectiveCycle(f, e.Quality);
            float remaining = 0f;
            if (placedIndex >= 0 && placedIndex < m_fishProgress.Count)
            {
                remaining = (float)Math.Max(0.0, cycle - (m_fishProgress[placedIndex] + sinceSettle));
            }

            return new AquariumFishInfo(
                handle, f.ID, f.Name, f.Tier, f.Rarity, e.Quality, e.Size,
                mat != null ? mat.ID : 0,
                mat != null ? mat.Name : "-",
                PointsPerMinute(f, e.Quality), cycle, remaining, f.AquariumCapacity);
        }

        private double SecondsSinceSettle() => Math.Max(0.0, (DateTime.UtcNow - m_lastSettleUtc).TotalSeconds);

        /// <summary>생산 중이거나 보류 중인 모든 재료의 상태(누적·요구 포인트, 보류 개수, 시간당 생산 개수)를 만든다.</summary>
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
                list.Add(new MaterialStatus(id, mat.Name, points, mat.AquariumRequiredPoints, pending, MaterialsPerHour(id)));
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

            // 배치 물고기 개체 복원: 저장된 handle로 Entity를 재등록하고 롤값을 주입한다.
            m_fishHandles.Clear();
            m_fishProgress.Clear();
            if (s.fish != null)
            {
                foreach (var fe in s.fish)
                {
                    if (EntityHandle.TryParse(fe.handle, out EntityHandle h) == false) continue;
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
