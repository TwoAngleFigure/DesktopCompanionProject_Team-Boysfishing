using System;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>플레이어 상태·장착·저장을 관리하고, 계산 결과 조회와 변경 알림을 제공한다.</summary>
    public class PlayerSystem_New : SystemBase, ISaveable
    {
        private EntityHandle m_playerHandle;
        private PlayerSave_New m_loadedSave;
        private StatCalculationResult m_stats = new StatCalculationResult();
        private float m_autoAttackInterval = 1f;
        private int m_bonusInventorySize;
        private int m_currentStorageUpgradeCost; // 비용 설정은 기존 테스트값 0을 유지한다.
        private const float StorageUpgradeCostMultiplier = 1.25f;
        private const int MaxInventorySize = 150;

        public EntityHandle PlayerHandle => m_playerHandle;
        public event Action<EntityHandle> OnStatChanged;
        public event Action<EntityHandle> OnEquipmentChanged;

        public float HealthPoint => m_stats.HealthPoint;
        public float AttackPoint => m_stats.AttackPoint;
        public float FlatAttackPoint => m_stats.FlatAttackPoint;
        public float ArmorPenetration => m_stats.ArmorPenetration;
        public float MultiHitChance => m_stats.MultiHitChance;
        public float CriticalChance => m_stats.CriticalChance;
        public float CriticalDamage => m_stats.CriticalDamage;
        public float CCDuration => m_stats.CCDuration;
        public float BiteTime => m_stats.BiteTime;
        public float HookingTiming => m_stats.HookingTiming;
        public float SizeModifier => m_stats.SizeModifier;
        public float RarityProbability => m_stats.RarityProbability;
        public float GoldGettingMultiply => m_stats.GoldGettingMultiply;
        public float MapMovementSpeedPerTime => m_stats.MapMovementSpeedPerTime;
        public int InventorySize => m_stats.InventorySize;
        public float AutoAttackInterval => m_autoAttackInterval;

        public string SaveId => "player_system_stats";
        public Type StateType => typeof(PlayerSave_New);

        public override void Initialize()
        {
            m_playerHandle = EntityManager.Create<PlayerData_New>(1);
            var player = GetPlayer();
            float interval = player == null ? 0f : player.BaseData.AutoAttackInterval;
            if (IsFinite(interval) && interval > 0f)
            {
                m_autoAttackInterval = interval;
            }
            else
            {
                m_autoAttackInterval = 1f;
                Debug.LogWarning("[PlayerSystem_New] 자동 릴링 공격 간격 설정이 없어 기본 1초를 사용합니다.");
            }
        }

        public override void PostInitialize()
        {
            var player = GetPlayer();
            if (player == null)
            {
                Debug.LogWarning("[PlayerSystem_New] 초기화 실패: 현재 플레이어가 없습니다.");
                return;
            }

            if (m_loadedSave != null)
            {
                RestoreLoadedSave(player, m_loadedSave);
                m_loadedSave = null;
            }
            RecalculateStats();
        }

        /// <summary>전체 스탯을 계산하고 결과를 한 번에 교체한 뒤 알린다. 실패 시 이전 결과를 유지한다.</summary>
        public bool RecalculateStats()
        {
            var statSystem = SystemManager?.GetSystem<StatSystem>();
            if (statSystem == null || !statSystem.TryCalculateStats(m_playerHandle, m_bonusInventorySize, out var result))
            {
                Debug.LogWarning("[PlayerSystem_New] 스탯 계산 실패: 계산 시스템 또는 플레이어가 없습니다.");
                return false;
            }

            m_stats = result;
            OnStatChanged?.Invoke(m_playerHandle);
            return true;
        }

        /// <summary>고유 주스탯을 변경하고 전체 결과를 갱신한다. 계산 실패 시 고유 값도 되돌린다.</summary>
        public bool SetMainStats(float str, float tec, float luk)
        {
            if (!IsFinite(str) || !IsFinite(tec) || !IsFinite(luk))
            {
                Debug.LogWarning("[PlayerSystem_New] 주스탯 변경 실패: NaN 또는 Infinity는 사용할 수 없습니다.");
                return false;
            }

            var player = GetPlayer();
            if (player == null) return false;
            float previousStr = player.Str;
            float previousTec = player.Tec;
            float previousLuk = player.Luk;
            if (previousStr == str && previousTec == tec && previousLuk == luk) return true;

            player.SetMainStats(str, tec, luk);
            if (RecalculateStats()) return true;
            player.SetMainStats(previousStr, previousTec, previousLuk);
            return false;
        }

        /// <summary>자동 릴링 공격 간격(초). 주스탯·장비 보정과 무관한 설정이며 저장하지 않는다.</summary>
        public bool SetAutoAttackInterval(float seconds)
        {
            if (!IsFinite(seconds) || seconds <= 0f)
            {
                Debug.LogWarning("[PlayerSystem_New] 자동 릴링 공격 간격은 유효한 양수여야 합니다.");
                return false;
            }
            if (m_autoAttackInterval == seconds) return true;
            m_autoAttackInterval = seconds;
            OnStatChanged?.Invoke(m_playerHandle);
            return true;
        }

        public object CaptureState()
        {
            var player = GetPlayer();
            if (player == null) return null;
            var save = new PlayerSave_New
            {
                dataId = player.DataId,
                str = player.Str,
                tec = player.Tec,
                luk = player.Luk,
                currentLicense = player.CurrentLicense,
                gold = player.Gold,
                bonusInventorySize = m_bonusInventorySize,
                currentStorageUpgradeCost = m_currentStorageUpgradeCost
            };

            foreach (var equipped in player.Equipped)
            {
                Entity entity = EntityManager.Get(equipped.Value);
                if (entity is Entity_Equipment equipment)
                {
                    save.equippedItems.Add(new PlayerSave_New.EquippedItemSave
                    {
                        area = equipped.Key,
                        handle = equipped.Value.ToString(),
                        dataId = equipment.DataId,
                        upgradeLevel = equipment.UpgradeLevel,
                        quantity = 1
                    });
                }
                else if (entity is Entity_Consumables consumable)
                {
                    save.equippedItems.Add(new PlayerSave_New.EquippedItemSave
                    {
                        area = equipped.Key,
                        handle = equipped.Value.ToString(),
                        dataId = consumable.DataId,
                        quantity = consumable.Quantity
                    });
                }
            }
            return save;
        }

        public void RestoreState(object state)
        {
            if (state is PlayerSave_New save) m_loadedSave = save;
        }

        private void RestoreLoadedSave(Entity_Player_New player, PlayerSave_New save)
        {
            if (save.dataId.HasValue && save.dataId.Value != player.DataId)
            {
                Debug.LogWarning("[PlayerSystem_New] 복원 실패: 저장된 플레이어 정의 ID가 일치하지 않습니다.");
                return;
            }

            // 없는 값은 정의 기본값을 유지하며, 저장된 0은 실제 값으로 복원한다.
            float str = save.str ?? player.Str;
            float tec = save.tec ?? player.Tec;
            float luk = save.luk ?? player.Luk;
            if (IsFinite(str) && IsFinite(tec) && IsFinite(luk))
            {
                player.SetMainStats(str, tec, luk);
            }
            else
            {
                Debug.LogWarning("[PlayerSystem_New] 주스탯 복원 실패: 유효하지 않은 수치라 기본값을 유지합니다.");
            }

            player.SetLicense(save.currentLicense);
            player.SetGold(save.gold);
            m_bonusInventorySize = save.bonusInventorySize;
            m_currentStorageUpgradeCost = save.currentStorageUpgradeCost;
            if (save.equippedItems == null) return;

            foreach (var equipped in save.equippedItems)
            {
                if (equipped == null || !EntityHandle.TryParse(equipped.handle, out EntityHandle handle) || handle.Value == Guid.Empty) continue;
                if (equipped.area == EquipmentMountingArea.Bait || equipped.area == EquipmentMountingArea.Groundbait)
                {
                    EntityHandle restored = EntityManager.Restore<ItemData_Consumables>(handle, equipped.dataId);
                    if (EntityManager.Get(restored) is Entity_Consumables consumable)
                    {
                        consumable.SetQuantity(Math.Max(1, equipped.quantity));
                        player.Equip(equipped.area, restored);
                    }
                }
                else
                {
                    EntityHandle restored = EntityManager.Restore<ItemData_Equipment>(handle, equipped.dataId);
                    if (EntityManager.Get(restored) is Entity_Equipment equipment)
                    {
                        equipment.SetUpgradeLevel(Math.Max(0, equipped.upgradeLevel));
                        player.Equip(equipped.area, restored);
                    }
                }
            }
        }

        public void Equip(EquipmentMountingArea area, EntityHandle afterEquipHandle)
        {
            var player = GetPlayer();
            var inventory = SystemManager?.GetSystem<InventorySystem>();
            if (player == null || inventory == null) return;

            player.TryGetEquipped(area, out EntityHandle beforeEquipHandle);
            if (beforeEquipHandle.Equals(afterEquipHandle)) return;
            if (afterEquipHandle.Value != Guid.Empty &&
                (EntityManager.Get(afterEquipHandle) == null || !TryRemoveFromInventory(inventory, afterEquipHandle)))
            {
                Debug.LogWarning("[PlayerSystem_New] 장착 실패: 인벤토리에서 아이템을 제거할 수 없습니다.");
                return;
            }

            if (beforeEquipHandle.Value != Guid.Empty)
            {
                player.Unequip(area);
                inventory.AddItem(beforeEquipHandle);
            }
            if (afterEquipHandle.Value != Guid.Empty) player.Equip(area, afterEquipHandle);
            RecalculateStats();
        }

        public string GetEquippedItemName(EquipmentMountingArea area)
        {
            return EntityManager?.Get(GetEquippedItemHandle(area))?.Name;
        }

        public EntityHandle GetEquippedItemHandle(EquipmentMountingArea area)
        {
            var player = GetPlayer();
            return player != null && player.TryGetEquipped(area, out EntityHandle handle) ? handle : default;
        }

        /// <summary>장착 스택을 소모한다. 수량만 바뀌면 장비 알림, 전량 소모로 해제되면 스탯 재계산을 수행한다.</summary>
        public bool TryConsumeEquippedItem(EquipmentMountingArea area, out ItemData consumedItemData, int amount = 1)
        {
            consumedItemData = null;
            var player = GetPlayer();
            if (amount <= 0 || player == null || !player.TryGetEquipped(area, out EntityHandle handle)) return false;

            Entity entity = EntityManager.Get(handle);
            int remaining;
            if (entity is Entity_Consumables consumable && consumable.Quantity >= amount)
            {
                consumedItemData = consumable.ItemData;
                consumable.Add(-amount);
                remaining = consumable.Quantity;
            }
            else if (entity is Entity_Materials material && material.Quantity >= amount)
            {
                consumedItemData = material.ItemData;
                material.Add(-amount);
                remaining = material.Quantity;
            }
            else return false;

            if (remaining <= 0)
            {
                player.Unequip(area);
                EntityManager.Destroy(handle);
                RecalculateStats();
            }
            else OnEquipmentChanged?.Invoke(m_playerHandle);
            return true;
        }

        public void UpgradeFishStorage()
        {
            var player = GetPlayer();
            if (player == null) return;
            if (player.BaseData.BaseInventorySize + m_bonusInventorySize >= MaxInventorySize)
            {
                Debug.LogWarning($"[PlayerSystem_New] 창고 최대 크기({MaxInventorySize}칸)에 도달했습니다.");
                return;
            }
            if (player.Gold < m_currentStorageUpgradeCost)
            {
                Debug.LogWarning("[PlayerSystem_New] 창고 확장에 필요한 골드가 부족합니다.");
                return;
            }

            player.AddGold(-m_currentStorageUpgradeCost);
            m_bonusInventorySize++;
            m_currentStorageUpgradeCost = Mathf.CeilToInt(m_currentStorageUpgradeCost * StorageUpgradeCostMultiplier);
            RecalculateStats();
        }

        private Entity_Player_New GetPlayer() => EntityManager?.Get<Entity_Player_New>(m_playerHandle);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool TryRemoveFromInventory(InventorySystem inventory, EntityHandle handle)
        {
            ItemType[] types = { ItemType.Fish, ItemType.Equipment, ItemType.Materials, ItemType.Consumables };
            foreach (ItemType type in types)
            {
                EntityHandle[] slots = inventory.GetSlots(type);
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].Equals(handle)) return inventory.RemoveAt(type, i, false);
                }
            }
            return false;
        }

        public bool TryReinforceEquipment(EntityHandle equipHandle)
        {
            var inventorySystem = SystemManager?.GetSystem<InventorySystem>();
            var player = GetPlayer();
            Entity_Equipment equipment = EntityManager?.Get<Entity_Equipment>(equipHandle);

            if (player == null || equipment == null || inventorySystem == null) return false;

            UpgradeStep nextStep = equipment.ItemData.GetNextUpgradeStep(equipment.UpgradeLevel);
            if (nextStep == null)
            {
                Debug.LogWarning("최대 강화 레벨입니다.");
                return false;
            }

            if (player.Gold < nextStep.GoldCost)
            {
                Debug.LogWarning("골드가 부족합니다.");
                return false;
            }

            if (nextStep.MaterialCosts != null)
            {
                foreach (var mat in nextStep.MaterialCosts)
                {
                    if (mat.Material != null)
                    {
                        if (inventorySystem.GetTotalQuantityByDataId(ItemType.Materials, mat.Material.ID) < mat.Count)
                        {
                            Debug.LogWarning("재료가 부족합니다.");
                            return false;
                        }
                    }
                }
            }

            player.AddGold(-nextStep.GoldCost);

            if (nextStep.MaterialCosts != null)
            {
                foreach (var mat in nextStep.MaterialCosts)
                {
                    if (mat.Material != null)
                    {
                        inventorySystem.ConsumeItemByDataId(ItemType.Materials, mat.Material.ID, mat.Count);
                    }
                }
            }

            equipment.SetUpgradeLevel(equipment.UpgradeLevel + 1);

            RecalculateStats();

            Debug.Log($"{equipment.ItemData.Name} 장비가 +{equipment.UpgradeLevel}강으로 강화되었습니다.");
            return true;
        }
    }
}
