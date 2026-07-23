using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 아이템 타입과 스택 규칙 제공
    /// </summary>
    internal static class InventoryItemRules
    {
        public static bool GetItemType(Entity entity, out ItemType itemType)
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

        public static bool GetStackQuantity(Entity entity, out int quantity)
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

        public static void SetStackQuantity(Entity entity, int quantity)
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

        public static bool CanRemove(Entity entity, int amount)
        {
            if (entity == null || amount <= 0)
            {
                return false;
            }

            if (GetStackQuantity(entity, out int quantity))
            {
                return amount <= quantity;
            }

            return amount == 1 && (entity is Entity_Fish || entity is Entity_Equipment);
        }

        /// <summary>
        /// 같은 타입과 DataId를 가진 재료/소모품 스택 머징
        /// </summary>
        public static bool MergeStack(Entity existingEntity, Entity incomingEntity, out int beforeQuantity, out int addedQuantity, out int afterQuantity)
        {
            beforeQuantity = 0;
            addedQuantity = 0;
            afterQuantity = 0;

            if (existingEntity is Entity_Materials existingMaterials
                && incomingEntity is Entity_Materials incomingMaterials
                && existingMaterials.DataId == incomingMaterials.DataId
                && incomingMaterials.Quantity > 0)
            {
                beforeQuantity = existingMaterials.Quantity;
                addedQuantity = incomingMaterials.Quantity;

                existingMaterials.Add(addedQuantity);
                afterQuantity = existingMaterials.Quantity;

                return true;
            }

            if (existingEntity is Entity_Consumables existingConsumables
                && incomingEntity is Entity_Consumables incomingConsumables
                && existingConsumables.DataId == incomingConsumables.DataId
                && incomingConsumables.Quantity > 0)
            {
                beforeQuantity = existingConsumables.Quantity;
                addedQuantity = incomingConsumables.Quantity;

                existingConsumables.Add(addedQuantity);
                afterQuantity = existingConsumables.Quantity;

                return true;
            }

            return false;
        }
    }
}