using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Systems
{
    public class MixtureSystem : SystemBase
    {
        private InventorySystem m_inventorySystem;

        private Dictionary<int, RecipeData_Mixture> m_recipeDatabase = new();

        public override void PostInitialize()
        {
            if (this.DataManager != null)
            {
                var recipeList = this.DataManager.GetAll<RecipeData_Mixture>();

                SetRecipeData(new List<RecipeData_Mixture>(recipeList));
            }
            else
            {
                Debug.LogError("[MixtureSystem] DataManager 참조를 찾을 수 없습니다!");
            }
        }

        public override void Initialize()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();

            if (m_inventorySystem == null)
            {
                Debug.LogError("[MixtureSystem] InventorySystem을 찾을 수 없습니다! GameManager 등록 순서를 확인하세요.");
            }
        }

        public void SetRecipeData(List<RecipeData_Mixture> rawDataList)
        {
            m_recipeDatabase.Clear();
            foreach (var data in rawDataList)
            {
                m_recipeDatabase[data.ID] = data;
            }
            Debug.Log($"[MixtureSystem] {m_recipeDatabase.Count}개의 조합 레시피 데이터를 성공적으로 로드했습니다.");
        }

        public List<ParsedIngredient> ParseIngredients(string ingredientString)
        {
            List<ParsedIngredient> parsedList = new List<ParsedIngredient>();

            if (string.IsNullOrEmpty(ingredientString)) return parsedList;

            string[] items = ingredientString.Split(';');

            foreach (string itemStr in items)
            {
                if (string.IsNullOrEmpty(itemStr)) continue;

                string[] details = itemStr.Split(':');
                if (details.Length >= 3)
                {
                    string typeString = details[0].Replace("ItemData_", "");

                    if (Enum.TryParse(typeString, out ItemType itemType))
                    {
                        parsedList.Add(new ParsedIngredient
                        {
                            itemType = itemType,
                            dataId = int.Parse(details[1]),
                            amount = int.Parse(details[2])
                        });
                    }
                    else
                    {
                        Debug.LogError($"[MixtureSystem] 알 수 없는 아이템 타입입니다: {details[0]}");
                    }
                }
            }
            return parsedList;
        }

        public bool CanCraft(RecipeData_Mixture recipe)
        {
            if (recipe == null || m_inventorySystem == null) return false;

            List<ParsedIngredient> ingredients = ParseIngredients(recipe.m_ingredients);

            foreach (var ingredient in ingredients)
            {
                int currentQuantity = m_inventorySystem.GetTotalQuantityByDataId(ingredient.itemType, ingredient.dataId);

                if (currentQuantity < ingredient.amount)
                {
                    return false;
                }
            }
            return true;
        }

        public bool Craft(RecipeData_Mixture recipe)
        {
            if (!CanCraft(recipe))
            {
                Debug.LogWarning("[MixtureSystem] 재료가 부족하여 조합할 수 없습니다.");
                return false;
            }

            List<ParsedIngredient> ingredients = ParseIngredients(recipe.m_ingredients);

            foreach (var ingredient in ingredients)
            {
                bool consumed = m_inventorySystem.ConsumeItemByDataId(ingredient.itemType, ingredient.dataId, ingredient.amount);

                if (!consumed)
                {
                    Debug.LogError($"[MixtureSystem] 치명적 오류: 재료 소모 실패 (ID: {ingredient.dataId})");
                    return false;
                }
            }

            string resultTypeStr = recipe.m_resultType.Replace("ItemData_", "");
            if (Enum.TryParse(resultTypeStr, out ItemType resultItemType))
            {
                return GrantReward(resultItemType, recipe.m_resultId, recipe.m_resultCount);
            }
            else
            {
                Debug.LogError($"[MixtureSystem] 보상 지급 실패. 알 수 없는 결과물 타입: {recipe.m_resultType}");
                return false;
            }
        }

        private bool GrantReward(ItemType itemType, int dataId, int amount)
        {
            EntityHandle newHandle = default;

            switch (itemType)
            {
                case ItemType.Materials:
                    newHandle = EntityManager.Create<ItemData_Materials>(dataId);
                    if (EntityManager.Get(newHandle) is Entity_Materials mat) mat.SetQuantity(amount);
                    break;

                case ItemType.Consumables:
                    newHandle = EntityManager.Create<ItemData_Consumables>(dataId);
                    if (EntityManager.Get(newHandle) is Entity_Consumables con) con.SetQuantity(amount);
                    break;

                case ItemType.Equipment:
                    newHandle = EntityManager.Create<ItemData_Equipment>(dataId);
                    break;

                case ItemType.Fish:
                    newHandle = EntityManager.Create<ItemData_Fish>(dataId);
                    break;

                default:
                    Debug.LogError($"[MixtureSystem] 지원하지 않는 보상 아이템 타입입니다: {itemType}");
                    return false;
            }

            bool added = m_inventorySystem.AddItem(newHandle);

            if (!added)
            {
                Debug.LogError("[MixtureSystem] 인벤토리가 꽉 찼거나 추가에 실패했습니다. 생성된 Entity를 파기합니다.");
                EntityManager.Destroy(newHandle);
                return false;
            }

            Debug.Log($"[MixtureSystem] 확정 보상 지급 완료: {dataId} x {amount}");
            return true;
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

        public List<RecipeData_Mixture> GetAllRecipes()
        {
            return new List<RecipeData_Mixture>(m_recipeDatabase.Values);
        }
    }

    public struct ParsedIngredient
    {
        public ItemType itemType;
        public int dataId;
        public int amount;
    }
}