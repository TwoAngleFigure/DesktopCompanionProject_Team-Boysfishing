using System;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class MixtureViewModel : UIViewModelBase
    {
        private MixtureSystem m_mixtureSystem;
        private InventorySystem m_inventorySystem;

        public event Action OnInventoryUpdated;

        public RelayCommand<MixtureRecipeSO> CraftCommand { get; private set; }

        public override void Bind()
        {
            m_mixtureSystem = SystemManager.GetSystem<MixtureSystem>();
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();

            CraftCommand = new RelayCommand<MixtureRecipeSO>(recipe =>
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

        public bool CanCraft(MixtureRecipeSO recipe)
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
    }
}