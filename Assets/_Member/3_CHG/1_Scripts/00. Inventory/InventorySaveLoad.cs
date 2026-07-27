using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;
using System;

namespace DesktopCompanion.Systems
{
    internal readonly struct InventoryAutoSellFilterState
    {
        public bool Enabled { get; }
        public ItemQuality MaxQuality { get; }
        public ItemRarity MaxRarity { get; }

        public InventoryAutoSellFilterState(bool enabled, ItemQuality maxQuality, ItemRarity maxRarity)
        {
            Enabled = enabled;
            MaxQuality = maxQuality;
            MaxRarity = maxRarity;
        }
    }

    /// <summary>
    /// 인벤토리 저장 데이터 생성과 복원을 담당한다.
    /// 인벤토리 변경 알림과 저장 요청은 InventorySystem이 담당한다.
    /// </summary>
    internal sealed class InventorySaveLoad
    {
        private readonly InventorySlotStorage m_slotStorage;
        private readonly EntityManager m_entityManager;
        private readonly DataManager m_dataManager;
        private readonly Action<string> m_logDebug;
        private readonly Action<string> m_logWarning;

        public InventorySaveLoad(InventorySlotStorage slotStorage, EntityManager entityManager, DataManager dataManager, Action<string> logDebug, Action<string> logWarning)
        {
            m_slotStorage = slotStorage ?? throw new ArgumentNullException(nameof(slotStorage));
            m_entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            m_dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
            m_logDebug = logDebug;
            m_logWarning = logWarning;
        }

        public InventorySave Capture(bool autoSellEnabled, ItemQuality maxAutoSellQuality, ItemRarity maxAutoSellRarity)
        {
            InventorySave save = new InventorySave
            {
                autoSellEnabled = autoSellEnabled,
                maxAutoSellQuality = NormalizeQuality(maxAutoSellQuality),
                maxAutoSellRarity = NormalizeRarity(maxAutoSellRarity)
            };

            CaptureSlots(save, ItemType.Fish);
            CaptureSlots(save, ItemType.Equipment);
            CaptureSlots(save, ItemType.Materials);

            LogDebug($"CaptureState finished. saveSlotCount: {save.slots.Count}");

            return save;
        }

