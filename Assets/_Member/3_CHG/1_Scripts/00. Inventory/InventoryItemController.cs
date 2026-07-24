using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using System;

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
    }

}
