using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;
using System;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public class InventorySystem : SystemBase, ISaveable
    {
        private const int FallbackInventorySize = 20;
        private const bool EnableInventoryDebugLog = true;

        private InventorySlotStorage m_slotStorage;

        private PlayerSystem m_playerSystem;
        private ShopSystem m_shopSystem;

        private bool m_autoSellEnabled;
        private ItemQuality m_maxAutoSellQuality = ItemQuality.OneStar;
        private ItemRarity m_maxAutoSellRarity = ItemRarity.Normal;

        public event Action OnInventoryChanged;

        public event Action OnInventorySaveRequested;
        public event Action OnAutoSellFilterChanged;

        public bool AutoSellEnabled => m_autoSellEnabled;
        public ItemQuality MaxAutoSellQuality => m_maxAutoSellQuality;
        public ItemRarity MaxAutoSellRarity => m_maxAutoSellRarity;

        private InventorySave m_loadedSave;

        public string SaveId => "inventory";
        public Type StateType => typeof(InventorySave);

        public override void Initialize()
        {
        }

        public override void PostInitialize()
        {
            m_playerSystem = SystemManager.GetSystem<PlayerSystem>();
            m_shopSystem = SystemManager.GetSystem<ShopSystem>();

            int fishInventorySize = GetCurrentInventorySize();
            int equipmentInventorySize = GetInitialExpandableInventorySize(ItemType.Equipment);
            int materialInventorySize = GetInitialExpandableInventorySize(ItemType.Materials);

            m_slotStorage = new InventorySlotStorage(fishInventorySize, equipmentInventorySize, materialInventorySize);

            if (m_loadedSave != null)
            {
                RestoreLoadedSave();
                m_loadedSave = null;
            }

            if (m_playerSystem != null)
            {
                m_playerSystem.OnStatChanged += HandlePlayerStatChanged;
            }

            LogDebug($"PostInitialize complete. fishSlots: {fishInventorySize}, equipmentSlots: {equipmentInventorySize}, materialSlots: {materialInventorySize}");
        }

        /// <summary>
        /// itemType 슬롯 Read용 함수
        /// </summary>
        /// <param name="itemType"></param>
        public EntityHandle[] GetSlots(ItemType itemType)
        {
            return m_slotStorage.GetSlotsCopy(itemType);
        }

        public int GetMaxSlotCount(ItemType itemType)
        {
            return m_slotStorage.GetMaxSlotCount(itemType);
        }

        public int GetUsedSlotCount(ItemType itemType)
        {
            return m_slotStorage.GetUsedSlotCount(itemType);
        }

        public bool GetHandleAt(ItemType itemType, int slotIndex, out EntityHandle handle)
        {
            return m_slotStorage.GetHandle(itemType, slotIndex, out handle);
        }

        public void SetAutoSellFilter(bool enabled, ItemQuality maxQuality, ItemRarity maxRarity)
        {
            if (m_autoSellEnabled == enabled
                && m_maxAutoSellQuality == maxQuality
                && m_maxAutoSellRarity == maxRarity)
            {
                return;
            }

            m_autoSellEnabled = enabled;
            m_maxAutoSellQuality = maxQuality;
            m_maxAutoSellRarity = maxRarity;

            OnAutoSellFilterChanged?.Invoke();
        }

        public bool AddItem(EntityHandle itemHandle)
        {
            LogDebug($"TryAddItem called. handle: {itemHandle}");

            if (IsEmptyHandle(itemHandle))
            {
                LogWarning("TryAddItem failed. Handle is empty.");
                return false;
            }

            if (m_slotStorage.ContainsHandle(itemHandle))
            {
                LogWarning($"TryAddItem failed. Already contains handle: {itemHandle}");
                return false;
            }

            Entity itemEntity = EntityManager.Get(itemHandle);

            if (itemEntity == null)
            {
                LogWarning($"TryAddItem failed. Entity not found. handle: {itemHandle}");
                return false;
            }

            if (!InventoryItemRules.GetItemType(itemEntity, out ItemType itemType))
            {
                LogWarning($"TryAddItem failed. Unsupported entity type: {itemEntity.GetType().Name}");
                return false;
            }

            // 스택 아이템은 수량이 1개 이상일 때만 인벤토리에 수납
            if (InventoryItemRules.GetStackQuantity(itemEntity, out int incomingQuantity) && incomingQuantity <= 0)
            {
                LogWarning($"TryAddItem failed. Stack quantity must be greater than zero. type: {itemEntity.GetType().Name}, dataId: {itemEntity.DataId}, quantity: {incomingQuantity}");
                return false;
            }

            if (itemEntity is Entity_Fish fish && ShouldSellFish(fish))
            {
                if (m_shopSystem == null)
                {
                    LogWarning("TryAddItem failed. ShopSystem not found.");
                    return false;
                }

                bool sold = m_shopSystem.SellAcquiredItem(itemHandle, out int earnedGold);

                if (sold)
                {
                    LogDebug($"Fish sold before inventory add. dataId: {fish.DataId}, name: {fish.Name}, earnedGold: {earnedGold}");
                    RequestSave();
                }
                else
                {
                    LogWarning($"TryAddItem failed. Fish sale failed. dataId: {fish.DataId}, name: {fish.Name}");
                }

                return sold;
            }

            EntityHandle[] slots = m_slotStorage.GetMutableSlots(itemType);
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if (MergeStackableItem(itemHandle, itemEntity, slots))
            {
                LogDebug($"TryAddItem merged. slotType: {slotType}, itemType: {itemType}, dataId: {itemEntity.DataId}, name: {itemEntity.Name}");
                NotifyInventoryChanged($"Merge item / slotType: {slotType}, itemType: {itemType}, dataId: {itemEntity.DataId}", true);
                return true;
            }

            ExpandInventoryIfNeeded(slotType);

            int emptyIndex = m_slotStorage.FindEmptySlotIndex(slotType);

            if (emptyIndex < 0)
            {
                LogWarning($"TryAddItem failed. No empty slot. slotType: {slotType}, itemType: {itemType}, dataId: {itemEntity.DataId}, name: {itemEntity.Name}");
                return false;
            }

            m_slotStorage.SetHandle(slotType, emptyIndex, itemHandle);
            ExpandInventoryIfNeeded(slotType);

            LogDebug($"TryAddItem success. slotType: {slotType}, itemType: {itemType}, slotIndex: {emptyIndex}, dataId: {itemEntity.DataId}, name: {itemEntity.Name}, handle: {itemHandle}");
            NotifyInventoryChanged($"Add item / slotType: {slotType}, itemType: {itemType}, slotIndex: {emptyIndex}, dataId: {itemEntity.DataId}", true);

            return true;
        }

        public bool RemoveAt(ItemType itemType, int slotIndex, bool destroyEntity = false, bool requestSave = true)
        {
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if (!m_slotStorage.IsValidSlotIndex(itemType, slotIndex))
            {
                LogWarning($"TryRemoveAt failed. Invalid slot. slotType: {slotType}, itemType: {itemType}, slotIndex: {slotIndex}");
                return false;
            }

            if (!m_slotStorage.GetHandle(itemType, slotIndex, out EntityHandle removedHandle))
            {
                LogWarning($"TryRemoveAt failed. Slot is empty. slotType: {slotType}, itemType: {itemType}, slotIndex: {slotIndex}");
                return false;
            }

            m_slotStorage.ClearHandle(itemType, slotIndex, out _);

            if (destroyEntity)
            {
                EntityManager.Destroy(removedHandle);
            }

            LogDebug($"TryRemoveAt success. itemType: {itemType}, slotIndex: {slotIndex}, destroyEntity: {destroyEntity}");
            NotifyInventoryChanged($"Remove item / itemType: {itemType}, slotIndex: {slotIndex}", requestSave);
            return true;
        }

        public bool SwapSlots(ItemType itemType, int fromIndex, int toIndex)
        {
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if (!m_slotStorage.IsValidSlotIndex(itemType, fromIndex) || !m_slotStorage.IsValidSlotIndex(itemType, toIndex))
            {
                LogWarning($"SwapSlots failed. Invalid index. slotType: {slotType}, itemType: {itemType}, from: {fromIndex}, to: {toIndex}");
                return false;
            }

            if (fromIndex == toIndex)
            {
                LogDebug($"SwapSlots skipped. Same index. itemType: {itemType}, index: {fromIndex}");
                return false;
            }

            m_slotStorage.Swap(itemType, fromIndex, toIndex);

            LogDebug($"SwapSlots success. slotType: {slotType}, itemType: {itemType}, from: {fromIndex}, to: {toIndex}");
            NotifyInventoryChanged($"Swap slots / slotType: {slotType}, from: {fromIndex}, to: {toIndex}", true);

            return true;
        }

        public bool RemoveQuantityAt(ItemType itemType, int slotIndex, int amount, bool destroyEntityWhenZero = true, bool requestSave = true)
        {
            if (amount <= 0)
            {
                LogWarning($"TryRemoveQuantityAt failed. Invalid amount: {amount}");
                return false;
            }

            if (!GetHandleAt(itemType, slotIndex, out EntityHandle handle))
            {
                LogWarning($"TryRemoveQuantityAt failed. Handle not found. itemType: {itemType}, slotIndex: {slotIndex}");
                return false;
            }

            Entity entity = EntityManager.Get(handle);

            if (entity == null)
            {
                LogWarning($"TryRemoveQuantityAt failed. Entity not found. handle: {handle}");
                return false;
            }

            if (!InventoryItemRules.GetStackQuantity(entity, out int currentQuantity))
            {
                LogWarning($"TryRemoveQuantityAt failed. Entity is not stackable. type: {entity.GetType().Name}, dataId: {entity.DataId}, name: {entity.Name}");
                return false;
            }

            if (currentQuantity < amount)
            {
                LogWarning($"TryRemoveQuantityAt failed. Not enough quantity. dataId: {entity.DataId}, name: {entity.Name}, current: {currentQuantity}, requested: {amount}");
                return false;
            }

            int nextQuantity = currentQuantity - amount;

            if (nextQuantity > 0)
            {
                InventoryItemRules.SetStackQuantity(entity, nextQuantity);

                LogDebug($"TryRemoveQuantityAt success. dataId: {entity.DataId}, name: {entity.Name}, before: {currentQuantity}, remove: {amount}, after: {nextQuantity}");
                NotifyInventoryChanged($"Decrease quantity / dataId: {entity.DataId}, amount: {amount}", requestSave);

                return true;
            }

            LogDebug($"TryRemoveQuantityAt zero. dataId: {entity.DataId}, name: {entity.Name}, before: {currentQuantity}, remove: {amount}. Slot will be removed.");
            return RemoveAt(itemType, slotIndex, destroyEntityWhenZero, requestSave);
        }

        public bool RemoveByHandle(EntityHandle handle, int amount, bool requestSave = true)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (!m_slotStorage.FindSlot(handle, out ItemType slotType, out int slotIndex))
            {
                return false;
            }

            Entity entity = EntityManager.Get(handle);

            if (!InventoryItemRules.CanRemove(entity, amount))
            {
                return false;
            }

            if (InventoryItemRules.GetStackQuantity(entity, out _))
            {
                return RemoveQuantityAt(slotType, slotIndex, amount, true, requestSave);
            }

            return RemoveAt(slotType, slotIndex, true, requestSave);
        }

        //item이 인벤토리에 몇 개 있는지 반환
        public int GetTotalQuantityByDataId(ItemType itemType, int dataId)
        {
            if (dataId <= 0)
            {
                return 0;
            }

            EntityHandle[] slots = m_slotStorage.GetMutableSlots(itemType);
            int totalQuantity = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (IsEmptyHandle(slots[i]))
                {
                    continue;
                }

                Entity entity = EntityManager.Get(slots[i]);

                if (entity == null || entity.DataId != dataId)
                {
                    continue;
                }

                if (InventoryItemRules.GetStackQuantity(entity, out int quantity))
                {
                    totalQuantity += quantity;
                }
            }

            return totalQuantity;
        }

        public bool ConsumeItemByDataId(ItemType itemType, int dataId, int amount)
        {
            LogDebug($"TryConsumeItemByDataId called. itemType: {itemType}, dataId: {dataId}, amount: {amount}");

            if (dataId <= 0 || amount <= 0)
            {
                LogWarning($"TryConsumeItemByDataId failed. Invalid args. itemType: {itemType}, dataId: {dataId}, amount: {amount}");
                return false;
            }

            int totalQuantity = GetTotalQuantityByDataId(itemType, dataId);

            if (totalQuantity < amount)
            {
                LogWarning($"TryConsumeItemByDataId failed. Not enough quantity. itemType: {itemType}, dataId: {dataId}, current: {totalQuantity}, requested: {amount}");
                return false;
            }

            EntityHandle[] slots = m_slotStorage.GetMutableSlots(itemType);
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);
            int remainingAmount = amount;

            for (int i = 0; i < slots.Length; i++)
            {
                if (remainingAmount <= 0)
                {
                    break;
                }

                if (IsEmptyHandle(slots[i]))
                {
                    continue;
                }

                Entity entity = EntityManager.Get(slots[i]);

                if (entity == null || entity.DataId != dataId)
                {
                    continue;
                }

                if (!InventoryItemRules.GetStackQuantity(entity, out int quantity))
                {
                    continue;
                }

                int removeAmount = Math.Min(quantity, remainingAmount);
                int nextQuantity = quantity - removeAmount;

                if (nextQuantity > 0)
                {
                    InventoryItemRules.SetStackQuantity(entity, nextQuantity);
                    LogDebug($"Consume partial stack. slotType: {slotType}, slotIndex: {i}, dataId: {dataId}, before: {quantity}, remove: {removeAmount}, after: {nextQuantity}");
                }
                else
                {
                    m_slotStorage.ClearHandle(itemType, i, out EntityHandle removedHandle);
                    EntityManager.Destroy(removedHandle);
                    LogDebug($"Consume whole stack. slotType: {slotType}, slotIndex: {i}, dataId: {dataId}, before: {quantity}, remove: {removeAmount}, entity destroyed.");
                }

                remainingAmount -= removeAmount;
            }

            bool result = remainingAmount <= 0;

            if (result)
            {
                LogDebug($"TryConsumeItemByDataId success. slotType: {slotType}, itemType: {itemType}, dataId: {dataId}, amount: {amount}");
                NotifyInventoryChanged($"Consume item / slotType: {slotType}, itemType: {itemType}, dataId: {dataId}, amount: {amount}", true);
            }
            else
            {
                LogWarning($"TryConsumeItemByDataId failed after loop. slotType: {slotType}, itemType: {itemType}, dataId: {dataId}, amount: {amount}, remaining: {remainingAmount}");
            }

            return result;
        }

        public object CaptureState()
        {
            InventorySave save = new InventorySave();

            CaptureSlots(save, ItemType.Fish, m_slotStorage.GetMutableSlots(ItemType.Fish));
            CaptureSlots(save, ItemType.Equipment, m_slotStorage.GetMutableSlots(ItemType.Equipment));
            CaptureSlots(save, ItemType.Materials, m_slotStorage.GetMutableSlots(ItemType.Materials));

            LogDebug($"CaptureState finished. saveSlotCount: {save.slots.Count}");

            return save;
        }

        public void RestoreState(object state)
        {
            LogDebug("RestoreState called.");

            if (state is not InventorySave save)
            {
                LogWarning($"RestoreState failed. Invalid state type: {state?.GetType().Name ?? "null"}");
                return;
            }

            m_loadedSave = save;

            LogDebug($"RestoreState pending. savedSlotCount: {save.slots.Count}");
        }

        private void NotifyInventoryChanged(string reason, bool requestSave)
        {
            LogDebug($"Inventory changed. Reason: {reason}");

            OnInventoryChanged?.Invoke();

            if (!requestSave)
            {
                return;
            }

            RequestSave();
        }

        public void RequestSave()
        {
            LogDebug("Save requested by inventory change.");
            OnInventorySaveRequested?.Invoke();
        }

        private void LogDebug(string message)
        {
            if (EnableInventoryDebugLog)
            {
                Debug.Log($"[InventorySystem] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (EnableInventoryDebugLog)
            {
                Debug.LogWarning($"[InventorySystem] {message}");
            }
        }

        private void CaptureSlots(InventorySave save, ItemType slotType, EntityHandle[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (IsEmptyHandle(slots[i]))
                {
                    continue;
                }

                Entity entity = EntityManager.Get(slots[i]);

                if (entity == null)
                {
                    LogWarning($"CaptureSlots skipped. Entity missing. slotType: {slotType}, slotIndex: {i}, handle: {slots[i]}");
                    continue;
                }

                if (!InventoryItemRules.GetItemType(entity, out ItemType itemType))
                {
                    LogWarning($"CaptureSlots skipped. Unsupported entity type: {entity.GetType().Name}");
                    continue;
                }

                //런타임 상태가 잘못되었을 때 수량 보정 방지
                if (InventoryItemRules.GetStackQuantity(entity, out int quantity) && quantity <= 0)
                {
                    LogWarning($"CaptureSlots skipped. Stack quantity must be greater than zero. slotType: {slotType}, slotIndex: {i}, dataId: {entity.DataId}, quantity: {quantity}");
                    continue;
                }

                InventorySave.SlotSave slotSave = CreateSlotSave(itemType, i, slots[i], entity);
                save.slots.Add(slotSave);

                LogDebug($"CaptureSlot. slotType: {slotType}, itemType: {itemType}, slotIndex: {i}, dataId: {entity.DataId}, name: {entity.Name}");
            }
        }

        private InventorySave.SlotSave CreateSlotSave(ItemType itemType, int slotIndex, EntityHandle handle, Entity entity)
        {
            InventorySave.SlotSave slotSave = new InventorySave.SlotSave
            {
                itemType = itemType,
                slotIndex = slotIndex,
                handle = handle.ToString(),
                dataId = entity.DataId,

                size = 0f,
                quality = ItemQuality.OneStar,
                upgradeLevel = 0,
                quantity = 0
            };

            if (entity is Entity_Fish fish)
            {
                slotSave.size = Math.Max(0f, fish.Size);
                slotSave.quality = NormalizeQuality(fish.Quality);
            }
            else if (entity is Entity_Equipment equipment)
            {
                slotSave.upgradeLevel = Math.Max(0, equipment.UpgradeLevel);
            }
            else if (entity is Entity_Materials materials)
            {
                slotSave.quantity = materials.Quantity;
            }
            else if (entity is Entity_Consumables consumables)
            {
                slotSave.quantity = consumables.Quantity;
            }

            return slotSave;
        }

        private bool RestoreSlot(InventorySave.SlotSave slotSave)
        {
            LogDebug($"RestoreSlot called. itemType: {slotSave.itemType}, slotIndex: {slotSave.slotIndex}, dataId: {slotSave.dataId}, handle: {slotSave.handle}");

            if (!RestoreEntity(slotSave, out EntityHandle restoredHandle))
            {
                LogWarning($"RestoreSlot failed. Entity restore failed. itemType: {slotSave.itemType}, dataId: {slotSave.dataId}");
                return false;
            }

            ItemType slotType = InventorySlotStorage.NormalizeSlotType(slotSave.itemType);
            int targetIndex = slotSave.slotIndex;

            if (!m_slotStorage.IsValidSlotIndex(slotSave.itemType, targetIndex)
                || m_slotStorage.GetHandle(slotSave.itemType, targetIndex, out _))
            {
                int originalIndex = targetIndex;
                targetIndex = m_slotStorage.FindEmptySlotIndex(slotType);

                LogWarning($"RestoreSlot target slot unavailable. slotType: {slotType}, itemType: {slotSave.itemType}, originalIndex: {originalIndex}, fallbackIndex: {targetIndex}");
            }

            if (targetIndex < 0)
            {
                EntityManager.Destroy(restoredHandle);

                LogWarning($"RestoreSlot failed. No empty slot. slotType: {slotType}, itemType: {slotSave.itemType}, dataId: {slotSave.dataId}");
                return false;
            }

            m_slotStorage.SetHandle(slotType, targetIndex, restoredHandle);

            LogDebug($"RestoreSlot success. slotType: {slotType}, itemType: {slotSave.itemType}, slotIndex: {targetIndex}, dataId: {slotSave.dataId}, handle: {restoredHandle}");
            return true;
        }

        private void RestoreLoadedSave()
        {
            LogDebug($"RestoreState start. savedSlotCount: {m_loadedSave.slots.Count}");

            int restoredCount = 0;
            int failedCount = 0;

            for (int i = 0; i < m_loadedSave.slots.Count; i++)
            {
                bool result = RestoreSlot(m_loadedSave.slots[i]);

                if (result)
                {
                    restoredCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            LogDebug($"RestoreState finished. restored: {restoredCount}, failed: {failedCount}");
            NotifyInventoryChanged($"Restore inventory / restored: {restoredCount}, failed: {failedCount}", false);
        }

        private bool RestoreEntity(InventorySave.SlotSave slotSave, out EntityHandle restoredHandle)
        {
            restoredHandle = default;

            if (!EntityHandle.TryParse(slotSave.handle, out EntityHandle parsedHandle))
            {
                LogWarning($"TryRestoreEntity failed. Handle parse failed. handle: {slotSave.handle}");
                return false;
            }

            switch (slotSave.itemType)
            {
                case ItemType.Fish:
                    return RestoreFish(slotSave, parsedHandle, out restoredHandle);

                case ItemType.Equipment:
                    return RestoreEquipment(slotSave, parsedHandle, out restoredHandle);

                case ItemType.Materials:
                    return RestoreMaterials(slotSave, parsedHandle, out restoredHandle);

                case ItemType.Consumables:
                    return RestoreConsumables(slotSave, parsedHandle, out restoredHandle);

                default:
                    LogWarning($"TryRestoreEntity failed. Unsupported itemType: {slotSave.itemType}");
                    return false;
            }
        }

        private bool RestoreFish(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
        {
            restoredHandle = default;

            if (DataManager.GetData<ItemData_Fish>(slotSave.dataId) == null)
            {
                LogWarning($"TryRestoreFish failed. Data not found. dataId: {slotSave.dataId}");
                return false;
            }

            restoredHandle = EntityManager.Restore<ItemData_Fish>(parsedHandle, slotSave.dataId);

            if (EntityManager.Get(restoredHandle) is Entity_Fish fish)
            {
                fish.SetRollResult(Math.Max(0f, slotSave.size), NormalizeQuality(slotSave.quality));
                return true;
            }

            return false;
        }

        private bool RestoreEquipment(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
        {
            restoredHandle = default;

            if (DataManager.GetData<ItemData_Equipment>(slotSave.dataId) == null)
            {
                LogWarning($"TryRestoreEquipment failed. Data not found. dataId: {slotSave.dataId}");
                return false;
            }

            restoredHandle = EntityManager.Restore<ItemData_Equipment>(parsedHandle, slotSave.dataId);

            if (EntityManager.Get(restoredHandle) is Entity_Equipment equipment)
            {
                equipment.SetUpgradeLevel(Math.Max(0, slotSave.upgradeLevel));
                return true;
            }

            return false;
        }

        private bool RestoreMaterials(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
        {
            restoredHandle = default;

            if (DataManager.GetData<ItemData_Materials>(slotSave.dataId) == null)
            {
                LogWarning($"TryRestoreMaterials failed. Data not found. dataId: {slotSave.dataId}");
                return false;
            }
            if (slotSave.quantity <= 0)
            {
                LogWarning($"TryRestoreMaterials failed. Quantity must be greater than zero. dataId: {slotSave.dataId}, quantity: {slotSave.quantity}");
                return false;
            }

            restoredHandle = EntityManager.Restore<ItemData_Materials>(parsedHandle, slotSave.dataId);


            if (EntityManager.Get(restoredHandle) is Entity_Materials materials)
            {
                materials.SetQuantity(slotSave.quantity);
                return true;
            }

            return false;
        }

        private bool RestoreConsumables(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
        {
            restoredHandle = default;

            if (DataManager.GetData<ItemData_Consumables>(slotSave.dataId) == null)
            {
                LogWarning($"TryRestoreConsumables failed. Data not found. dataId: {slotSave.dataId}");
                return false;
            }
            if (slotSave.quantity <= 0)
            {
                LogWarning($"TryRestoreConsumables failed. Quantity must be greater than zero. dataId: {slotSave.dataId}, quantity: {slotSave.quantity}");
                return false;
            }

            restoredHandle = EntityManager.Restore<ItemData_Consumables>(parsedHandle, slotSave.dataId);

            if (EntityManager.Get(restoredHandle) is Entity_Consumables consumables)
            {
                consumables.SetQuantity(slotSave.quantity);
                return true;
            }

            return false;
        }

        private bool MergeStackableItem(EntityHandle incomingHandle, Entity incomingEntity, EntityHandle[] targetSlots)
        {
            if (!InventoryItemRules.GetItemType(incomingEntity, out ItemType itemType)
                || !InventoryItemRules.GetStackQuantity(incomingEntity, out int incomingQuantity)
                || incomingQuantity <= 0)
            {
                return false;
            }

            for (int i = 0; i < targetSlots.Length; i++)
            {
                Entity existingEntity = GetAliveEntityOrClear(itemType, targetSlots, i);

                if (existingEntity == null
                    || !InventoryItemRules.MergeStack(existingEntity, incomingEntity, out int beforeQuantity, out int addedQuantity, out int afterQuantity))
                {
                    continue;
                }

                EntityManager.Destroy(incomingHandle);

                string stackTypeName = itemType == ItemType.Materials ? "materials" : "consumables";
                LogDebug($"Merge {stackTypeName}. dataId: {existingEntity.DataId}, before: {beforeQuantity}, add: {addedQuantity}, after: {afterQuantity}");
                return true;
            }

            return false;
        }

        private Entity GetAliveEntityOrClear(ItemType itemType, EntityHandle[] slots, int slotIndex)
        {
            if (IsEmptyHandle(slots[slotIndex]))
            {
                return null;
            }

            Entity entity = EntityManager.Get(slots[slotIndex]);

            if (entity == null)
            {
                m_slotStorage.ClearHandle(itemType, slotIndex, out _);
                return null;
            }

            return entity;
        }

        private bool ShouldSellFish(Entity_Fish fish)
        {
            return IsAutoSellTarget(fish) || IsFishInventoryFull();
        }

        private bool IsAutoSellTarget(Entity_Fish fish)
        {
            if (!m_autoSellEnabled)
            {
                return false;
            }

            return fish.Quality <= m_maxAutoSellQuality
                && fish.Rarity <= m_maxAutoSellRarity;
        }

        private bool IsFishInventoryFull()
        {
            return m_slotStorage.FindEmptySlotIndex(ItemType.Fish) < 0;
        }

        public bool CanRemoveByHandle(EntityHandle handle, int amount)
        {
            if (amount <= 0 || !m_slotStorage.FindSlot(handle, out _, out _))
            {
                return false;
            }

            Entity entity = EntityManager.Get(handle);
            return InventoryItemRules.CanRemove(entity, amount);
        }

        private int GetInitialExpandableInventorySize(ItemType itemType)
        {
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if ((slotType != ItemType.Equipment && slotType != ItemType.Materials)
                || m_loadedSave == null
                || m_loadedSave.slots == null)
            {
                return InventorySlotStorage.ExpandableInventoryInitialSize;
            }

            int highestSlotIndex = -1;
            int savedSlotCount = 0;

            for (int i = 0; i < m_loadedSave.slots.Count; i++)
            {
                InventorySave.SlotSave slotSave = m_loadedSave.slots[i];

                if (InventorySlotStorage.NormalizeSlotType(slotSave.itemType) != slotType)
                {
                    continue;
                }

                savedSlotCount++;
                highestSlotIndex = Math.Max(highestSlotIndex, slotSave.slotIndex);
            }

            return InventorySlotStorage.CalculateInitialExpandableSize(highestSlotIndex, savedSlotCount);
        }

        private void ExpandInventoryIfNeeded(ItemType itemType)
        {
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if (!m_slotStorage.ExpandIfNeeded(slotType, out int previousSlotSize, out int nextSlotSize, out int remainingSlotCount))
            {
                return;
            }

            LogDebug($"Expandable inventory expanded. slotType: {slotType}, before: {previousSlotSize}, after: {nextSlotSize}, remainingBeforeExpand: {remainingSlotCount}");
        }

        private int GetCurrentInventorySize()
        {
            if (m_playerSystem == null)
            {
                LogWarning($"PlayerSystem not found. fallback slot size: {FallbackInventorySize}");
                return FallbackInventorySize;
            }

            int size = m_playerSystem.BaseInventorySize;

            if (size <= 0)
            {
                LogWarning($"PlayerSystem.BaseInventorySize is invalid. value: {size}, fallback slot size: {FallbackInventorySize}");
                return FallbackInventorySize;
            }

            return size;
        }

        private bool IsEmptyHandle(EntityHandle handle)
        {
            return handle.Value == Guid.Empty;
        }

        private ItemQuality NormalizeQuality(ItemQuality quality)
        {
            if (Enum.IsDefined(typeof(ItemQuality), quality))
            {
                return quality;
            }

            return ItemQuality.OneStar;
        }

        private bool ResizeFishSlots(int slotSize)
        {
            if (slotSize <= 0)
            {
                LogWarning($"ResizeFishSlots failed. Invalid slot size: {slotSize}");
                return false;
            }

            if (!m_slotStorage.ResizeFishSlots(slotSize))
            {
                LogWarning($"ResizeFishSlots failed. Fish exist outside next slot size. nextSize: {slotSize}");
                return false;
            }

            NotifyInventoryChanged($"Resize fish inventory / slotSize: {slotSize}", false);

            return true;
        }

        private void HandlePlayerStatChanged(EntityHandle playerHandle)
        {
            int nextInventorySize = GetCurrentInventorySize();

            bool result = ResizeFishSlots(nextInventorySize);

            LogDebug($"Fish inventory size stat changed. nextSize: {nextInventorySize}, resizeResult: {result}");
        }
    }
}