        public InventoryAutoSellFilterState Restore(InventorySave save, out int restoredCount, out int failedCount)
        {
            restoredCount = 0;
            failedCount = 0;

            InventoryAutoSellFilterState filterState = GetAutoSellFilterState(save);

            if (save == null || save.slots == null)
            {
                LogWarning("RestoreState failed. Save data or slot data is null.");
                return filterState;
            }

            LogDebug($"RestoreState start. savedSlotCount: {save.slots.Count}");

            for (int i = 0; i < save.slots.Count; i++)
            {
                if (RestoreSlot(save.slots[i]))
                {
                    restoredCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            LogDebug($"RestoreState finished. restored: {restoredCount}, failed: {failedCount}");
            return filterState;
        }

        public static int GetInitialExpandableInventorySize(InventorySave save, ItemType itemType)
        {
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if ((slotType != ItemType.Equipment && slotType != ItemType.Materials)
                || save == null
                || save.slots == null)
            {
                return InventorySizeController.ExpandableInventoryInitialSize;
            }

            int highestSlotIndex = -1;
            int savedSlotCount = 0;

            for (int i = 0; i < save.slots.Count; i++)
            {
                InventorySave.SlotSave slotSave = save.slots[i];

                if (InventorySlotStorage.NormalizeSlotType(slotSave.itemType) != slotType)
                {
                    continue;
                }

                savedSlotCount++;
                highestSlotIndex = Math.Max(highestSlotIndex, slotSave.slotIndex);
            }

            return InventorySizeController.CalculateInitialExpandableSize(highestSlotIndex, savedSlotCount);
        }

        private void CaptureSlots(InventorySave save, ItemType slotType)
        {
            int slotCount = m_slotStorage.GetMaxSlotCount(slotType);

            for (int i = 0; i < slotCount; i++)
            {
                if (!m_slotStorage.GetHandle(slotType, i, out EntityHandle handle))
                {
                    continue;
                }

                Entity entity = m_entityManager.Get(handle);

                if (entity == null)
                {
                    LogWarning($"CaptureSlots skipped. Entity missing. slotType: {slotType}, slotIndex: {i}, handle: {handle}");
                    continue;
                }

                if (!InventoryItemRules.GetItemType(entity, out ItemType itemType))
                {
                    LogWarning($"CaptureSlots skipped. Unsupported entity type: {entity.GetType().Name}");
                    continue;
                }

                // 런타임 상태가 잘못되었을 때 수량 보정 방지
                if (InventoryItemRules.GetStackQuantity(entity, out int quantity) && quantity <= 0)
                {
                    LogWarning($"CaptureSlots skipped. Stack quantity must be greater than zero. slotType: {slotType}, slotIndex: {i}, dataId: {entity.DataId}, quantity: {quantity}");
                    continue;
                }

                InventorySave.SlotSave slotSave = CreateSlotSave(itemType, i, handle, entity);
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
                m_entityManager.Destroy(restoredHandle);

                LogWarning($"RestoreSlot failed. No empty slot. slotType: {slotType}, itemType: {slotSave.itemType}, dataId: {slotSave.dataId}");
                return false;
            }

            m_slotStorage.SetHandle(slotType, targetIndex, restoredHandle);

            LogDebug($"RestoreSlot success. slotType: {slotType}, itemType: {slotSave.itemType}, slotIndex: {targetIndex}, dataId: {slotSave.dataId}, handle: {restoredHandle}");
            return true;
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

            if (m_dataManager.GetData<ItemData_Fish>(slotSave.dataId) == null)
            {
                LogWarning($"TryRestoreFish failed. Data not found. dataId: {slotSave.dataId}");
                return false;
            }

            restoredHandle = m_entityManager.Restore<ItemData_Fish>(parsedHandle, slotSave.dataId);

            if (m_entityManager.Get(restoredHandle) is Entity_Fish fish)
            {
                fish.SetRollResult(Math.Max(0f, slotSave.size), NormalizeQuality(slotSave.quality));
                return true;
            }

            return false;
        }

        private bool RestoreEquipment(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
        {
            restoredHandle = default;

            if (m_dataManager.GetData<ItemData_Equipment>(slotSave.dataId) == null)
            {
                LogWarning($"TryRestoreEquipment failed. Data not found. dataId: {slotSave.dataId}");
                return false;
            }

            restoredHandle = m_entityManager.Restore<ItemData_Equipment>(parsedHandle, slotSave.dataId);

            if (m_entityManager.Get(restoredHandle) is Entity_Equipment equipment)
            {
                equipment.SetUpgradeLevel(Math.Max(0, slotSave.upgradeLevel));
                return true;
            }

            return false;
        }

        private bool RestoreMaterials(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
        {
            restoredHandle = default;

            if (m_dataManager.GetData<ItemData_Materials>(slotSave.dataId) == null)
            {
                LogWarning($"TryRestoreMaterials failed. Data not found. dataId: {slotSave.dataId}");
                return false;
            }

            if (slotSave.quantity <= 0)
            {
                LogWarning($"TryRestoreMaterials failed. Quantity must be greater than zero. dataId: {slotSave.dataId}, quantity: {slotSave.quantity}");
                return false;
            }

            restoredHandle = m_entityManager.Restore<ItemData_Materials>(parsedHandle, slotSave.dataId);

            if (m_entityManager.Get(restoredHandle) is Entity_Materials materials)
            {
                materials.SetQuantity(slotSave.quantity);
                return true;
            }

            return false;
        }

        private bool RestoreConsumables(InventorySave.SlotSave slotSave, EntityHandle parsedHandle, out EntityHandle restoredHandle)
        {
            restoredHandle = default;

            if (m_dataManager.GetData<ItemData_Consumables>(slotSave.dataId) == null)
            {
                LogWarning($"TryRestoreConsumables failed. Data not found. dataId: {slotSave.dataId}");
                return false;
            }

            if (slotSave.quantity <= 0)
            {
                LogWarning($"TryRestoreConsumables failed. Quantity must be greater than zero. dataId: {slotSave.dataId}, quantity: {slotSave.quantity}");
                return false;
            }

            restoredHandle = m_entityManager.Restore<ItemData_Consumables>(parsedHandle, slotSave.dataId);

            if (m_entityManager.Get(restoredHandle) is Entity_Consumables consumables)
            {
                consumables.SetQuantity(slotSave.quantity);
                return true;
            }

            return false;
        }

        private static ItemQuality NormalizeQuality(ItemQuality quality)
        {
            if (Enum.IsDefined(typeof(ItemQuality), quality))
            {
                return quality;
            }

            return ItemQuality.OneStar;
        }

        private static ItemRarity NormalizeRarity(ItemRarity rarity)
        {
            if (Enum.IsDefined(typeof(ItemRarity), rarity))
            {
                return rarity;
            }

            return ItemRarity.Normal;
        }

        private static InventoryAutoSellFilterState GetAutoSellFilterState(InventorySave save)
        {
            if (save == null)
            {
                return new InventoryAutoSellFilterState(false, ItemQuality.OneStar, ItemRarity.Normal);
            }

            return new InventoryAutoSellFilterState(
                save.autoSellEnabled,
                NormalizeQuality(save.maxAutoSellQuality),
                NormalizeRarity(save.maxAutoSellRarity));
        }

        private void LogDebug(string message)
        {
            m_logDebug?.Invoke(message);
        }

        private void LogWarning(string message)
        {
            m_logWarning?.Invoke(message);
        }
    }
}