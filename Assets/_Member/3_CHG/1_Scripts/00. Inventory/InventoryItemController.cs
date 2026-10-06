using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    internal readonly struct AddItemContext
    {
        public EntityHandle Handle { get; }
        public Entity ItemEntity { get; }
        public ItemType Type { get; }

        public AddItemContext(EntityHandle handle, Entity itemEntity, ItemType type)
        {
            Handle = handle;
            ItemEntity = itemEntity;
            Type = type;
        }
    }

    public enum AddItemResult
    {
        Added,
        Merged,
        NoSpace,
        Invalid
    }
    internal sealed class InventoryItemController
    {
        private readonly InventorySlotStorage m_slotStorage;
        private readonly EntityManager m_entityManager;

        public InventoryItemController(InventorySlotStorage slotStorage, EntityManager entityManager)
        {
            m_slotStorage = slotStorage;
            m_entityManager = entityManager;
        }

        #region AddItem
        public AddItemResult IsValidToAddItem(EntityHandle itemHandle, out AddItemContext context)
        {
            context = default;

            if (IsEmptyHandle(itemHandle) || m_slotStorage.ContainsHandle(itemHandle))
            {
                return AddItemResult.Invalid;
            }

            Entity itemEntity = m_entityManager.Get(itemHandle);

            if (itemEntity == null || !InventoryItemRules.GetItemType(itemEntity, out ItemType itemType))
            {
                return AddItemResult.Invalid;
            }

            // 스택 아이템은 수량이 1개 이상일 때만 인벤토리에 수납
            if (InventoryItemRules.GetStackQuantity(itemEntity, out int incomingQuantity) && incomingQuantity <= 0)
            {
                return AddItemResult.Invalid;
            }

            AddItemContext newContext = new AddItemContext(itemHandle, itemEntity, itemType);
            context = newContext;

            return AddItemResult.Added;
        }

        public AddItemResult AddItem(AddItemContext? context)
        {
            EntityHandle itemHandle = context.Value.Handle;
            Entity itemEntity = context.Value.ItemEntity;
            ItemType itemType = context.Value.Type;

            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if (MergeStackableItem(itemHandle, itemEntity))
            {
                return AddItemResult.Merged;
            }

            int emptyIndex = m_slotStorage.FindEmptySlotIndex(slotType);

            if (emptyIndex < 0)
            {
                return AddItemResult.NoSpace;
            }
            m_slotStorage.SetHandle(slotType, emptyIndex, itemHandle);

            return AddItemResult.Added;
        }

        private bool IsEmptyHandle(EntityHandle handle)
        {
            return handle.Value == Guid.Empty;
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

                m_entityManager.Destroy(incomingHandle);

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

            Entity entity = m_entityManager.Get(handle);

            if (entity == null)
            {
                m_slotStorage.ClearHandle(itemType, slotIndex, out _);
                return null;
            }

            return entity;
        }
        #endregion

        public bool SwapSlots(ItemType itemType, int fromIndex, int toIndex)
        {
            if (!m_slotStorage.IsValidSlotIndex(itemType, fromIndex) || !m_slotStorage.IsValidSlotIndex(itemType, toIndex))
            {
                return false;
            }

            if (fromIndex == toIndex)
            {
                return false;
            }

            m_slotStorage.Swap(itemType, fromIndex, toIndex);

            return true;
        }

        #region RemoveItem
        public bool RemoveAt(ItemType itemType, int slotIndex, bool destroyEntity = false)
        {
            if (!m_slotStorage.ClearHandle(itemType, slotIndex, out EntityHandle removedHandle))
            {
                return false;
            }

            if (destroyEntity)
            {
                m_entityManager.Destroy(removedHandle);
            }
            return true;
        }

        public bool RemoveQuantityAt(ItemType itemType, int slotIndex, int amount, out Entity entity, bool destroyEntityWhenZero = true)
        {
            entity = default;
            
            if (amount <= 0)
            {
                return false;
            }

            if (!m_slotStorage.GetHandle(itemType, slotIndex, out EntityHandle handle))
            {
                return false;
            }

            entity = m_entityManager.Get(handle);

            if (entity == null || !InventoryItemRules.GetStackQuantity(entity, out int currentQuantity)
                || currentQuantity < amount)
            {
                return false;
            }

            int nextQuantity = currentQuantity - amount;

            if (nextQuantity > 0)
            {
                InventoryItemRules.SetStackQuantity(entity, nextQuantity);

                return true;
            }
            return RemoveAt(itemType, slotIndex, destroyEntityWhenZero);
        }

        public bool CanRemoveByHandle(EntityHandle handle, int amount)
        {
            if (amount <= 0 || !m_slotStorage.FindSlot(handle, out _, out _))
            {
                return false;
            }

            Entity entity = m_entityManager.Get(handle);
            return InventoryItemRules.CanRemove(entity, amount);
        }

        public bool RemoveByHandles(IReadOnlyCollection<ItemQuantity> items)
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

                Entity entity = m_entityManager.Get(item.Handle);

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
                    m_entityManager.Destroy(entry.Item.Handle);
                }
            }

            return true;
        }
        #endregion

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

                Entity entity = m_entityManager.Get(handle);

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
            if (dataId <= 0 || amount <= 0)
            {
                return false;
            }

            int totalQuantity = GetTotalQuantityByDataId(itemType, dataId);

            if (totalQuantity < amount)
            {
                return false;
            }

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

                Entity entity = m_entityManager.Get(handle);

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
                }
                else
                {
                    if(!RemoveAt(itemType, i, true))
                        return false;
                }

                remainingAmount -= removeAmount;
            }

            bool result = remainingAmount <= 0;

            return result;
        }
    }

}
