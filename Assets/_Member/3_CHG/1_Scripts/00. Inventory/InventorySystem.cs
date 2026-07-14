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
        private const int ExpandableInventoryInitialSize = 35;
        private const int ExpandableInventoryExpandSize = 5;
        private const int ExpandableInventoryRemainingSlots = 5;
        private const bool EnableInventoryDebugLog = true;

        private EntityHandle[] m_fishSlots;
        private EntityHandle[] m_equipmentSlots;
        private EntityHandle[] m_materialSlots;

        private PlayerSystem m_playerSystem;

        public event Action OnInventoryChanged;

        public event Action OnInventorySaveRequested;

        private InventorySave m_loadedSave;

        public string SaveId => "inventory";
        public Type StateType => typeof(InventorySave);

        public override void Initialize()
        {
        }

        public override void PostInitialize()
        {
            m_playerSystem = SystemManager.GetSystem<PlayerSystem>();

            int fishInventorySize = GetCurrentInventorySize();
            int equipmentInventorySize = GetInitialExpandableInventorySize(ItemType.Equipment);
            int materialInventorySize = GetInitialExpandableInventorySize(ItemType.Materials);

            m_fishSlots = CreateSlots(fishInventorySize);
            m_equipmentSlots = CreateSlots(equipmentInventorySize);
            m_materialSlots = CreateSlots(materialInventorySize);

            if (m_loadedSave != null)
            {
                RestoreLoadedSave();
                m_loadedSave = null;
            }

            m_playerSystem.OnStatChanged += HandlePlayerStatChanged;

            LogDebug($"PostInitialize complete. fishSlots: {fishInventorySize}, equipmentSlots: {equipmentInventorySize}, materialSlots: {materialInventorySize}");
        }

        /// <summary>
        /// itemType 슬롯 Read용 함수
        /// </summary>
        /// <param name="itemType"></param>
        public EntityHandle[] GetSlots(ItemType itemType)
        {
            EntityHandle[] sourceSlots = GetSlotArray(itemType);
            EntityHandle[] copiedSlots = new EntityHandle[sourceSlots.Length];

            Array.Copy(sourceSlots, copiedSlots, sourceSlots.Length);

            return copiedSlots;
        }

        public int GetMaxSlotCount(ItemType itemType)
        {
            return GetSlotArray(itemType).Length;
        }

        public int GetUsedSlotCount(ItemType itemType)
        {
            EntityHandle[] slots = GetSlotArray(itemType);
            int usedCount = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (!IsEmptyHandle(slots[i]))
                {
                    usedCount++;
                }
            }

            return usedCount;
        }

        public bool GetHandleAt(ItemType itemType, int slotIndex, out EntityHandle handle)
        {
            handle = default;

            EntityHandle[] slots = GetSlotArray(itemType);

            if (!IsValidSlotIndex(slots, slotIndex))
            {
                return false;
            }

            if (IsEmptyHandle(slots[slotIndex]))
            {
                return false;
            }

            handle = slots[slotIndex];
            return true;
        }

        public bool AddItem(EntityHandle itemHandle)
        {
            LogDebug($"TryAddItem called. handle: {itemHandle}");

            if (IsEmptyHandle(itemHandle))
            {
                LogWarning("TryAddItem failed. Handle is empty.");
                return false;
            }

            if (ContainsHandle(itemHandle))
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

            if (!GetItemType(itemEntity, out ItemType itemType))
            {
                LogWarning($"TryAddItem failed. Unsupported entity type: {itemEntity.GetType().Name}");
                return false;
            }

            EntityHandle[] slots = GetSlotArray(itemType);
            ItemType slotType = NormalizeSlotType(itemType);

            if (MergeStackableItem(itemHandle, itemEntity, slots))
            {
                LogDebug($"TryAddItem merged. slotType: {slotType}, itemType: {itemType}, dataId: {itemEntity.DataId}, name: {itemEntity.Name}");
                NotifyInventoryChanged($"Merge item / slotType: {slotType}, itemType: {itemType}, dataId: {itemEntity.DataId}", true);
                return true;
            }

            ExpandInventoryIfNeeded(slotType);

            slots = GetSlotArray(slotType);
            int emptyIndex = FindEmptySlotIndex(slots);

            if (emptyIndex < 0)
            {
                LogWarning($"TryAddItem failed. No empty slot. slotType: {slotType}, itemType: {itemType}, dataId: {itemEntity.DataId}, name: {itemEntity.Name}");
                return false;
            }

            slots[emptyIndex] = itemHandle;
            ExpandInventoryIfNeeded(slotType);

            LogDebug($"TryAddItem success. slotType: {slotType}, itemType: {itemType}, slotIndex: {emptyIndex}, dataId: {itemEntity.DataId}, name: {itemEntity.Name}, handle: {itemHandle}");
            NotifyInventoryChanged($"Add item / slotType: {slotType}, itemType: {itemType}, slotIndex: {emptyIndex}, dataId: {itemEntity.DataId}", true);

            return true;
        }

        public bool RemoveAt(ItemType itemType, int slotIndex, bool destroyEntity = false)
        {
            EntityHandle[] slots = GetSlotArray(itemType);
            ItemType slotType = NormalizeSlotType(itemType);

            if (!IsValidSlotIndex(slots, slotIndex))
            {
                LogWarning($"TryRemoveAt failed. Invalid slot. slotType: {slotType}, itemType: {itemType}, slotIndex: {slotIndex}");
                return false;
            }

            if (IsEmptyHandle(slots[slotIndex]))
            {
                LogWarning($"TryRemoveAt failed. Slot is empty. slotType: {slotType}, itemType: {itemType}, slotIndex: {slotIndex}");
                return false;
            }

            EntityHandle removedHandle = slots[slotIndex];
            slots[slotIndex] = default;

            if (destroyEntity)
            {
                EntityManager.Destroy(removedHandle);
            }

            LogDebug($"TryRemoveAt success. itemType: {itemType}, slotIndex: {slotIndex}, destroyEntity: {destroyEntity}");
            NotifyInventoryChanged($"Remove item / itemType: {itemType}, slotIndex: {slotIndex}", true);
            return true;
        }

        public bool SwapSlots(ItemType itemType, int fromIndex, int toIndex)
        {
            EntityHandle[] slots = GetSlotArray(itemType);
            ItemType slotType = NormalizeSlotType(itemType);

            if (!IsValidSlotIndex(slots, fromIndex) || !IsValidSlotIndex(slots, toIndex))
            {
                LogWarning($"TrySwapSlots failed. Invalid index. slotType: {slotType}, itemType: {itemType}, from: {fromIndex}, to: {toIndex}");
                return false;
            }

            if (fromIndex == toIndex)
            {
                LogDebug($"TrySwapSlots skipped. Same index. itemType: {itemType}, index: {fromIndex}");
                return false;
            }

            EntityHandle tempHandle = slots[toIndex];
            slots[toIndex] = slots[fromIndex];
            slots[fromIndex] = tempHandle;

            LogDebug($"TrySwapSlots success. slotType: {slotType}, itemType: {itemType}, from: {fromIndex}, to: {toIndex}");
            NotifyInventoryChanged($"Swap slots / slotType: {slotType}, from: {fromIndex}, to: {toIndex}", true);

            return true;
        }

        public bool RemoveQuantityAt(ItemType itemType, int slotIndex, int amount, bool destroyEntityWhenZero = true)
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

            if (!GetStackQuantity(entity, out int currentQuantity))
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
                SetStackQuantity(entity, nextQuantity);

                LogDebug($"TryRemoveQuantityAt success. dataId: {entity.DataId}, name: {entity.Name}, before: {currentQuantity}, remove: {amount}, after: {nextQuantity}");
                NotifyInventoryChanged($"Decrease quantity / dataId: {entity.DataId}, amount: {amount}", true);

                return true;
            }

            LogDebug($"TryRemoveQuantityAt zero. dataId: {entity.DataId}, name: {entity.Name}, before: {currentQuantity}, remove: {amount}. Slot will be removed.");
            return RemoveAt(itemType, slotIndex, destroyEntityWhenZero);
        }

        //item이 인벤토리에 몇 개 있는지 반환
        public int GetTotalQuantityByDataId(ItemType itemType, int dataId)
        {
            if (dataId <= 0)
            {
                return 0;
            }

            EntityHandle[] slots = GetSlotArray(itemType);
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

                if (GetStackQuantity(entity, out int quantity))
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

            EntityHandle[] slots = GetSlotArray(itemType);
            ItemType slotType = NormalizeSlotType(itemType);
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

                if (!GetStackQuantity(entity, out int quantity))
                {
                    continue;
                }

                int removeAmount = Math.Min(quantity, remainingAmount);
                int nextQuantity = quantity - removeAmount;

                if (nextQuantity > 0)
                {
                    SetStackQuantity(entity, nextQuantity);
                    LogDebug($"Consume partial stack. slotType: {slotType}, slotIndex: {i}, dataId: {dataId}, before: {quantity}, remove: {removeAmount}, after: {nextQuantity}");
                }
                else
                {
                    EntityHandle removedHandle = slots[i];
                    slots[i] = default;
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

            CaptureSlots(save, ItemType.Fish, m_fishSlots);
            CaptureSlots(save, ItemType.Equipment, m_equipmentSlots);
            CaptureSlots(save, ItemType.Materials, m_materialSlots);

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

                if (!GetItemType(entity, out ItemType itemType))
                {
                    LogWarning($"CaptureSlots skipped. Unsupported entity type: {entity.GetType().Name}");
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
                slotSave.quantity = Math.Max(1, materials.Quantity);
            }
            else if (entity is Entity_Consumables consumables)
            {
                slotSave.quantity = Math.Max(1, consumables.Quantity);
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

            EntityHandle[] slots = GetSlotArray(slotSave.itemType);
            ItemType slotType = NormalizeSlotType(slotSave.itemType);
            int targetIndex = slotSave.slotIndex;

            if (!IsValidSlotIndex(slots, targetIndex) || !IsEmptyHandle(slots[targetIndex]))
            {
                int originalIndex = targetIndex;
                targetIndex = FindEmptySlotIndex(slots);

                LogWarning($"RestoreSlot target slot unavailable. slotType: {slotType}, itemType: {slotSave.itemType}, originalIndex: {originalIndex}, fallbackIndex: {targetIndex}");
            }

            if (targetIndex < 0)
            {
                EntityManager.Destroy(restoredHandle);

                LogWarning($"RestoreSlot failed. No empty slot. slotType: {slotType}, itemType: {slotSave.itemType}, dataId: {slotSave.dataId}");
                return false;
            }

            slots[targetIndex] = restoredHandle;

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

            restoredHandle = EntityManager.Restore<ItemData_Materials>(parsedHandle, slotSave.dataId);

            if (EntityManager.Get(restoredHandle) is Entity_Materials materials)
            {
                materials.SetQuantity(Math.Max(1, slotSave.quantity));
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

            restoredHandle = EntityManager.Restore<ItemData_Consumables>(parsedHandle, slotSave.dataId);

            if (EntityManager.Get(restoredHandle) is Entity_Consumables consumables)
            {
                consumables.SetQuantity(Math.Max(1, slotSave.quantity));
                return true;
            }

            return false;
        }

        private bool MergeStackableItem(EntityHandle incomingHandle, Entity incomingEntity, EntityHandle[] targetSlots)
        {
            if (incomingEntity is Entity_Materials incomingMaterials)
            {
                return MergeMaterials(incomingHandle, incomingMaterials, targetSlots);
            }

            if (incomingEntity is Entity_Consumables incomingConsumables)
            {
                return MergeConsumables(incomingHandle, incomingConsumables, targetSlots);
            }

            return false;
        }

        private bool MergeMaterials(EntityHandle incomingHandle, Entity_Materials incomingMaterials, EntityHandle[] targetSlots)
        {
            if (incomingMaterials.Quantity <= 0)
            {
                return false;
            }

            for (int i = 0; i < targetSlots.Length; i++)
            {
                Entity existingEntity = GetAliveEntityOrClear(targetSlots, i);

                if (existingEntity is Entity_Materials existingMaterials && existingMaterials.DataId == incomingMaterials.DataId)
                {
                    int beforeQuantity = existingMaterials.Quantity;

                    existingMaterials.Add(incomingMaterials.Quantity);
                    EntityManager.Destroy(incomingHandle);

                    LogDebug($"Merge materials. dataId: {existingMaterials.DataId}, before: {beforeQuantity}, add: {incomingMaterials.Quantity}, after: {existingMaterials.Quantity}");
                    return true;
                }
            }

            return false;
        }

        private bool MergeConsumables(EntityHandle incomingHandle, Entity_Consumables incomingConsumables, EntityHandle[] targetSlots)
        {
            if (incomingConsumables.Quantity <= 0)
            {
                return false;
            }

            for (int i = 0; i < targetSlots.Length; i++)
            {
                Entity existingEntity = GetAliveEntityOrClear(targetSlots, i);

                if (existingEntity is Entity_Consumables existingConsumables && existingConsumables.DataId == incomingConsumables.DataId)
                {
                    int beforeQuantity = existingConsumables.Quantity;

                    existingConsumables.Add(incomingConsumables.Quantity);
                    EntityManager.Destroy(incomingHandle);

                    LogDebug($"Merge consumables. dataId: {existingConsumables.DataId}, before: {beforeQuantity}, add: {incomingConsumables.Quantity}, after: {existingConsumables.Quantity}");
                    return true;
                }
            }

            return false;
        }

        private Entity GetAliveEntityOrClear(EntityHandle[] slots, int slotIndex)
        {
            if (IsEmptyHandle(slots[slotIndex]))
            {
                return null;
            }

            Entity entity = EntityManager.Get(slots[slotIndex]);

            if (entity == null)
            {
                slots[slotIndex] = default;
                return null;
            }

            return entity;
        }

        private bool GetItemType(Entity entity, out ItemType itemType)
        {
            itemType = default;

            if (entity is Entity_Fish)
            {
                itemType = ItemType.Fish;
                return true;
            }

            if (entity is Entity_Equipment)
            {
                itemType = ItemType.Equipment;
                return true;
            }

            if (entity is Entity_Materials)
            {
                itemType = ItemType.Materials;
                return true;
            }

            if (entity is Entity_Consumables)
            {
                itemType = ItemType.Consumables;
                return true;
            }

            return false;
        }

        private EntityHandle[] GetSlotArray(ItemType itemType)
        {
            ItemType slotType = NormalizeSlotType(itemType);

            switch (slotType)
            {
                case ItemType.Fish:
                    return m_fishSlots;

                case ItemType.Equipment:
                    return m_equipmentSlots;

                case ItemType.Materials:
                    return m_materialSlots;

                default:
                    return Array.Empty<EntityHandle>();
            }
        }

        private ItemType NormalizeSlotType(ItemType itemType)
        {
            if (itemType == ItemType.Consumables)
            {
                return ItemType.Materials;
            }

            return itemType;
        }

        private int GetInitialExpandableInventorySize(ItemType itemType)
        {
            ItemType slotType = NormalizeSlotType(itemType);

            if (!IsExpandableInventoryType(slotType) || m_loadedSave == null || m_loadedSave.slots == null)
            {
                return ExpandableInventoryInitialSize;
            }

            int highestSlotIndex = -1;
            int savedSlotCount = 0;

            for (int i = 0; i < m_loadedSave.slots.Count; i++)
            {
                InventorySave.SlotSave slotSave = m_loadedSave.slots[i];

                if (NormalizeSlotType(slotSave.itemType) != slotType)
                {
                    continue;
                }

                savedSlotCount++;
                highestSlotIndex = Math.Max(highestSlotIndex, slotSave.slotIndex);
            }

            int requiredUsedSlotCount = Math.Max(highestSlotIndex + 1, savedSlotCount);

            // 빈 슬롯이 5개가 되는 순간 한 줄 추가
            // -> 복원 직후에는 최소 6개의 빈 슬롯이 남도록 계산
            int requiredSlotCount = requiredUsedSlotCount + ExpandableInventoryRemainingSlots + 1;
            int roundedSlotCount = RoundUpToMultiple(requiredSlotCount, ExpandableInventoryExpandSize);

            return Math.Max(ExpandableInventoryInitialSize, roundedSlotCount);
        }

        private void ExpandInventoryIfNeeded(ItemType itemType)
        {
            ItemType slotType = NormalizeSlotType(itemType);

            if (!IsExpandableInventoryType(slotType))
            {
                return;
            }

            EntityHandle[] slots = GetSlotArray(slotType);
            int remainingSlotCount = slots.Length - GetUsedSlotCount(slotType);

            if (remainingSlotCount > ExpandableInventoryRemainingSlots)
            {
                return;
            }

            int previousSlotSize = slots.Length;
            int nextSlotSize = previousSlotSize + ExpandableInventoryExpandSize;

            ResizeExpandableInventory(slotType, nextSlotSize);

            LogDebug($"Expandable inventory expanded. slotType: {slotType}, before: {previousSlotSize}, after: {nextSlotSize}, remainingBeforeExpand: {remainingSlotCount}");
        }

        private void ResizeExpandableInventory(ItemType itemType, int slotSize)
        {
            ItemType slotType = NormalizeSlotType(itemType);

            switch (slotType)
            {
                case ItemType.Equipment:
                    Array.Resize(ref m_equipmentSlots, slotSize);
                    break;

                case ItemType.Materials:
                    Array.Resize(ref m_materialSlots, slotSize);
                    break;
            }
        }

        private bool IsExpandableInventoryType(ItemType itemType)
        {
            ItemType slotType = NormalizeSlotType(itemType);
            return slotType == ItemType.Equipment || slotType == ItemType.Materials;
        }

        private int RoundUpToMultiple(int value, int multiple)
        {
            if (value <= 0)
            {
                return multiple;
            }

            return ((value + multiple - 1) / multiple) * multiple;
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

        private EntityHandle[] CreateSlots(int slotSize)
        {
            return new EntityHandle[slotSize];
        }

        private int FindEmptySlotIndex(EntityHandle[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (IsEmptyHandle(slots[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool ContainsHandle(EntityHandle handle)
        {
            return ContainsHandle(m_fishSlots, handle) || ContainsHandle(m_equipmentSlots, handle) || ContainsHandle(m_materialSlots, handle);
        }

        private bool ContainsHandle(EntityHandle[] slots, EntityHandle handle)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Equals(handle))
                {
                    return true;
                }
            }

            return false;
        }

        private bool GetStackQuantity(Entity entity, out int quantity)
        {
            quantity = 0;

            if (entity is Entity_Materials materials)
            {
                quantity = materials.Quantity;
                return true;
            }

            if (entity is Entity_Consumables consumables)
            {
                quantity = consumables.Quantity;
                return true;
            }

            return false;
        }

        private void SetStackQuantity(Entity entity, int quantity)
        {
            if (entity is Entity_Materials materials)
            {
                materials.SetQuantity(quantity);
            }
            else if (entity is Entity_Consumables consumables)
            {
                consumables.SetQuantity(quantity);
            }
        }

        private bool IsValidSlotIndex(EntityHandle[] slots, int slotIndex)
        {
            return slots != null && slotIndex >= 0 && slotIndex < slots.Length;
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

            if (!CanResizeSlotArray(m_fishSlots, slotSize))
            {
                LogWarning($"ResizeFishSlots failed. Fish exist outside next slot size. nextSize: {slotSize}");
                return false;
            }

            Array.Resize(ref m_fishSlots, slotSize);
            NotifyInventoryChanged($"Resize fish inventory / slotSize: {slotSize}", false);

            return true;
        }

        private bool CanResizeSlotArray(EntityHandle[] slots, int nextSlotSize)
        {
            if (slots == null)
            {
                return true;
            }

            if (nextSlotSize >= slots.Length)
            {
                return true;
            }

            // 줄어드는 경우, 잘려나갈 범위에 아이템이 있으면 금지.
            for (int i = nextSlotSize; i < slots.Length; i++)
            {
                if (!IsEmptyHandle(slots[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void HandlePlayerStatChanged(EntityHandle playerHandle)
        {
            int nextInventorySize = GetCurrentInventorySize();

            bool result = ResizeFishSlots(nextInventorySize);

            LogDebug($"Fish inventory size stat changed. nextSize: {nextInventorySize}, resizeResult: {result}");
        }

#if UNITY_EDITOR
        public void DebugPrintSlots()
        {
            Debug.Log("========== Inventory Slots ==========");
            DebugPrintSlotArray("Fish", m_fishSlots);
            DebugPrintSlotArray("Equipment", m_equipmentSlots);
            DebugPrintSlotArray("Materials + Consumables", m_materialSlots);
            Debug.Log("=====================================");
        }

        private void DebugPrintSlotArray(string title, EntityHandle[] slots)
        {
            Debug.Log($"--- {title} ---");

            for (int i = 0; i < slots.Length; i++)
            {
                if (IsEmptyHandle(slots[i]))
                {
                    Debug.Log($"[{i}] Empty");
                    continue;
                }

                Entity entity = EntityManager.Get(slots[i]);

                if (entity == null)
                {
                    Debug.LogWarning($"[{i}] Missing Entity / handle: {slots[i]}");
                    continue;
                }

                Debug.Log($"[{i}] {entity.GetType().Name} / DataId: {entity.DataId} / Name: {entity.Name} / Handle: {slots[i]} {GetDebugEntityExtraInfo(entity)}");
            }
        }

        private string GetDebugEntityExtraInfo(Entity entity)
        {
            if (entity is Entity_Fish fish)
            {
                return $"/ Size: {fish.Size} / Quality: {fish.Quality}";
            }

            if (entity is Entity_Equipment equipment)
            {
                return $"/ UpgradeLevel: {equipment.UpgradeLevel}";
            }

            if (entity is Entity_Materials materials)
            {
                return $"/ Quantity: {materials.Quantity}";
            }

            if (entity is Entity_Consumables consumables)
            {
                return $"/ Quantity: {consumables.Quantity}";
            }

            return string.Empty;
        }
#endif
    }
}