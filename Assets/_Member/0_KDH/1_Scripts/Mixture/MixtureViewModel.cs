using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;

namespace DesktopCompanion.Views
{
    public class MixtureViewModel : UIViewModelBase
    {
        private MixtureSystem m_mixtureSystem;
        private InventorySystem m_inventorySystem;
        private CurrencySystem m_currencySystem;

        /// <summary>표시를 다시 그려야 하는 변화(재료 증감·골드 증감)를 알린다.</summary>
        public event Action OnInventoryUpdated;

        /// <summary>보유 골드. 제작 비용과 비교해 표시색·버튼 활성을 정하는 데 쓴다.</summary>
        public int CurrentGold => m_currencySystem != null ? m_currencySystem.CurrentGold : 0;

        public RelayCommand<RecipeData_Mixture> CraftCommand { get; private set; }
        public override void Bind()
        {
            m_mixtureSystem = SystemManager.GetSystem<MixtureSystem>();
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();

            CraftCommand = new RelayCommand<RecipeData_Mixture>(recipe =>
            {
                if (CanCraft(recipe))
                {
                    m_mixtureSystem?.Craft(recipe);
                }
            });

            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged += HandleInventoryChanged;
            }

            // 제작 비용에 골드가 있으므로 판매 등으로 골드만 변해도 표시를 다시 그려야 한다.
            m_currencySystem = SystemManager.GetSystem<CurrencySystem>();
            if (m_currencySystem != null)
            {
                m_currencySystem.OnGoldChanged += HandleGoldChanged;
            }
        }

        public override void Unbind()
        {
            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
            }
            if (m_currencySystem != null)
            {
                m_currencySystem.OnGoldChanged -= HandleGoldChanged;
            }

            m_mixtureSystem = null;
            m_inventorySystem = null;
            m_currencySystem = null;
        }

        private void HandleInventoryChanged()
        {
            OnInventoryUpdated?.Invoke();
        }

        private void HandleGoldChanged(int newGold)
        {
            OnInventoryUpdated?.Invoke();
        }

        public bool CanCraft(RecipeData_Mixture recipe)
        {
            if (m_mixtureSystem == null || recipe == null) return false;
            return m_mixtureSystem.CanCraft(recipe);
        }

        public int GetOwnedQuantity(ItemType type, int dataId)
        {
            if (m_inventorySystem == null) return 0;
            return m_inventorySystem.GetTotalQuantityByDataId(type, dataId);
        }

        public string GetItemName(ItemType type, int dataId)
        {
            if (m_mixtureSystem == null) return "알 수 없음";

            return m_mixtureSystem.GetItemName(type, dataId);
        }

        public List<ParsedIngredient> GetParsedIngredients(string ingredientString)
        {
            if (m_mixtureSystem == null) return new List<ParsedIngredient>();
            return m_mixtureSystem.ParseIngredients(ingredientString);
        }

        /// <summary>종류·ID로 아이템 정의를 찾는다. View가 슬롯 표시와 툴팁 상세를 만들 때 쓴다.</summary>
        public ItemData GetItemData(ItemType type, int dataId)
        {
            return m_mixtureSystem != null ? m_mixtureSystem.GetItemData(type, dataId) : null;
        }

        /// <summary>레시피 결과물의 종류·ID를 푼다. 데이터의 타입 문자열은 "ItemData_" 접두를 갖는다.</summary>
        public bool TryGetResult(RecipeData_Mixture recipe, out ItemType type, out int dataId)
        {
            type = default;
            dataId = 0;

            if (recipe == null || string.IsNullOrEmpty(recipe.m_resultType)) return false;

            if (Enum.TryParse(recipe.m_resultType.Replace("ItemData_", ""), out type) == false) return false;

            dataId = recipe.m_resultId;
            return true;
        }

        /// <summary>레시피 결과물의 정의. 종류를 못 풀거나 정의가 없으면 null.</summary>
        public ItemData GetResultData(RecipeData_Mixture recipe)
        {
            return TryGetResult(recipe, out ItemType type, out int dataId) ? GetItemData(type, dataId) : null;
        }

        /// <summary>
        /// 결과물의 표시용 정의. 확정 지급이면 아이템 정의, RandomDrop이면 랜덤 테이블 정의를 준다.
        /// 둘 다 GameData라 이름과 에셋 키를 같은 방식으로 읽는다.
        /// <see cref="GetResultData"/>는 ItemData만 다루므로 랜덤 결과에서 null이다.
        /// </summary>
        public GameData GetResultDefinition(RecipeData_Mixture recipe)
        {
            return m_mixtureSystem != null ? m_mixtureSystem.GetResultDefinition(recipe) : null;
        }

        public string GetRecipeName(RecipeData_Mixture recipe)
        {
            GameData data = GetResultDefinition(recipe);

            return data != null ? data.Name : "알 수 없음";
        }

        /// <summary>
        /// 결과물의 설명 문장. 레시피 자체의 설명을 먼저 보고, 없으면 결과 아이템의 설명을 쓴다.
        /// 랜덤 장비 생산처럼 결과 아이템 정의가 없는 종류는 레시피 쪽에만 문장이 있고,
        /// 보스 소환 미끼처럼 아이템이 어디서 조합되든 같은 설명인 것은 아이템 쪽에만 있다.
        /// 둘 다 비어 있으면 빈 문자열 — 그 경우는 능력치 수치로 표시한다.
        /// </summary>
        public string GetResultDescription(RecipeData_Mixture recipe)
        {
            if (recipe == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(recipe.m_description) == false)
            {
                return recipe.m_description;
            }

            ItemData data = GetResultData(recipe);
            return data != null ? data.Description : string.Empty;
        }

        /// <summary>
        /// 일반 소모품의 장착 효과 목록. 소모품이 아니면 빈 배열이다.
        /// 설명 문장이 있는 결과물은 <see cref="GetResultDescription"/>이 대신 표시되므로 이 목록을 쓰지 않는다.
        /// </summary>
        public StatModifier[] GetResultModifiers(RecipeData_Mixture recipe)
        {
            if (GetResultData(recipe) is not ItemData_Consumables consumable)
            {
                return Array.Empty<StatModifier>();
            }

            return consumable.Modifiers ?? Array.Empty<StatModifier>();
        }

        /// <summary>결과물의 '종류' 칸(미끼·떡밥). 소모품이 아니면 빈 문자열.</summary>
        public string GetResultCategoryText(RecipeData_Mixture recipe)
        {
            return GetResultData(recipe) is ItemData_Consumables consumable
                ? ItemLabels.MountingArea(consumable.MountingArea)
                : string.Empty;
        }
    }
}