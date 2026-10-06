using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Systems
{
    public class MixtureSystem : SystemBase
    {
        // 결과 타입이 이 값이면 m_resultId는 아이템이 아니라 RandomTableData의 ID다.
        private const string RandomDropTypeName = "RandomDrop";

        private InventorySystem m_inventorySystem;
        private RandomTableSystem m_randomTableSystem;

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
            m_randomTableSystem = SystemManager.GetSystem<RandomTableSystem>();

            if (m_inventorySystem == null)
            {
                Debug.LogError("[MixtureSystem] InventorySystem을 찾을 수 없습니다! GameManager 등록 순서를 확인하세요.");
            }
            if (m_randomTableSystem == null)
            {
                Debug.LogError("[MixtureSystem] RandomTableSystem을 찾을 수 없습니다! RandomDrop 레시피가 동작하지 않습니다.");
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

            // 결과를 먼저 확정한다. 소모한 뒤에 실패하면 재료만 사라진다.
            if (!TryResolveResult(recipe, out ItemType resultItemType, out int resultDataId))
            {
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

            return GrantReward(resultItemType, resultDataId, recipe.m_resultCount);
        }

        /// <summary>
        /// 레시피의 결과물을 구체적인 (종류, ID)로 확정한다.
        /// RandomDrop은 이 시점에 티어에서 후보를 뽑아 하나로 정한다.
        /// 소모 전에 호출해야 실패했을 때 재료가 보존된다.
        /// </summary>
        private bool TryResolveResult(RecipeData_Mixture recipe, out ItemType itemType, out int dataId)
        {
            itemType = default;
            dataId = 0;

            string typeName = recipe.m_resultType != null
                ? recipe.m_resultType.Replace("ItemData_", "")
                : string.Empty;

            // RandomDrop은 ItemType에 없는 값이다. 결과 ID가 아이템이 아니라 테이블을 가리키므로
            // 여기서 굴려 하나로 확정한다. 어떤 후보가 있는지는 전적으로 시트가 정한다.
            if (typeName == RandomDropTypeName)
            {
                if (m_randomTableSystem == null)
                {
                    Debug.LogError("[MixtureSystem] RandomTableSystem이 없어 랜덤 결과를 정할 수 없습니다.");
                    return false;
                }
                return m_randomTableSystem.TryRoll(recipe.m_resultId, out itemType, out dataId);
            }

            if (Enum.TryParse(typeName, out itemType))
            {
                dataId = recipe.m_resultId;
                return true;
            }

            Debug.LogError($"[MixtureSystem] 알 수 없는 결과물 타입: {recipe.m_resultType} (레시피 {recipe.ID})");
            return false;
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

        /// <summary>
        /// 종류·ID로 아이템 정의를 찾는다. 표시 계층이 아이콘·티어·툴팁 상세를 만들 때 쓴다.
        /// </summary>
        public ItemData GetItemData(ItemType itemType, int dataId)
        {
            var dataManager = this.DataManager;

            if (dataManager == null) return null;

            switch (itemType)
            {
                case ItemType.Materials: return dataManager.GetData<ItemData_Materials>(dataId);
                case ItemType.Consumables: return dataManager.GetData<ItemData_Consumables>(dataId);
                case ItemType.Equipment: return dataManager.GetData<ItemData_Equipment>(dataId);
                case ItemType.Fish: return dataManager.GetData<ItemData_Fish>(dataId);
                default: return null;
            }
        }

        /// <summary>
        /// 레시피 결과물의 표시용 정의. 확정 지급이면 아이템 정의를, RandomDrop이면 테이블 정의를 준다.
        /// 둘 다 GameData라 이름·AssetKey를 같은 방식으로 읽을 수 있다.
        ///
        /// 표시 계층이 GetItemData(itemType, m_resultId)를 직접 부르면 RandomDrop에서 null이 된다 —
        /// 그 ID는 아이템이 아니라 테이블을 가리키기 때문이다. 이 경로로 두 경우를 함께 해소한다.
        /// </summary>
        public GameData GetResultDefinition(RecipeData_Mixture recipe)
        {
            if (recipe == null)
            {
                return null;
            }

            string typeName = recipe.m_resultType != null
                ? recipe.m_resultType.Replace("ItemData_", "")
                : string.Empty;

            if (typeName == RandomDropTypeName)
            {
                return m_randomTableSystem != null ? m_randomTableSystem.GetTable(recipe.m_resultId) : null;
            }

            return Enum.TryParse(typeName, out ItemType itemType)
                ? GetItemData(itemType, recipe.m_resultId)
                : null;
        }

        public string GetItemName(ItemType itemType, int dataId)
        {
            ItemData itemData = GetItemData(itemType, dataId);

            return itemData != null ? itemData.Name : "알 수 없음";
        }

        public List<RecipeData_Mixture> GetAllRecipes()
        {
            return new List<RecipeData_Mixture>(m_recipeDatabase.Values);
        }

        /// <summary>
        /// 그 아이템을 만들어 내는 레시피. 같은 결과물의 레시피가 여럿이면 먼저 찾은 하나를 준다.
        /// 제작 비용의 출처는 아이템 정의가 아니라 이 레시피다 — 표시 계층이 비용을 물어볼 때 쓴다.
        /// </summary>
        public RecipeData_Mixture GetRecipeByResult(ItemType itemType, int dataId)
        {
            foreach (RecipeData_Mixture recipe in m_recipeDatabase.Values)
            {
                if (recipe == null || recipe.m_resultId != dataId) continue;

                string typeString = recipe.m_resultType != null
                    ? recipe.m_resultType.Replace("ItemData_", "")
                    : string.Empty;

                if (Enum.TryParse(typeString, out ItemType resultType) && resultType == itemType)
                {
                    return recipe;
                }
            }
            return null;
        }
    }

    public struct ParsedIngredient
    {
        public ItemType itemType;
        public int dataId;
        public int amount;
    }
}