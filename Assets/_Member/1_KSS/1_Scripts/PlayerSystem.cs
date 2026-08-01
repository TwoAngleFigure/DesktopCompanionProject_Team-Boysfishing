using System;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
// [세이브 관련 주석 처리 해제]
using DesktopCompanion.Save;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 플레이어의 기본 스탯을 관리하고, 장비 장착에 따른 스탯 변화를 실시간으로 계산하는 시스템입니다.
    /// </summary>
    // [세이브 관련 주석 처리 해제] ISaveable 상속 활성화
    public class PlayerSystem : SystemBase, ISaveable
    {
        private PlayerSave m_loadedSave;
        private EntityHandle playerHandle;
        private Entity_Player entity_Player;
        private InventorySystem m_inventorySystem;

        public EntityHandle PlayerHandle => playerHandle;

        public event Action<EntityHandle> OnStatChanged;
        // [추가] 장비 변경이나 수량 변경 시 스탯 계산 없이 UI만 갱신하기 위한 이벤트
        public event Action<EntityHandle> OnEquipmentChanged;

        #region Stats

        [Header("Battle")]
        [SerializeField] private int m_baseDamagePerClick;
        [SerializeField] private float m_baseManualDamagePerHitMultiply;
        [SerializeField] private int m_baseBattleTimeVariable;
        [SerializeField] private float m_baseCriticalChance;
        [SerializeField] private float m_baseCriticalMultiply;

        [Header("Auto Battle")]
        [SerializeField] private float m_baseAutoBattleCooltime;
        [SerializeField] private float m_baseAutoSpeedPerTime;
        [SerializeField] private float m_baseAutoDamagePerHitMultiply;

        [Header("Battle Reward")]
        [SerializeField] private float m_baseProbabilityAtFishSize;
        [SerializeField] private float m_baseProbabilityAtFishRarity;
        [SerializeField] private float m_baseGoldGettingMultiply;

        [Header("Other")]
        [SerializeField] private float m_baseMapMovementSpeedPerTime;
        [SerializeField] private int m_baseInventorySize;
        [SerializeField] private int m_startingLicense;
        [SerializeField] private int m_startingGold;

        public int BaseDamagePerClick => m_baseDamagePerClick;
        public float BaseManualDamagePerHitMultiply => m_baseManualDamagePerHitMultiply;
        public int BaseBattleTimeVariable => m_baseBattleTimeVariable;
        public float BaseCriticalChance => m_baseCriticalChance;
        public float BaseCriticalMultiply => m_baseCriticalMultiply;

        public float BaseAutoBattleCooltime => Mathf.Max(0.1f, m_baseAutoBattleCooltime);
        public float BaseAutoSpeedPerTime => 1f / Mathf.Max(0.1f, m_baseAutoSpeedPerTime);
        public float BaseAutoDamagePerHitMultiply => m_baseAutoDamagePerHitMultiply;

        public float BaseProbabilityAtFishSize => m_baseProbabilityAtFishSize;
        public float BaseProbabilityAtFishRarity => m_baseProbabilityAtFishRarity;
        public float BaseGoldGettingMultiply => m_baseGoldGettingMultiply;

        public float BaseMapMovementSpeedPerTime => m_baseMapMovementSpeedPerTime;
        public int BaseInventorySize => m_baseInventorySize;
        public int StartingLicense => m_startingLicense;
        public int StartingGold => m_startingGold;

        #endregion

        #region Save

        public string SaveId => "player_system_stats";
        public Type StateType => typeof(PlayerSave);

        public object CaptureState()
        {
            PlayerSave save = new PlayerSave();

            if (entity_Player != null)
            {
                save.currentLicense = entity_Player.CurrentLicense;
                save.gold = entity_Player.Gold;

                foreach (var kvp in entity_Player.Equipped)
                {
                    if (kvp.Value.Value != Guid.Empty)
                    {
                        Entity entity = EntityManager.Get(kvp.Value);
                        if (entity is Entity_Equipment equip)
                        {
                            save.equippedItems.Add(new PlayerSave.EquippedItemSave
                            {
                                area = kvp.Key,
                                handle = kvp.Value.ToString(),
                                dataId = equip.DataId,
                                upgradeLevel = equip.UpgradeLevel,
                                quantity = 1
                            });
                        }
                        else if (entity is Entity_Consumables consumable)
                        {
                            save.equippedItems.Add(new PlayerSave.EquippedItemSave
                            {
                                area = kvp.Key,
                                handle = kvp.Value.ToString(),
                                dataId = consumable.DataId,
                                upgradeLevel = 0,
                                quantity = consumable.Quantity
                            });
                        }
                    }
                }
            }

            save.bonusInventorySize = m_bonusInventorySize;
            save.currentStorageUpgradeCost = m_currentStorageUpgradeCost;

            return save;
        }

        public void RestoreState(object state)
        {
            if (state is PlayerSave save)
            {
                m_loadedSave = save;
                Debug.Log("[PlayerSystem] Player save loaded pending PostInitialize.");
            }
        }

        private void RestoreLoadedSave()
        {
            if (entity_Player != null)
            {
                entity_Player.SetLicense(m_loadedSave.currentLicense);
                entity_Player.SetGold(m_loadedSave.gold);

                foreach (var equipSave in m_loadedSave.equippedItems)
                {
                    if (EntityHandle.TryParse(equipSave.handle, out EntityHandle parsedHandle))
                    {
                        if (equipSave.area == EquipmentMountingArea.Bait || equipSave.area == EquipmentMountingArea.Groundbait)
                        {
                            EntityHandle restoredHandle = EntityManager.Restore<ItemData_Consumables>(parsedHandle, equipSave.dataId);
                            if (EntityManager.Get(restoredHandle) is Entity_Consumables consumable)
                            {
                                consumable.SetQuantity(Math.Max(1, equipSave.quantity));
                                entity_Player.Equip(equipSave.area, restoredHandle);
                            }
                        }
                        else
                        {
                            EntityHandle restoredHandle = EntityManager.Restore<ItemData_Equipment>(parsedHandle, equipSave.dataId);
                            if (EntityManager.Get(restoredHandle) is Entity_Equipment equip)
                            {
                                equip.SetUpgradeLevel(Math.Max(0, equipSave.upgradeLevel));
                                entity_Player.Equip(equipSave.area, restoredHandle);
                            }
                        }
                    }
                }
            }

            m_bonusInventorySize = m_loadedSave.bonusInventorySize;
            m_currentStorageUpgradeCost = m_loadedSave.currentStorageUpgradeCost;

            Debug.Log("[PlayerSystem] Player save restored successfully.");
        }

        #endregion

        public override void Initialize()
        {
            playerHandle = EntityManager.Create<PlayerData>(1);
            entity_Player = EntityManager.Get<Entity_Player>(playerHandle);
        }

        public override void PostInitialize()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
            Entity_Player entity_Player = EntityManager.Get<Entity_Player>(playerHandle);

            if (m_loadedSave != null)
            {
                RestoreLoadedSave();
                m_loadedSave = null;
            }

            m_startingLicense = entity_Player.CurrentLicense;
            m_startingGold = entity_Player.Gold;

            CaculatedStat();
        }

        public void InitializeStat()
        {
            m_baseDamagePerClick = entity_Player.BaseData.BaseDamagePerClick;
            m_baseManualDamagePerHitMultiply = entity_Player.BaseData.BaseManualDamagePerHitMultiply;
            m_baseBattleTimeVariable = entity_Player.BaseData.BaseBattleTimeVariable;
            m_baseCriticalChance = entity_Player.BaseData.BaseCriticalChance;
            m_baseCriticalMultiply = entity_Player.BaseData.BaseCriticalMultiply;

            m_baseAutoBattleCooltime = entity_Player.BaseData.BaseAutoBattleCooltime;
            m_baseAutoSpeedPerTime = entity_Player.BaseData.BaseAutoSpeedPerTime;
            m_baseAutoDamagePerHitMultiply = entity_Player.BaseData.BaseAutoDamagePerHitMultiply;

            m_baseProbabilityAtFishSize = entity_Player.BaseData.BaseProbabilityAtFishSize;
            m_baseProbabilityAtFishRarity = entity_Player.BaseData.BaseProbabilityAtFishRarity;
            m_baseGoldGettingMultiply = entity_Player.BaseData.BaseGoldGettingMultiply;

            m_baseMapMovementSpeedPerTime = entity_Player.BaseData.BaseMapMovementSpeedPerTime;
            m_baseInventorySize = entity_Player.BaseData.BaseInventorySize;
        }

        public void CaculatedStat()
        {
            // 여기서 캐릭터의 기본 스탯은 어떠한 경우에도 무조건 갱신됩니다!
            InitializeStat();

            foreach (EntityHandle equitmentHandle in entity_Player.Equipped.Values)
            {
                // 🛡️ [방어 코드 1] 슬롯이 비어있으면 해당 칸의 추가 스탯 계산만 건너뜁니다.
                if (equitmentHandle.Value == Guid.Empty) continue;

                Entity entity = EntityManager.Get(equitmentHandle);
                if (entity == null) continue;

                StatModifier[] modifiers = null;

                if (entity is Entity_Equipment equipment && equipment.ItemData != null)
                {
                    modifiers = equipment.CurrentModifiers;
                }
                else if (entity is Entity_Consumables consumable && consumable.ItemData != null)
                {
                    modifiers = consumable.ItemData.Modifiers;
                }

                if (modifiers == null) continue;

                foreach (StatModifier stat in modifiers)
                {
                    switch (stat.Stat)
                    {
                        case PlayerStat.DamagePerClick:
                            m_baseDamagePerClick += (int)stat.Value;
                            break;
                        case PlayerStat.ManualDamagePerHitMultiply:
                            m_baseManualDamagePerHitMultiply += stat.Value;
                            break;
                        case PlayerStat.BattleTimeVariable:
                            m_baseBattleTimeVariable += (int)stat.Value;
                            break;
                        case PlayerStat.CriticalChance:
                            m_baseCriticalChance += stat.Value;
                            break;
                        case PlayerStat.CriticalMultiply:
                            m_baseCriticalMultiply += stat.Value;
                            break;
                        case PlayerStat.AutoBattleCooltime:
                            m_baseAutoBattleCooltime -= stat.Value;
                            break;
                        case PlayerStat.AutoSpeedPerTime:
                            m_baseAutoSpeedPerTime += stat.Value;
                            break;
                        case PlayerStat.AutoDamagePerHitMultiply:
                            m_baseAutoDamagePerHitMultiply += stat.Value;
                            break;
                        case PlayerStat.ProbabilityAtFishSize:
                            m_baseProbabilityAtFishSize += stat.Value;
                            break;
                        case PlayerStat.ProbabilityAtFishRarity:
                            m_baseProbabilityAtFishRarity += stat.Value;
                            break;
                        case PlayerStat.GoldGettingMultiply:
                            m_baseGoldGettingMultiply += stat.Value;
                            break;
                        case PlayerStat.MapMovementSpeedPerTime:
                            m_baseMapMovementSpeedPerTime += stat.Value;
                            break;
                        case PlayerStat.InventorySize:
                            m_baseInventorySize += (int)stat.Value;
                            break;
                    }
                }
            }

            m_baseInventorySize += m_bonusInventorySize;
            OnStatChanged?.Invoke(playerHandle);
        }

        private bool TryFindAndRemoveFromInventory(InventorySystem inventory, EntityHandle handle)
        {
            ItemType[] types = { ItemType.Fish, ItemType.Equipment, ItemType.Materials, ItemType.Consumables };

            foreach (var type in types)
            {
                EntityHandle[] slots = inventory.GetSlots(type);
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].Equals(handle))
                    {
                        return inventory.RemoveAt(type, i, false);
                    }
                }
            }
            return false;
        }

        public void Equip(EquipmentMountingArea area, EntityHandle afterEquipHandle)
        {
            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);

            // [추가된 방어 코드] 기존에 장비가 없고, 새로 장착할 장비도 없는 경우 불필요한 로직 없이 조용히 종료
            bool hasEquippedItem = player.Equipped.TryGetValue(area, out EntityHandle beforeEquipHandle) && beforeEquipHandle.Value != Guid.Empty;
            if (!hasEquippedItem && afterEquipHandle.Value == Guid.Empty)
            {
                return;
            }

            // 1. 새로 장착할 아이템이 빈값이 아닐 때 인벤토리에서 먼저 제거 (병합 꼬임 방지)
            if (afterEquipHandle.Value != Guid.Empty && m_inventorySystem != null)
            {
                bool isRemoved = TryFindAndRemoveFromInventory(m_inventorySystem, afterEquipHandle);
                if (!isRemoved)
                {
                    Debug.LogWarning("[PlayerSystem] 인벤토리에서 장착할 아이템을 찾지 못하거나 제거에 실패했습니다.");
                }
            }

            // 2. 기존 장비 해제 및 인벤토리 반환 (새 장비가 이미 빠져나간 상태이므로 병합 안전)
            if (hasEquippedItem)
            {
                player.Unequip(area);
                m_inventorySystem?.AddItem(beforeEquipHandle);
            }

            // 3. 새 아이템 장착 (빈값이면 빈값대로 덮어씌워 완벽한 해제 상태로 만듦)
            player.Equip(area, afterEquipHandle);

            // 4. 스탯 재계산 (기본 스탯 + 장착 장비 스탯 안전하게 갱신)
            CaculatedStat();

            // [디버그] 정상적으로 장착되었음을 알리는 로그 출력
            Debug.Log($"[PlayerSystem] {area} 슬롯 장비 갱신 완료.");
        }

        /// <summary>
        /// 특정 장비 슬롯에 장착된 아이템의 이름을 반환합니다. (UI 표시용)
        /// </summary>
        public string GetEquippedItemName(EquipmentMountingArea area)
        {
            // [예외 처리] 플레이어 엔티티가 없거나 핸들이 비어있을 경우 널포인터 에러 방지용으로 null 반환
            if (playerHandle.Value == Guid.Empty) return null;

            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);
            if (player != null && player.Equipped.TryGetValue(area, out EntityHandle handle))
            {
                Entity entity = EntityManager.Get(handle);
                if (entity is Entity_Equipment equipment) return equipment.Name;
                if (entity is Entity_Consumables consumable) return consumable.Name;
                if (entity is Entity_Materials material) return material.Name;
                return null;
            }
            return null;
        }

        public EntityHandle GetEquippedItemHandle(EquipmentMountingArea area)
        {
            if (playerHandle.Value == Guid.Empty) return default;

            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);
            if (player != null && player.Equipped.TryGetValue(area, out EntityHandle handle))
            {
                return handle;
            }

            return default;
        }

        /// <summary>
        /// 낚시 시스템 등에서 장착 중인 아이템(주로 소모품)을 소모할 때 호출합니다.
        /// </summary>
        /// <param name="area">소모할 아이템이 장착된 부위 (예: Bait, Groundbait)</param>
        /// <param name="amount">소모할 수량</param>
        /// <returns>소모에 성공하면 true, 수량이 부족하거나 아이템이 없으면 false</returns>
        public bool TryConsumeEquippedItem(EquipmentMountingArea area, int amount = 1)
        {
            if (playerHandle.Value == Guid.Empty) return false;

            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);
            if (player == null || !player.Equipped.TryGetValue(area, out EntityHandle handle))
            {
                return false; // 장착된 아이템이 없음
            }

            if (handle.Value == Guid.Empty) return false;

            Entity entity = EntityManager.Get(handle);
            if (entity is Entity_Consumables consumable)
            {
                // 소모할 수량 확인
                if (consumable.Quantity >= amount)
                {
                    consumable.Add(-amount);

                    // 수량이 0 이하가 되면 장착 해제 및 엔티티 파기
                    if (consumable.Quantity <= 0)
                    {
                        player.Unequip(area);
                        EntityManager.Destroy(handle);
                        
                        // 완전히 다 소모되어 장착 해제될 때만 스탯 계산 (비용이 큼)
                        CaculatedStat();
                    }
                    else
                    {
                        // 수량만 변했을 때는 스탯 재계산 없이 UI 갱신 이벤트만 발생
                        OnEquipmentChanged?.Invoke(playerHandle);
                    }

                    return true;
                }
            }
            else if (entity is Entity_Materials material)
            {
                if (material.Quantity >= amount)
                {
                    material.Add(-amount);
                    if (material.Quantity <= 0)
                    {
                        player.Unequip(area);
                        EntityManager.Destroy(handle);
                        CaculatedStat();
                    }
                    else
                    {
                        OnEquipmentChanged?.Invoke(playerHandle);
                    }
                    return true;
                }
            }

            return false;
        }

        // =========================================================
        // 물고기 창고 강화 로직 (골드 소모 및 1.25배 비용 증가)
        // =========================================================
        private int m_bonusInventorySize = 0; // 강화로 영구적으로 늘어난 인벤토리 칸 수

        // [나중에 수정할 부분] 현재 0으로 두어 무한 테스트 가능. 실전 시 100 등으로 변경!
        private int m_currentStorageUpgradeCost = 0;
        private float m_storageUpgradeCostMultiplier = 1.25f; // 비용 1.25배 증가

        private const int MAX_INVENTORY_SIZE = 150; // 최대 인벤토리 확장 제한

        public void UpgradeFishStorage()
        {
            if (playerHandle.Value == Guid.Empty) return;

            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);

            if (player.BaseData.BaseInventorySize + m_bonusInventorySize >= MAX_INVENTORY_SIZE)
            {
                Debug.LogWarning($"[물고기 창고] 이미 최대 크기({MAX_INVENTORY_SIZE}칸)에 도달하여 더 이상 확장할 수 없습니다.");
                return;
            }

            // 플레이어의 골드가 업그레이드 비용보다 같거나 많은지 확인
            if (player != null && player.Gold >= m_currentStorageUpgradeCost)
            {
                // 1. 골드 차감
                player.AddGold(-m_currentStorageUpgradeCost);

                // 2. 인벤토리 크기 1 증가
                m_bonusInventorySize += 1;

                // 3. 다음 업그레이드 비용 1.25배 계산 
                // (Mathf.CeilToInt를 써서 소수점은 올림 처리합니다. 예: 125.5 골드 -> 126 골드)
                // 현재는 0 * 1.25 이므로 계속 0이 됩니다.
                m_currentStorageUpgradeCost = Mathf.CeilToInt(m_currentStorageUpgradeCost * m_storageUpgradeCostMultiplier);

                // 4. 스탯 재계산 및 UI/인벤토리 자동 확장 방송(OnStatChanged) 송출!
                CaculatedStat();

                Debug.Log($"[물고기 창고] 강화 성공! 총 추가 칸 수: {m_bonusInventorySize} / 다음 필요 골드: {m_currentStorageUpgradeCost}");
            }
            else
            {
                Debug.LogWarning($"[물고기 창고] 골드가 부족합니다! (필요 골드: {m_currentStorageUpgradeCost} / 보유 골드: {player?.Gold})");
            }
        }

        // =========================================================
        // 장비 강화 로직
        // =========================================================
        public bool TryReinforceEquipment(EntityHandle equipHandle)
        {
            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);
            Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(equipHandle);

            if (player == null || equipment == null) return false;

            // 1. 다음 강화 데이터 조회
            DesktopCompanion.Data.UpgradeStep nextStep = equipment.ItemData.GetNextUpgradeStep(equipment.UpgradeLevel);
            if (nextStep == null)
            {
                Debug.LogWarning("최대 강화 레벨입니다.");
                return false;
            }

            // 2. 비용 검증 (골드)
            if (player.Gold < nextStep.GoldCost)
            {
                Debug.LogWarning("골드가 부족합니다.");
                return false;
            }

            // 3. 비용 검사 (재료)
            if (nextStep.MaterialCosts != null)
            {
                foreach (var mat in nextStep.MaterialCosts)
                {
                    if (mat.Material != null)
                    {
                        if (m_inventorySystem.GetTotalQuantityByDataId(DesktopCompanion.Data.ItemType.Materials, mat.Material.ID) < mat.Count)
                        {
                            Debug.LogWarning("재료가 부족합니다.");
                            return false;
                        }
                    }
                }
            }

            // 4. 비용 차감
            player.AddGold(-nextStep.GoldCost);

            // 재료 차감
            if (nextStep.MaterialCosts != null)
            {
                foreach (var mat in nextStep.MaterialCosts)
                {
                    if (mat.Material != null)
                    {
                        m_inventorySystem.ConsumeItemByDataId(DesktopCompanion.Data.ItemType.Materials, mat.Material.ID, mat.Count);
                    }
                }
            }

            // 5. 실제 강화 처리 (데이터는 변경하지 않고 개체의 상태만 업데이트)
            equipment.SetUpgradeLevel(equipment.UpgradeLevel + 1);

            // 6. 스탯 갱신
            CaculatedStat();

            Debug.Log($"{equipment.ItemData.Name} 장비가 +{equipment.UpgradeLevel}강으로 강화되었습니다.");
            return true;
        }
    }
}