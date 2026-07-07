#if UNITY_EDITOR
using System;
using System.Text;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public class InventoryDebugSystem : SystemBase
    {
        public static InventoryDebugSystem Instance { get; private set; }

        private InventorySystem m_inventorySystem;

        public override void Initialize()
        {
            Instance = this;
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();

            if (m_inventorySystem == null)
            {
                Debug.LogError("[InventoryDebugSystem] InventorySystem not found. Register InventorySystem before InventoryDebugSystem.");
                return;
            }

            Debug.Log("[InventoryDebugSystem] Initialized.");
        }

        public bool DebugCreateAndAddItem(ItemType itemType, int dataId, int quantity = 1, float fishSize = 10f, ItemQuality fishQuality = ItemQuality.OneStar)
        {
            if (!TryGetInventorySystem(out InventorySystem inventorySystem))
            {
                return false;
            }

            if (dataId <= 0)
            {
                Debug.LogWarning("[InventoryDebugSystem] Add item failed. dataId is invalid.");
                return false;
            }

            EntityHandle handle = default;

            switch (itemType)
            {
                case ItemType.Fish:
                    handle = EntityManager.Create<ItemData_Fish>(dataId);

                    if (EntityManager.Get(handle) is Entity_Fish fish)
                    {
                        fish.SetRollResult(fishSize, fishQuality);
                    }

                    break;

                case ItemType.Equipment:
                    handle = EntityManager.Create<ItemData_Equipment>(dataId);
                    break;

                case ItemType.Materials:
                    handle = EntityManager.Create<ItemData_Materials>(dataId);

                    if (EntityManager.Get(handle) is Entity_Materials materials)
                    {
                        materials.SetQuantity(Math.Max(1, quantity));
                    }

                    break;

                case ItemType.Consumables:
                    handle = EntityManager.Create<ItemData_Consumables>(dataId);

                    if (EntityManager.Get(handle) is Entity_Consumables consumables)
                    {
                        consumables.SetQuantity(Math.Max(1, quantity));
                    }

                    break;

                default:
                    Debug.LogWarning($"[InventoryDebugSystem] Add item failed. Unsupported ItemType: {itemType}");
                    return false;
            }

            Entity createdEntity = EntityManager.Get(handle);

            if (createdEntity == null)
            {
                Debug.LogWarning($"[InventoryDebugSystem] Add item failed. Entity was not created. itemType: {itemType}, dataId: {dataId}");
                return false;
            }

            bool result = inventorySystem.TryAddItem(handle);

            if (!result)
            {
                EntityManager.Destroy(handle);
                Debug.LogWarning($"[InventoryDebugSystem] Add item failed. Created entity destroyed. itemType: {itemType}, dataId: {dataId}, handle: {handle}");
                return false;
            }

            Debug.Log($"[InventoryDebugSystem] Add item success. itemType: {itemType}, dataId: {dataId}, quantity: {quantity}, handle: {handle}");
            return true;
        }

        public bool DebugConsumeItemByDataId(ItemType itemType, int dataId, int amount)
        {
            if (!TryGetInventorySystem(out InventorySystem inventorySystem))
            {
                return false;
            }

            bool result = inventorySystem.TryConsumeItemByDataId(itemType, dataId, amount);
            Debug.Log($"[InventoryDebugSystem] Consume result: {result}, itemType: {itemType}, dataId: {dataId}, amount: {amount}");
            return result;
        }

        public bool DebugRemoveAt(ItemType itemType, int slotIndex, bool destroyEntity = true)
        {
            if (!TryGetInventorySystem(out InventorySystem inventorySystem))
            {
                return false;
            }

            bool result = inventorySystem.TryRemoveAt(itemType, slotIndex, destroyEntity);
            Debug.Log($"[InventoryDebugSystem] Remove result: {result}, itemType: {itemType}, slotIndex: {slotIndex}, destroyEntity: {destroyEntity}");
            return result;
        }

        public bool DebugSwapSlots(ItemType itemType, int fromIndex, int toIndex)
        {
            if (!TryGetInventorySystem(out InventorySystem inventorySystem))
            {
                return false;
            }

            bool result = inventorySystem.TrySwapSlots(itemType, fromIndex, toIndex);
            Debug.Log($"[InventoryDebugSystem] Swap result: {result}, itemType: {itemType}, fromIndex: {fromIndex}, toIndex: {toIndex}");
            return result;
        }

        public void DebugPrintAllTabs()
        {
            if (!TryGetInventorySystem(out InventorySystem inventorySystem))
            {
                return;
            }

            StringBuilder logBuilder = new StringBuilder();

            logBuilder.AppendLine("========== Inventory Debug / All Tabs ==========");
            AppendTabLog(logBuilder, inventorySystem, "Fish Tab", ItemType.Fish);
            AppendTabLog(logBuilder, inventorySystem, "Equipment Tab", ItemType.Equipment);
            AppendTabLog(logBuilder, inventorySystem, "Material-Consumable Tab", ItemType.Materials);
            logBuilder.Append("================================================");

            Debug.Log(logBuilder.ToString());
        }

        public void DebugPrintFishTab()
        {
            DebugPrintSingleTab("Fish Tab", ItemType.Fish);
        }

        public void DebugPrintEquipmentTab()
        {
            DebugPrintSingleTab("Equipment Tab", ItemType.Equipment);
        }

        public void DebugPrintMaterialConsumableTab()
        {
            DebugPrintSingleTab("Material-Consumable Tab", ItemType.Materials);
        }

        private void DebugPrintSingleTab(string tabName, ItemType slotType)
        {
            if (!TryGetInventorySystem(out InventorySystem inventorySystem))
            {
                return;
            }

            StringBuilder logBuilder = new StringBuilder();

            logBuilder.AppendLine($"========== Inventory Debug / {tabName} ==========");
            AppendTabLog(logBuilder, inventorySystem, tabName, slotType);
            logBuilder.Append("================================================");

            Debug.Log(logBuilder.ToString());
        }

        private bool TryGetInventorySystem(out InventorySystem inventorySystem)
        {
            inventorySystem = m_inventorySystem;

            if (inventorySystem != null)
            {
                return true;
            }

            inventorySystem = SystemManager.GetSystem<InventorySystem>();
            m_inventorySystem = inventorySystem;

            if (inventorySystem == null)
            {
                Debug.LogError("[InventoryDebugSystem] InventorySystem not found.");
                return false;
            }

            return true;
        }

        private void AppendTabLog(StringBuilder logBuilder, InventorySystem inventorySystem, string tabName, ItemType slotType)
        {
            EntityHandle[] slots = inventorySystem.GetSlots(slotType);

            logBuilder.AppendLine($"----- {tabName} / SlotType: {slotType} -----");

            if (slots == null)
            {
                logBuilder.AppendLine($"[{tabName}] slots are null.");
                logBuilder.AppendLine($"----- End {tabName} -----");
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (IsEmptyHandle(slots[i]))
                {
                    logBuilder.AppendLine($"[{tabName}] Slot {i}: Empty");
                    continue;
                }

                Entity entity = EntityManager.Get(slots[i]);

                if (entity == null)
                {
                    logBuilder.AppendLine($"[{tabName}] Slot {i}: Missing Entity / handle: {slots[i]}");
                    continue;
                }

                logBuilder.AppendLine($"[{tabName}] Slot {i}: {entity.Name} / type: {GetEntityItemTypeName(entity)} / dataId: {entity.DataId} / handle: {slots[i]}{GetDebugEntityExtraInfo(entity)}");
            }

            logBuilder.AppendLine($"----- End {tabName} -----");
        }

        private string GetEntityItemTypeName(Entity entity)
        {
            if (entity is Entity_Fish)
            {
                return ItemType.Fish.ToString();
            }

            if (entity is Entity_Equipment)
            {
                return ItemType.Equipment.ToString();
            }

            if (entity is Entity_Materials)
            {
                return ItemType.Materials.ToString();
            }

            if (entity is Entity_Consumables)
            {
                return ItemType.Consumables.ToString();
            }

            return "Unknown";
        }

        private string GetDebugEntityExtraInfo(Entity entity)
        {
            if (entity is Entity_Fish fish)
            {
                return $" / size: {fish.Size} / quality: {fish.Quality}";
            }

            if (entity is Entity_Equipment equipment)
            {
                return $" / upgradeLevel: {equipment.UpgradeLevel}";
            }

            if (entity is Entity_Materials materials)
            {
                return $" / quantity: {materials.Quantity}";
            }

            if (entity is Entity_Consumables consumables)
            {
                return $" / quantity: {consumables.Quantity}";
            }

            return string.Empty;
        }

        private bool IsEmptyHandle(EntityHandle handle)
        {
            return handle.Value == Guid.Empty;
        }
    }
}
#endif
