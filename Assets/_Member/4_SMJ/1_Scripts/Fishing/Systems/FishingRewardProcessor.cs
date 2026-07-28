using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using UnityEngine;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    public enum FishingRewardResult
    {
        Success,
        InventoryFull,
        Failed
    }

    public class FishingRewardProcessor
    {
        private readonly EntityManager m_entityManager;
        private readonly InventorySystem m_inventorySystem;

        public FishingRewardProcessor(
            EntityManager entityManager,
            InventorySystem inventorySystem)
        {
            m_entityManager = entityManager;
            m_inventorySystem = inventorySystem;
        }

        public FishingRewardResult TryCreateCaughtFish(
            ItemData_Fish itemData,
            float size,
            ItemQuality quality,
            out EntityHandle caughtHandle)
        {
            caughtHandle = default;

            if (itemData == null)
            {
                Debug.LogWarning("[FishingRewardProcessor] 포획 물고기 데이터가 없습니다.");
                return FishingRewardResult.Failed;
            }

            EntityHandle handle = m_entityManager.Create<ItemData_Fish>(itemData.ID);
            Entity_Fish fish = m_entityManager.Get<Entity_Fish>(handle);

            if (fish == null)
            {
                m_entityManager.Destroy(handle);
                return FishingRewardResult.Failed;
            }

            fish.SetRollResult(size, quality);

            caughtHandle = handle;
            return FishingRewardResult.Success;
        }

        public FishingRewardResult TryFinalizeCaughtFish(EntityHandle caughtHandle)
        {
            if (IsFishInventoryFull())
            {
                return FishingRewardResult.InventoryFull;
            }

            if (TryAddItem(caughtHandle))
            {
                return FishingRewardResult.Success;
            }

            Debug.LogWarning("[FishingRewardProcessor] 포획 물고기 인벤토리 지급에 실패했습니다.");
            return FishingRewardResult.Failed;
        }

        private bool IsFishInventoryFull()
        {
            return m_inventorySystem.GetUsedSlotCount(ItemType.Fish) >=
                   m_inventorySystem.GetMaxSlotCount(ItemType.Fish);
        }

        public IReadOnlyList<FishingGrantedDropInfo> Process(ItemDrop[] drops, ItemQuality quality)
        {
            var grantedDrops = new List<FishingGrantedDropInfo>();

            if (drops == null || drops.Length == 0)
            {
                return grantedDrops;
            }

            int qualityCount = Mathf.Clamp(
                (int)quality, 
                (int)ItemQuality.OneStar, 
                (int)ItemQuality.FiveStar);

            foreach (ItemDrop drop in drops)
            {
                if (drop == null || drop.Item == null || drop.Count <= 0)
                {
                    Debug.LogWarning(
                        "[FishingRewardProcessor] 유효하지 않은 드롭 데이터입니다.");
                    continue;
                }

                if (!Roll(drop.Probability))
                {
                    continue;
                }

                // 기본 수량 × 성급
                int requestedCount = drop.Count * qualityCount;

                int grantedCount = GrantItem(
                    drop.Item,
                    requestedCount);

                if (grantedCount > 0)
                {
                    grantedDrops.Add(
                        new FishingGrantedDropInfo(
                            drop.Item.Type,
                            drop.Item.ID,
                            grantedCount));
                }

                if (grantedCount == requestedCount)
                {
                    Debug.Log(
                        $"[FishingRewardProcessor] 보상 지급 성공: " +
                        $"item={drop.Item.Name}, " +
                        $"quality={quality}, " +
                        $"count={grantedCount}");
                }
                else
                {
                    Debug.LogWarning(
                        $"[FishingRewardProcessor] 보상 일부/전체 지급 실패: " +
                        $"item={drop.Item.Name}, " +
                        $"quality={quality}, " +
                        $"requested={requestedCount}, " +
                        $"granted={grantedCount}");
                }
        }

                return grantedDrops;
        }

        private bool Roll(float probability)
        {
            float clampedProbability = Mathf.Clamp01(probability);

            if (clampedProbability <= 0f)
            {
                return false;
            }

            if (clampedProbability >= 1f)
            {
                return true;
            }

            return UnityEngine.Random.value < clampedProbability;
        }

        private int GrantItem(ItemData itemData, int count)
        {
            if (itemData is ItemData_Materials materialsData)
            {
                return GrantMaterials(materialsData, count);
            }

            if (itemData is ItemData_Consumables consumablesData)
            {
                return GrantConsumables(consumablesData, count);
            }

            if (itemData is ItemData_Equipment equipmentData)
            {
                return GrantEquipment(equipmentData, count);
            }

            if (itemData is ItemData_Fish fishData)
            {
                return GrantFish(fishData, count);
            }

            Debug.LogWarning(
                $"[FishingRewardProcessor] 지원하지 않는 아이템 타입: {itemData.GetType().Name}");
            return 0;
        }

        private int GrantMaterials(ItemData_Materials itemData, int count)
        {
            EntityHandle handle = m_entityManager.Create<ItemData_Materials>(itemData.ID);
            Entity_Materials entity = m_entityManager.Get<Entity_Materials>(handle);

            if (entity == null)
            {
                m_entityManager.Destroy(handle);
                return 0;
            }

            entity.SetQuantity(count);
            return TryAddItem(handle) ? count : 0;
        }

        private int GrantConsumables(ItemData_Consumables itemData, int count)
        {
            EntityHandle handle = m_entityManager.Create<ItemData_Consumables>(itemData.ID);
            Entity_Consumables entity = m_entityManager.Get<Entity_Consumables>(handle);

            if (entity == null)
            {
                m_entityManager.Destroy(handle);
                return 0;
            }

            entity.SetQuantity(count);
            return TryAddItem(handle) ? count : 0;
        }

        private int GrantEquipment(ItemData_Equipment itemData, int count)
        {
            int grantedCount = 0;

            for (int i = 0; i < count; i++)
            {
                EntityHandle handle = m_entityManager.Create<ItemData_Equipment>(itemData.ID);

                if (m_entityManager.Get<Entity_Equipment>(handle) == null)
                {
                    m_entityManager.Destroy(handle);
                    break;
                }

                if (!TryAddItem(handle))
                {
                    break;
                }

                grantedCount++;
            }

            return grantedCount;
        }

        private int GrantFish(ItemData_Fish itemData, int count)
        {
            int grantedCount = 0;

            for (int i = 0; i < count; i++)
            {
                EntityHandle handle = m_entityManager.Create<ItemData_Fish>(itemData.ID);
                Entity_Fish entity = m_entityManager.Get<Entity_Fish>(handle);

                if (entity == null)
                {
                    m_entityManager.Destroy(handle);
                    break;
                }

                entity.SetRollResult(itemData.Size, itemData.Star);

                if (!TryAddItem(handle))
                {
                    break;
                }

                grantedCount++;
            }

            return grantedCount;
        }

        private bool TryAddItem(EntityHandle handle)
        {
            if (m_inventorySystem.AddItem(handle))
            {
                return true;
            }

            m_entityManager.Destroy(handle);
            return false;
        }
    }
}
