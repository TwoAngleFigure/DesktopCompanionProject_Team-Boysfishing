#if UNITY_EDITOR
using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.DebugTools
{
    public class InventoryDebugBridge : MonoBehaviour
    {
        [Header("Add Selected Item")]
        [SerializeField] private ItemType addItemType = ItemType.Fish;
        [SerializeField] private int addDataId = 100001;
        [SerializeField] private int addQuantity = 1;
        [SerializeField] private float addFishSize = 10f;
        [SerializeField] private ItemQuality addFishQuality = ItemQuality.OneStar;

        [Header("Add One Each")]
        [SerializeField] private int fishDataId = 100001;
        [SerializeField] private int equipmentDataId = 200001;
        [SerializeField] private int materialDataId = 300001;
        [SerializeField] private int consumableDataId = 400001;
        [SerializeField] private int stackQuantity = 5;

        [Header("Consume")]
        [SerializeField] private ItemType consumeItemType = ItemType.Materials;
        [SerializeField] private int consumeDataId = 300001;
        [SerializeField] private int consumeAmount = 1;

        [Header("Remove")]
        [SerializeField] private ItemType removeItemType = ItemType.Materials;
        [SerializeField] private int removeSlotIndex = 0;
        [SerializeField] private bool destroyEntityOnRemove = true;

        [Header("Swap")]
        [SerializeField] private ItemType swapItemType = ItemType.Materials;
        [SerializeField] private int swapFromIndex = 0;
        [SerializeField] private int swapToIndex = 1;

        [ContextMenu("Inventory Debug/Add Selected Item")]
        private void AddSelectedItem()
        {
            InventoryDebugSystem debugSystem = GetDebugSystem();

            if (debugSystem == null)
            {
                return;
            }

            debugSystem.DebugCreateAndAddItem(addItemType, addDataId, addQuantity, addFishSize, addFishQuality);
        }

        [ContextMenu("Inventory Debug/Add One Each")]
        private void AddOneEach()
        {
            InventoryDebugSystem debugSystem = GetDebugSystem();

            if (debugSystem == null)
            {
                return;
            }

            debugSystem.DebugCreateAndAddItem(ItemType.Fish, fishDataId, 1, addFishSize, addFishQuality);
            debugSystem.DebugCreateAndAddItem(ItemType.Equipment, equipmentDataId, 1, addFishSize, addFishQuality);
            debugSystem.DebugCreateAndAddItem(ItemType.Materials, materialDataId, stackQuantity, addFishSize, addFishQuality);
            debugSystem.DebugCreateAndAddItem(ItemType.Consumables, consumableDataId, stackQuantity, addFishSize, addFishQuality);
        }

        [ContextMenu("Inventory Debug/Consume Item By DataId")]
        private void ConsumeItemByDataId()
        {
            InventoryDebugSystem debugSystem = GetDebugSystem();

            if (debugSystem == null)
            {
                return;
            }

            debugSystem.DebugConsumeItemByDataId(consumeItemType, consumeDataId, consumeAmount);
        }

        [ContextMenu("Inventory Debug/Remove Slot")]
        private void RemoveSlot()
        {
            InventoryDebugSystem debugSystem = GetDebugSystem();

            if (debugSystem == null)
            {
                return;
            }

            debugSystem.DebugRemoveAt(removeItemType, removeSlotIndex, destroyEntityOnRemove);
        }

        [ContextMenu("Inventory Debug/Swap Slots")]
        private void SwapSlots()
        {
            InventoryDebugSystem debugSystem = GetDebugSystem();

            if (debugSystem == null)
            {
                return;
            }

            debugSystem.DebugSwapSlots(swapItemType, swapFromIndex, swapToIndex);
        }

        [ContextMenu("Inventory Debug/Print All Tabs")]
        private void PrintAllTabs()
        {
            InventoryDebugSystem debugSystem = GetDebugSystem();

            if (debugSystem == null)
            {
                return;
            }

            debugSystem.DebugPrintAllTabs();
        }

        [ContextMenu("Inventory Debug/Print Fish Tab")]
        private void PrintFishTab()
        {
            InventoryDebugSystem debugSystem = GetDebugSystem();

            if (debugSystem == null)
            {
                return;
            }

            debugSystem.DebugPrintFishTab();
        }

        [ContextMenu("Inventory Debug/Print Equipment Tab")]
        private void PrintEquipmentTab()
        {
            InventoryDebugSystem debugSystem = GetDebugSystem();

            if (debugSystem == null)
            {
                return;
            }

            debugSystem.DebugPrintEquipmentTab();
        }

        [ContextMenu("Inventory Debug/Print Material-Consumable Tab")]
        private void PrintMaterialConsumableTab()
        {
            InventoryDebugSystem debugSystem = GetDebugSystem();

            if (debugSystem == null)
            {
                return;
            }

            debugSystem.DebugPrintMaterialConsumableTab();
        }

        private InventoryDebugSystem GetDebugSystem()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[InventoryDebugBridge] Enter Play Mode before using inventory debug menu.");
                return null;
            }

            InventoryDebugSystem debugSystem = InventoryDebugSystem.Instance;

            if (debugSystem == null)
            {
                Debug.LogError("[InventoryDebugBridge] InventoryDebugSystem not found. Register it in GameManager.RegisterSystems().");
                return null;
            }

            return debugSystem;
        }
    }
}
#endif
