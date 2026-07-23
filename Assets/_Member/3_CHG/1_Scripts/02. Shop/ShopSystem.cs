using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public class ShopSystem : SystemBase
    {
        private InventorySystem m_inventorySystem;
        private PlayerSystem m_playerSystem;
        private CurrencySystem m_currencySystem;

        private IReadOnlyList<ItemData> m_products;

        public IReadOnlyList<ItemData> Products => m_products;

        //현재 기획 미흡으로 인해 구매 가격 = baseprice * 3으로 지정. 기획 후 수정
        private float m_priceMultiplier = 3.0f;

        public override void PostInitialize()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
            m_playerSystem = SystemManager.GetSystem<PlayerSystem>();
            m_currencySystem = SystemManager.GetSystem<CurrencySystem>();

            if (m_inventorySystem == null)
            {
                Debug.LogError("[ShopSystem] InventorySystem not found.");
            }

            if (m_playerSystem == null)
            {
                Debug.LogError("[ShopSystem] PlayerSystem not found.");
            }

            if (m_currencySystem == null)
            {
                Debug.LogError("[ShopSystem] CurrencySystem not found.");
            }

            //현재 기획 상 상점 판매 품목은 소모품으로 한정. 기획 후 수정
            m_products = DataManager.GetAll<ItemData_Consumables>();
        }

        #region sell

        public int CalculateSellGold(EntityHandle handle, int amount)
        {
            Entity entity = EntityManager.Get(handle);
            long basePrice = CalculateBaseSellPrice(entity, amount);

            return ApplyGoldMultiplier(basePrice);
        }

        public int CalculateSellGold(IReadOnlyCollection<ItemQuantity> items)
        {
            return TryCalculateSellGold(items, out int sellGold) ? sellGold : 0;
        }

        public bool SellItems(IReadOnlyCollection<ItemQuantity> items, out int earnedGold)
        {
            earnedGold = 0;

            if (m_inventorySystem == null || m_currencySystem == null)
            {
                return false;
            }

            if (!TryCalculateSellGold(items, out int sellGold))
            {
                return false;
            }

            if (!m_currencySystem.AddGold(sellGold))
            {
                return false;
            }

            if (!m_inventorySystem.RemoveByHandles(items, false))
            {
                if (!m_currencySystem.AddGold(-sellGold))
                {
                    Debug.LogError("[ShopSystem] Failed to roll back gold after inventory removal failure.");
                }

                return false;
            }

            m_inventorySystem.RequestSave();

            earnedGold = sellGold;
            return true;
        }

        public bool SellAcquiredItem(EntityHandle handle, out int earnedGold)
        {
            earnedGold = 0;

            //자동 판매는 현재 기획 상 물고기 아이템에 한정
            if (EntityManager.Get(handle) is not Entity_Fish fish)
            {
                return false;
            }

            earnedGold = ApplyGoldMultiplier(CalculateBaseSellPrice(fish, 1));

            if (earnedGold <= 0 || m_currencySystem == null || !m_currencySystem.AddGold(earnedGold))
            {
                earnedGold = 0;
                return false;
            }

            EntityManager.Destroy(handle);
            return true;
        }

        private bool TryCalculateSellGold(IReadOnlyCollection<ItemQuantity> items, out int sellGold)
        {
            sellGold = 0;

            if (m_inventorySystem == null || items == null || items.Count == 0)
            {
                return false;
            }

            HashSet<EntityHandle> uniqueHandles = new();
            long totalBasePrice = 0;

            foreach (ItemQuantity item in items)
            {
                if (!uniqueHandles.Add(item.Handle) || !m_inventorySystem.CanRemoveByHandle(item.Handle, item.Amount))
                {
                    return false;
                }

                Entity entity = EntityManager.Get(item.Handle);
                long basePrice = CalculateBaseSellPrice(entity, item.Amount);

                if (basePrice <= 0)
                {
                    return false;
                }

                totalBasePrice += basePrice;
            }

            sellGold = ApplyGoldMultiplier(totalBasePrice);
            return sellGold > 0;
        }

        private long CalculateBaseSellPrice(Entity entity, int amount)
        {
            if (!IsValidSellAmount(entity, amount))
            {
                return 0;
            }

            int unitPrice = GetUnitSellPrice(entity);

            if (unitPrice <= 0)
            {
                return 0;
            }

            return (long)unitPrice * amount;
        }

        private int GetUnitSellPrice(Entity entity)
        {
            if (entity is Entity_Fish fish)
            {
                return fish.ItemData?.BasePrice ?? 0;
            }

            if (entity is Entity_Equipment equipment)
            {
                return equipment.ItemData?.BasePrice ?? 0;
            }

            if (entity is Entity_Materials materials)
            {
                return materials.ItemData?.BasePrice ?? 0;
            }

            if (entity is Entity_Consumables consumables)
            {
                return consumables.ItemData?.BasePrice ?? 0;
            }

            return 0;
        }

        private bool IsValidSellAmount(Entity entity, int amount)
        {
            if (entity == null || amount <= 0)
            {
                return false;
            }

            if (entity is Entity_Materials materials)
            {
                return amount <= materials.Quantity;
            }

            if (entity is Entity_Consumables consumables)
            {
                return amount <= consumables.Quantity;
            }

            return amount == 1 && (entity is Entity_Fish || entity is Entity_Equipment);
        }

        private int ApplyGoldMultiplier(long basePrice)
        {
            if (basePrice <= 0 || m_playerSystem == null)
            {
                return 0;
            }

            double finalGold = Math.Floor(basePrice * (double)m_playerSystem.BaseGoldGettingMultiply);

            if (finalGold <= 0d)
            {
                return 0;
            }

            return finalGold >= int.MaxValue
                ? int.MaxValue
                : (int)finalGold;
        }

        #endregion

        /// <summary> 현재 플레이어가 구매할 수 있는 아이템을 반환 </summary>
        public IReadOnlyList<ItemData> GetBuyableItems()
        {
            Entity_Player playerEntity = (Entity_Player)EntityManager.Get(m_playerSystem.PlayerHandle);
            int license = playerEntity.CurrentLicense;
            List<ItemData> buyableItem = new List<ItemData>();
            
            foreach(ItemData item in m_products)
            {
                if (item.Tier <= license)
                    buyableItem.Add(item);
            }

            return buyableItem;
        }

        /// <summary> 아이템을 구매하는 함수. ItemType과 DataId 검증 확실히 할 것. </summary>
        public bool BuyItem(ItemType type, int dataId, int amount)
        {
            #region Validation
            if (!IsValidDataId(type, dataId, out int price))
                return false;

            if (amount <= 0 || (type == ItemType.Equipment && amount > 1))
            {
                Debug.LogWarning($"[ShopSystem] 구매하려는 품목의 수량이 올바르지 않습니다. amount : {amount}");
                return false;
            }

            if ((type == ItemType.Materials || type == ItemType.Consumables) && m_inventorySystem.GetTotalQuantityByDataId(type, dataId) <= 0 && !m_inventorySystem.HasEmptySlot(type) ||
                (type == ItemType.Equipment && !m_inventorySystem.HasEmptySlot(type)))
            {
                Debug.LogWarning($"[ShopSystem] 구매하려는 품목이 인벤토리에 들어갈 자리가 없습니다.");
                return false;
            }

            int totalPrice = CalculateItemPrice(price) * amount;

            if(totalPrice <= 0)
            {
                Debug.LogWarning("[ShopSystem] 가격이 0 이하인 품목은 존재하지 않습니다.");
                return false;
            }

            if(m_currencySystem.CurrentGold < totalPrice)
            {
                Debug.LogWarning($"[ShopSystem] 구매하려는 품목의 가격이 현재 소지한 골드보다 높습니다. 현재 소지한 골드 : {m_currencySystem.CurrentGold}, 가격 : {totalPrice}");
                return false;
            }
            #endregion
            EntityHandle itemHandle;
            switch (type)
            {
                case ItemType.Materials:
                    itemHandle = EntityManager.Create<ItemData_Materials>(dataId);
                    Entity_Materials entity_Materials = EntityManager.Get<Entity_Materials>(itemHandle);
                    entity_Materials.SetQuantity(amount);
                    break;
                case ItemType.Equipment:
                    itemHandle = EntityManager.Create<ItemData_Equipment>(dataId); break;
                case ItemType.Consumables:
                    itemHandle = EntityManager.Create<ItemData_Consumables>(dataId);
                    Entity_Consumables entity_Consumables = EntityManager.Get<Entity_Consumables>(itemHandle);
                    entity_Consumables.SetQuantity(amount);
                    break;
                default:
                    return false;
            }

            if (!m_currencySystem.AddGold(-totalPrice))
            {
                EntityManager.Destroy(itemHandle);
                return false;
            }

            if (!m_inventorySystem.AddItem(itemHandle)) 
            { 
                EntityManager.Destroy(itemHandle);
                if (!m_currencySystem.AddGold(totalPrice))
                    Debug.LogError($"[ShopSystem] 환불에 실패했습니다. : {totalPrice}");
                return false;
            }
            return true;
        }

        //현재 기획 미흡으로 인해 구매 가격 = 판매 가격 x n으로 구현. 기획 후 수정
        public int CalculateItemPrice(int basePrice)
        {
            float price = basePrice * m_priceMultiplier;

            return (int)price;
        }
        
        private bool IsValidDataId(ItemType type, int dataId, out int price)
        {
            foreach(ItemData item in m_products)
            {
                if (item.ID == dataId && item.Type == type)
                {
                    price = item.BasePrice;
                    return true;
                }
            }

            Debug.LogWarning($"[ShopSystem] 아이템이 검증에 실패했습니다.\nID : {dataId} | ID_Type : {type}");
            price = 0;
            return false;
        }

    }
}