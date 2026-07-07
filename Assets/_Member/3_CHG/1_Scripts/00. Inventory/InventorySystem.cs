using System;
using System.Collections.Generic;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    [Serializable]
    public class InventorySave
    {
        public List<SlotSave> slots = new List<SlotSave>();

        [Serializable]
        public class SlotSave
        {
            public ItemType itemType;
            public int slotIndex;

            public string handle;
            public int dataId;

            public float size;
            public ItemQuality quality;
            public int upgradeLevel;
            public int quantity;
        }
    }

    /// <summary>
    /// 인벤토리 시스템.
    /// - EntityHandle을 슬롯 배열에 보관
    /// - 아이템 추가 / 삭제 / 수량 소모 / 슬롯 교환
    /// - 저장 / 복원
    /// - 인벤토리 변경 이벤트 발행
    /// </summary>
    public class InventorySystem : SystemBase, ISaveable
    {
        private const int FallbackInventorySize = 20;
        private const bool EnableInventoryDebugLog = true;

        private EntityHandle[] m_fishSlots;
        private EntityHandle[] m_equipmentSlots;
        private EntityHandle[] m_materialSlots;

        // UI 갱신용 이벤트.
        public event Action OnInventoryChanged;

        // 프로토타입용 즉시 저장 요청 이벤트.
        public event Action OnInventorySaveRequested;

        public string SaveId => "inventory";
        public Type StateType => typeof(InventorySave);

        public override void Initialize()
        {
            int baseInventorySize = GetBaseInventorySizeFromPlayerData();

            m_fishSlots = CreateSlots(baseInventorySize);
            m_equipmentSlots = CreateSlots(baseInventorySize);
            m_materialSlots = CreateSlots(baseInventorySize);

            LogDebug($"Initialize complete. slotSize: {baseInventorySize}");
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

        public bool TryGetHandleAt(ItemType itemType, int slotIndex, out EntityHandle handle)
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

        public bool TryAddItem(EntityHandle itemHandle)
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

            if (!TryGetItemType(itemEntity, out ItemType itemType))
            {
                LogWarning($"TryAddItem failed. Unsupported entity type: {itemEntity.GetType().Name}");
                return false;
            }

            EntityHandle[] slots = GetSlotArray(itemType);
            ItemType slotType = NormalizeSlotType(itemType);

            if (TryMergeStackableItem(itemHandle, itemEntity, slots, itemType))
            {
                LogDebug($"TryAddItem merged. slotType: {slotType}, itemType: {itemType}, dataId: {itemEntity.DataId}, name: {itemEntity.Name}");
                NotifyInventoryChanged($"Merge item / slotType: {slotType}, itemType: {itemType}, dataId: {itemEntity.DataId}", true);
                return true;
            }

            int emptyIndex = FindEmptySlotIndex(slots);

            if (emptyIndex < 0)
            {
                LogWarning($"TryAddItem failed. No empty slot. slotType: {slotType}, itemType: {itemType}, dataId: {itemEntity.DataId}, name: {itemEntity.Name}");
                return false;
            }

            slots[emptyIndex] = itemHandle;

            LogDebug($"TryAddItem success. slotType: {slotType}, itemType: {itemType}, slotIndex: {emptyIndex}, dataId: {itemEntity.DataId}, name: {itemEntity.Name}, handle: {itemHandle}");
            NotifyInventoryChanged($"Add item / slotType: {slotType}, itemType: {itemType}, slotIndex: {emptyIndex}, dataId: {itemEntity.DataId}", true);

            return true;
        }

        public bool TryRemoveAt(ItemType itemType, int slotIndex, bool destroyEntity = false)
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
            Entity removedEntity = EntityManager.Get(removedHandle);

            slots[slotIndex] = default;

            if (destroyEntity)
            {
                EntityManager.Destroy(removedHandle);
            }

            if (removedEntity != null)
            {
                LogDebug($"TryRemoveAt success. slotType: {slotType}, itemType: {itemType}, slotIndex: {slotIndex}, dataId: {removedEntity.DataId}, name: {removedEntity.Name}, destroyEntity: {destroyEntity}");
            }
            else
            {
                LogWarning($"TryRemoveAt success but entity was missing. slotType: {slotType}, itemType: {itemType}, slotIndex: {slotIndex}, handle: {removedHandle}, destroyEntity: {destroyEntity}");
            }

            NotifyInventoryChanged($"Remove item / slotType: {slotType}, itemType: {itemType}, slotIndex: {slotIndex}", true);
            return true;
        }

        public bool TrySwapSlots(ItemType itemType, int fromIndex, int toIndex)
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
                LogWarning($"TrySwapSlots skipped. Same index. slotType: {slotType}, itemType: {itemType}, index: {fromIndex}");
                return false;
            }

            EntityHandle tempHandle = slots[toIndex];
            slots[toIndex] = slots[fromIndex];
            slots[fromIndex] = tempHandle;

            LogDebug($"TrySwapSlots success. slotType: {slotType}, itemType: {itemType}, from: {fromIndex}, to: {toIndex}");
            NotifyInventoryChanged($"Swap slots / slotType: {slotType}, from: {fromIndex}, to: {toIndex}", true);

            return true;
        }

        public bool TryRemoveQuantityAt(ItemType itemType, int slotIndex, int amount, bool destroyEntityWhenZero = true)
        {
            if (amount <= 0)
            {
                LogWarning($"TryRemoveQuantityAt failed. Invalid amount: {amount}");
                return false;
            }

            if (!TryGetHandleAt(itemType, slotIndex, out EntityHandle handle))
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

            if (!TryGetStackQuantity(entity, out int currentQuantity))
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
            return TryRemoveAt(itemType, slotIndex, destroyEntityWhenZero);
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

                if (TryGetStackQuantity(entity, out int quantity))
                {
                    totalQuantity += quantity;
                }
            }

            return totalQuantity;
        }

        public bool TryConsumeItemByDataId(ItemType itemType, int dataId, int amount)
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

                if (!TryGetStackQuantity(entity, out int quantity))
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

            LogDebug($"RestoreState start. savedSlotCount: {save.slots.Count}");

            ClearAllSlots(true);

            int restoredCount = 0;
            int failedCount = 0;

            for (int i = 0; i < save.slots.Count; i++)
            {
                bool result = RestoreSlot(save.slots[i]);

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
            if (!EnableInventoryDebugLog)
            {
                return;
            }

            Debug.Log($"[InventorySystem] {message}");
        }

        private void LogWarning(string message)
        {
            if (!EnableInventoryDebugLog)
            {
                return;
            }

            Debug.LogWarning($"[InventorySystem] {message}");
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

                if (!TryGetItemType(entity, out ItemType itemType))
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

            if (!TryRestoreEntity(slotSave, out EntityHandle restoredHandle))
            {
                LogWarning($"RestoreSlot failed. Entity restore failed. itemType: {slotSave.itemType}, dataId: {slotSave.dataId}");
                return false;
            }

            Entity restoredEntity = EntityManager.Get(restoredHandle);

            if (restoredEntity == null)
            {
                LogWarning($"RestoreSlot failed. Restored entity not found. handle: {restoredHandle}");
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

            LogDebug($"RestoreSlot success. slotType: {slotType}, itemType: {slotSave.itemType}, slotIndex: {targetIndex}, dataId: {restoredEntity.DataId}, name: {restoredEntity.Name}, handle: {restoredHandle}");
            return true;
        }

        private bool TryRestoreEntity(InventorySave.SlotSave slotSave, out EntityHandle restoredHandle)
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
                    return TryRestoreFish(slotSave, parsedHandle, out restoredHandle);

                case ItemType.Equipment:
                    return TryRestoreEquipment(slotSave, parsedHandle, out restoredHandle);

                case ItemType.Materials:
                    return TryRestoreMaterials(slotSave, parsedHandle, out restoredHandle);

                case ItemType.Consumables:
                    return TryRestoreConsumables(slotSave, parsedHandle, out restoredHandle);

                default:
                    LogWarning($"TryRestoreEntity failed. Unsupported itemType: {slotSave.itemType}");
                    return false;
            }
        }

        private bool TryRestoreFish(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
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

        private bool TryRestoreEquipment(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
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

        private bool TryRestoreMaterials(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
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

        private bool TryRestoreConsumables(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
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

        private bool TryMergeStackableItem(EntityHandle incomingHandle, Entity incomingEntity, EntityHandle[] targetSlots, ItemType itemType)
        {
            if (incomingEntity is Entity_Materials incomingMaterials)
            {
                return TryMergeMaterials(incomingHandle, incomingMaterials, targetSlots);
            }

            if (incomingEntity is Entity_Consumables incomingConsumables)
            {
                return TryMergeConsumables(incomingHandle, incomingConsumables, targetSlots);
            }

            return false;
        }

        private bool TryMergeMaterials(EntityHandle incomingHandle, Entity_Materials incomingMaterials, EntityHandle[] targetSlots)
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

        private bool TryMergeConsumables(EntityHandle incomingHandle, Entity_Consumables incomingConsumables, EntityHandle[] targetSlots)
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
            if (!IsValidSlotIndex(slots, slotIndex))
            {
                return null;
            }

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

        private bool TryGetItemType(Entity entity, out ItemType itemType)
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

        private int GetBaseInventorySizeFromPlayerData()
        {
            IReadOnlyList<PlayerData> playerDataList = DataManager.GetAll<PlayerData>();

            if (playerDataList == null || playerDataList.Count <= 0)
            {
                LogWarning($"PlayerData not found. fallback slot size: {FallbackInventorySize}");
                return FallbackInventorySize;
            }

            PlayerData playerData = playerDataList[0];

            if (playerData.BaseInventorySize <= 0)
            {
                LogWarning($"PlayerData.BaseInventorySize is invalid. value: {playerData.BaseInventorySize}, fallback slot size: {FallbackInventorySize}");
                return FallbackInventorySize;
            }

            return playerData.BaseInventorySize;
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

        private bool TryGetStackQuantity(Entity entity, out int quantity)
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
            int safeQuantity = Math.Max(0, quantity);

            if (entity is Entity_Materials materials)
            {
                materials.SetQuantity(safeQuantity);
            }
            else if (entity is Entity_Consumables consumables)
            {
                consumables.SetQuantity(safeQuantity);
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

        private void ClearAllSlots(bool destroyEntities)
        {
            ClearSlots(m_fishSlots, destroyEntities);
            ClearSlots(m_equipmentSlots, destroyEntities);
            ClearSlots(m_materialSlots, destroyEntities);
        }

        private void ClearSlots(EntityHandle[] slots, bool destroyEntities)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (IsEmptyHandle(slots[i]))
                {
                    continue;
                }

                EntityHandle handle = slots[i];
                slots[i] = default;

                if (destroyEntities)
                {
                    EntityManager.Destroy(handle);
                }
            }
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