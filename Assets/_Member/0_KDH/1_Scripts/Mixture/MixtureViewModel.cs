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

        public event Action OnInventoryUpdated;

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
        }

        public override void Unbind()
        {
            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
            }

            m_mixtureSystem = null;
            m_inventorySystem = null;
        }

        private void HandleInventoryChanged()
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

        public string GetRecipeName(RecipeData_Mixture recipe)
        {
            if (m_mixtureSystem == null || recipe == null) return "알 수 없음";

            string typeStr = recipe.m_resultType.Replace("ItemData_", "");
            if (Enum.TryParse(typeStr, out ItemType type))
            {
                return m_mixtureSystem.GetItemName(type, recipe.m_resultId);
            }
            return "알 수 없음";
        }
    }
}