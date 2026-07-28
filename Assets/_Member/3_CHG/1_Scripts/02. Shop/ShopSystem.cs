using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DG.Tweening.Core.Easing;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public class ShopSystem : SystemBase
    {
        private InventorySystem m_inventorySystem;
        private PlayerSystem m_playerSystem;
        private CurrencySystem m_currencySystem;

        private IReadOnlyList<ShopProducts> m_products;

        public IReadOnlyList<ShopProducts> Products => m_products;

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
            m_products = DataManager.GetAll<ShopProducts>();
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
        public bool IsBuyableProduct(ShopProducts product)
        {
            Entity_Player playerEntity = (Entity_Player)EntityManager.Get(m_playerSystem.PlayerHandle);
            int license = playerEntity.CurrentLicense;
            if (product.IsSummon)
            {
                if (product.Tier <= license - 1)
                    return true;
                else
                    return false;
            }
            if (product.Tier <= license)
                return true;

            return false;
        }

        /// <summary> 아이템을 구매하는 함수.</summary>
        public bool BuyItem(int productId, int amount)
        {
            #region Validation
            if (!FindProduct(productId, out ShopProducts product))
                return false;

            if (amount <= 0 || (product.ItemType == ItemType.Equipment && amount > 1))
            {
                Debug.LogWarning($"[ShopSystem] 구매하려는 품목의 수량이 올바르지 않습니다. amount : {amount}");
                return false;
            }

            if (!IsBuyableProduct(product))
            {
                Debug.LogWarning($"[ShopSystem] 품목을 구매할 자격이 갖춰지지 않았습니다. 아이템 티어 : {product.Tier}");
                return false;
            }

            if(!FindProductItemData(product, out ItemData item))
            {
                Debug.LogWarning($"[ShopSystem] 구매 검증에 실패했습니다. id : {product.ID} | baseId : {product.BaseId}");
                return false;
            }

            if ((product.ItemType == ItemType.Materials || product.ItemType == ItemType.Consumables) && 
                m_inventorySystem.GetTotalQuantityByDataId(product.ItemType, product.BaseId) <= 0 && !m_inventorySystem.HasEmptySlot(product.ItemType) ||
                (product.ItemType == ItemType.Equipment && !m_inventorySystem.HasEmptySlot(product.ItemType)))
            {
                Debug.LogWarning($"[ShopSystem] 구매하려는 품목이 인벤토리에 들어갈 자리가 없습니다.");
                return false;
            }

            if(product.Price <= 0)
            {
                Debug.LogWarning("[ShopSystem] 가격이 0 이하인 품목은 존재하지 않습니다.");
                return false;
            }

            int totalPrice = product.Price * amount;

            if (m_currencySystem.CurrentGold < totalPrice)
            {
                Debug.LogWarning($"[ShopSystem] 구매하려는 품목의 가격이 현재 소지한 골드보다 높습니다. 현재 소지한 골드 : {m_currencySystem.CurrentGold}, 가격 : {product.Price}");
                return false;
            }
            #endregion

            EntityHandle itemHandle;
            switch (product.ItemType)
            {
                case ItemType.Materials:
                    itemHandle = EntityManager.Create<ItemData_Materials>(product.BaseId);
                    Entity_Materials entity_Materials = EntityManager.Get<Entity_Materials>(itemHandle);
                    entity_Materials.SetQuantity(amount);
                    break;
                case ItemType.Equipment:
                    itemHandle = EntityManager.Create<ItemData_Equipment>(product.BaseId); break;
                case ItemType.Consumables:
                    itemHandle = EntityManager.Create<ItemData_Consumables>(product.BaseId);
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
        
        private bool FindProduct(int productId, out ShopProducts product)
        {
            product = default;
            foreach(ShopProducts shopProduct in m_products)
            {
                if (shopProduct.ID == productId)
                {
                    product = shopProduct;
                    return true;
                }
            }

            Debug.LogWarning($"[ShopSystem] ID로 상품 품목을 찾을 수 없습니다. ID : {productId}");
            return false;
        }

        public bool FindProductItemData(ShopProducts product, out ItemData item)
        {
            ItemType type = product.ItemType;

            switch (type)
            {
                case ItemType.Materials:
                    item = DataManager.GetData<ItemData_Materials>(product.BaseId);
                    break;
                case ItemType.Equipment:
                    item = DataManager.GetData<ItemData_Equipment>(product.BaseId);
                    break;
                case ItemType.Consumables:
                    item = DataManager.GetData<ItemData_Consumables>(product.BaseId);
                    break;
                case ItemType.Fish:
                default:
                    item = default;
                    break;
            }
            if( item == null)
            {
                return false;
            }
            return true;
        }

    }
}