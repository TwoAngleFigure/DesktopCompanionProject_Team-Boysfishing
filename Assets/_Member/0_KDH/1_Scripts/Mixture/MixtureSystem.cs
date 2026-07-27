using System;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Systems
{
    public class MixtureSystem : SystemBase
    {
        private InventorySystem m_inventorySystem;

        public override void PostInitialize()
        {
        }

        public override void Initialize()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();

            if (m_inventorySystem == null)
            {
                Debug.LogError("[MixtureSystem] InventorySystem을 찾을 수 없습니다! GameManager 등록 순서를 확인하세요.");
            }
        }

        public bool CanCraft(MixtureRecipeSO recipe)
        {
            if (recipe == null || m_inventorySystem == null) return false;

            foreach (var ingredient in recipe.ingredients)
            {
                int currentQuantity = m_inventorySystem.GetTotalQuantityByDataId(ingredient.itemType, ingredient.dataId);

                if (currentQuantity < ingredient.amount)
                {
                    return false;
                }
            }
            return true;
        }

        public bool Craft(MixtureRecipeSO recipe)
        {
            if (!CanCraft(recipe))
            {
                Debug.LogWarning("[MixtureSystem] 재료가 부족하여 조합할 수 없습니다.");
                return false;
            }

            foreach (var ingredient in recipe.ingredients)
            {
                bool consumed = m_inventorySystem.ConsumeItemByDataId(ingredient.itemType, ingredient.dataId, ingredient.amount);

                if (!consumed)
                {
                    Debug.LogError($"[MixtureSystem] 치명적 오류: 재료 소모 실패 (ID: {ingredient.dataId})");
                    return false;
                }
            }

            return GrantReward(recipe.result);
        }

        private bool GrantReward(RecipeResult result)
        {
            if (result.resultType == RecipeResultType.Specific)
            {
                EntityHandle newHandle = default;

                switch (result.itemType)
                {
                    case ItemType.Materials:
                        newHandle = EntityManager.Create<ItemData_Materials>(result.dataId);
                        if (EntityManager.Get(newHandle) is Entity_Materials mat) mat.SetQuantity(result.amount);
                        break;

                    case ItemType.Consumables:
                        newHandle = EntityManager.Create<ItemData_Consumables>(result.dataId);
                        if (EntityManager.Get(newHandle) is Entity_Consumables con) con.SetQuantity(result.amount);
                        break;

                    case ItemType.Equipment:
                        newHandle = EntityManager.Create<ItemData_Equipment>(result.dataId);
                        break;

                    case ItemType.Fish:
                        newHandle = EntityManager.Create<ItemData_Fish>(result.dataId);
                        break;

                    default:
                        Debug.LogError($"[MixtureSystem] 지원하지 않는 보상 아이템 타입입니다: {result.itemType}");
                        return false;
                }

                bool added = m_inventorySystem.AddItem(newHandle);

                if (!added)
                {
                    Debug.LogError("[MixtureSystem] 인벤토리가 꽉 찼거나 추가에 실패했습니다. 생성된 Entity를 파기합니다.");
                    EntityManager.Destroy(newHandle);
                    return false;
                }

                Debug.Log($"[MixtureSystem] 확정 보상 지급 완료: {result.dataId} x {result.amount}");
                return true;
            }
            else if (result.resultType == RecipeResultType.RandomDrop)
            {
                Debug.Log("추후 구현");
                return true;
            }

            return false;
        }

        public string GetItemName(ItemType itemType, int dataId)
        {
            var dataManager = this.DataManager;

            if (dataManager == null) return "알 수 없음";

            GameData itemData = null;

            switch (itemType)
            {
                case ItemType.Materials:
                    itemData = dataManager.GetData<ItemData_Materials>(dataId);
                    break;
                case ItemType.Consumables:
                    itemData = dataManager.GetData<ItemData_Consumables>(dataId);
                    break;
                case ItemType.Equipment:
                    itemData = dataManager.GetData<ItemData_Equipment>(dataId);
                    break;
                case ItemType.Fish:
                    itemData = dataManager.GetData<ItemData_Fish>(dataId);
                    break;
            }

            return itemData != null ? itemData.Name : "알 수 없음";
        }
    }
}