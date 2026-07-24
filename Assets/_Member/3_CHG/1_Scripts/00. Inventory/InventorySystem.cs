using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public class InventorySystem : SystemBase, ISaveable
    {
        private const int FallbackInventorySize = 20;
        private const bool EnableInventoryDebugLog = true;

        private InventorySlotStorage m_slotStorage;
        private InventorySaveLoad m_saveLoad;
        private InventoryAutoSellFilter m_autoSellFilter;

        private PlayerSystem m_playerSystem;
        private ShopSystem m_shopSystem;

        public event Action OnInventoryChanged;

        public event Action OnInventorySaveRequested;
        public event Action OnAutoSellFilterChanged;

        private InventorySave m_loadedSave;

        public string SaveId => "inventory";
        public Type StateType => typeof(InventorySave);

        public bool AutoSellEnabled => m_autoSellFilter.AutoSellEnabled;
        public ItemQuality MaxAutoSellQuality => m_autoSellFilter.MaxAutoSellQuality;
        public ItemRarity MaxAutoSellRarity => m_autoSellFilter.MaxAutoSellRarity;

        public override void Initialize()
        {
        }

        public override void PostInitialize()
        {
            m_playerSystem = SystemManager.GetSystem<PlayerSystem>();
            m_shopSystem = SystemManager.GetSystem<ShopSystem>();

            int fishInventorySize = GetCurrentInventorySize();
            int equipmentInventorySize = InventorySaveLoad.GetInitialExpandableInventorySize(m_loadedSave, ItemType.Equipment);
            int materialInventorySize = InventorySaveLoad.GetInitialExpandableInventorySize(m_loadedSave, ItemType.Materials);

            m_slotStorage = new InventorySlotStorage(fishInventorySize, equipmentInventorySize, materialInventorySize);
            m_saveLoad = new InventorySaveLoad(m_slotStorage, EntityManager, DataManager, LogDebug, LogWarning);
            m_autoSellFilter = new InventoryAutoSellFilter();

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
        public EntityHandle[] GetSlots(ItemType itemType)
        {
            return m_slotStorage.GetSlotsCopy(itemType);
        }

        /// <summary> 슬롯 갯수를 세는 함수 </summary>
        public int GetMaxSlotCount(ItemType itemType)
        {
            return m_slotStorage.GetMaxSlotCount(itemType);
        }

        /// <summary> 비어있지 않은 슬롯 갯수를 세는 함수 </summary>
        public int GetUsedSlotCount(ItemType itemType)
        {
            return m_slotStorage.GetUsedSlotCount(itemType);
        }

        /// <summary> 특정 슬롯을 참조해서 핸들을 반환하는 함수 </summary>
        public bool GetHandleAt(ItemType itemType, int slotIndex, out EntityHandle handle)
        {
            return m_slotStorage.GetHandle(itemType, slotIndex, out handle);
        }

        /// <summary> 자동 필터 세팅 </summary>
        public bool SetAutoSellFilter(bool enabled, ItemQuality maxQuality, ItemRarity maxRarity)
        {
            switch(m_autoSellFilter.Set(enabled, maxQuality, maxRarity))
            {
                case SetFilterResult.Changed:
                    OnAutoSellFilterChanged?.Invoke();
                    RequestSave();
                    return true;
                case SetFilterResult.UnChanged:
                    return true;
                case SetFilterResult.Invalid:
                default:
                    return false;
            }
        }

        /// <summary> 인벤토리에 빈 슬롯이 있는 지 확인하는 함수 </summary>
        public bool HasEmptySlot(ItemType itemType)
        {
            return m_slotStorage.FindEmptySlotIndex(itemType) >= 0;
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

            if (itemEntity is Entity_Fish fish && m_autoSellFilter.IsAutoSellTarget(fish))
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

            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if (MergeStackableItem(itemHandle, itemEntity))
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

        /// <summary>
        /// 아이템 복수 개를 삭제해야 할 때 사용하는 함수
        /// </summary>
        public bool RemoveByHandles(IReadOnlyCollection<ItemQuantity> items, bool requestSave = true)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }

            HashSet<EntityHandle> uniqueHandles = new();
            List<(ItemQuantity Item, ItemType SlotType, int SlotIndex, Entity Entity, bool IsStackable, int NextQuantity)> entries = new(items.Count);

            foreach (ItemQuantity item in items)
            {
                if (item.Amount <= 0 || !uniqueHandles.Add(item.Handle)
                    || !m_slotStorage.FindSlot(item.Handle, out ItemType slotType, out int slotIndex))
                {
                    return false;
                }

                Entity entity = EntityManager.Get(item.Handle);

                if (!InventoryItemRules.CanRemove(entity, item.Amount))
                {
                    return false;
                }

                bool isStackable = InventoryItemRules.GetStackQuantity(entity, out int currentQuantity);
                int nextQuantity = isStackable ? currentQuantity - item.Amount : 0;
                entries.Add((item, slotType, slotIndex, entity, isStackable, nextQuantity));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry.IsStackable && entry.NextQuantity > 0)
                {
                    InventoryItemRules.SetStackQuantity(entry.Entity, entry.NextQuantity);
                    continue;
                }

                m_slotStorage.ClearHandle(entry.SlotType, entry.SlotIndex, out _);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (!entry.IsStackable || entry.NextQuantity <= 0)
                {
                    EntityManager.Destroy(entry.Item.Handle);
                }
            }

            NotifyInventoryChanged($"Remove item batch / requestCount: {entries.Count}", requestSave);
            return true;
        }

        //item이 인벤토리에 몇 개 있는지 반환
        public int GetTotalQuantityByDataId(ItemType itemType, int dataId)
        {
            if (dataId <= 0)
            {
                return 0;
            }

            int slotCount = m_slotStorage.GetMaxSlotCount(itemType);
            int totalQuantity = 0;

            for (int i = 0; i < slotCount; i++)
            {
                if (!m_slotStorage.GetHandle(itemType, i, out EntityHandle handle))
                {
                    continue;
                }

                Entity entity = EntityManager.Get(handle);

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

            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);
            int slotCount = m_slotStorage.GetMaxSlotCount(itemType);
            int remainingAmount = amount;

            for (int i = 0; i < slotCount; i++)
            {
                if (remainingAmount <= 0)
                {
                    break;
                }

                if (!m_slotStorage.GetHandle(itemType, i, out EntityHandle handle))
                {
                    continue;
                }

                Entity entity = EntityManager.Get(handle);

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
            return m_saveLoad.Capture(m_autoSellFilter.AutoSellEnabled, m_autoSellFilter.MaxAutoSellQuality, m_autoSellFilter.MaxAutoSellRarity);
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

        
        
        private void RestoreLoadedSave()
        {
            InventoryAutoSellFilterState filterState = m_saveLoad.Restore(m_loadedSave, out int restoredCount, out int failedCount);
            m_autoSellFilter.Set(filterState.Enabled, filterState.MaxQuality, filterState.MaxRarity);

            NotifyInventoryChanged($"Restore inventory / restored: {restoredCount}, failed: {failedCount}", false);
        }

        private bool MergeStackableItem(EntityHandle incomingHandle, Entity incomingEntity)
        {
            if (!InventoryItemRules.GetItemType(incomingEntity, out ItemType itemType)
                || !InventoryItemRules.GetStackQuantity(incomingEntity, out int incomingQuantity)
                || incomingQuantity <= 0)
            {
                return false;
            }

            int slotCount = m_slotStorage.GetMaxSlotCount(itemType);

            for (int i = 0; i < slotCount; i++)
            {
                Entity existingEntity = GetAliveEntityOrClear(itemType, i);

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

        private Entity GetAliveEntityOrClear(ItemType itemType, int slotIndex)
        {
            if (!m_slotStorage.GetHandle(itemType, slotIndex, out EntityHandle handle))
            {
                return null;
            }

            Entity entity = EntityManager.Get(handle);

            if (entity == null)
            {
                m_slotStorage.ClearHandle(itemType, slotIndex, out _);
                return null;
            }

            return entity;
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