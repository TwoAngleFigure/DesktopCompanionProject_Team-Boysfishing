using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Views
{
    public class ReinforceWindowView : UIViewBase
    {
        [SerializeField] private ReinforceSlotWidget m_slotWidget;
        [SerializeField] private TextMeshProUGUI m_statText;
        [SerializeField] private TextMeshProUGUI m_goldText;
        [SerializeField] private TextMeshProUGUI m_materialText;
        [SerializeField] private Button m_reinforceBtn;

        [Header("Inventory Link")]
        [SerializeField] private ItemPickupController m_itemPickupController;

        private ReinforceViewModel m_viewModel;

        public override void Bind()
        {
            m_viewModel = new ReinforceViewModel();
            m_viewModel.Inject(SystemManager, EntityManager);
            m_viewModel.Bind();

            if (m_slotWidget != null)
            {
                m_viewModel.SelectedEquipment.OnChanged += handle => 
                {
                    if (handle.Value == System.Guid.Empty)
                    {
                        m_slotWidget.SetItem(handle, null, 0, "");
                    }
                    else
                    {
                        Entity_Equipment equip = EntityManager.Get<Entity_Equipment>(handle);
                        if (equip != null && equip.ItemData != null)
                        {
                            Sprite icon = null;
                            if (!string.IsNullOrEmpty(equip.ItemData.AssetKey))
                            {
                                AssetProvider.TryGet<Sprite>(equip.ItemData.AssetKey, out icon);
                            }
                            m_slotWidget.SetItem(handle, icon, equip.UpgradeLevel, equip.ItemData.Name);
                        }
                    }
                };
            }

            if (m_statText != null)
            {
                m_viewModel.StatIncreaseText.OnChanged += text => m_statText.text = text;
            }
            if (m_materialText != null)
            {
                m_viewModel.RequiredMaterialText.OnChanged += text => m_materialText.text = text;
            }
            if (m_goldText != null)
            {
                m_viewModel.RequiredGold.OnChanged += UpdateGoldUI;
                m_viewModel.CurrentGold.OnChanged += _ => UpdateGoldUI(m_viewModel.RequiredGold.Value);
            }
            if (m_reinforceBtn != null)
            {
                m_reinforceBtn.onClick.AddListener(() => m_viewModel.ReinforceCommand.Execute());
            }
            if (m_slotWidget != null)
            {
                m_slotWidget.OnSlotDropped += HandleItemDropped;
            }
        }

        private void UpdateGoldUI(int requiredGold)
        {
            int current = m_viewModel.CurrentGold.Value;
            string colorHex = current >= requiredGold ? "#00FF00" : "#FF0000";
            m_goldText.text = $"필요 골드 <color={colorHex}>{current}</color> / {requiredGold}";
        }

        private void HandleItemDropped()
        {
            if (m_itemPickupController != null && m_itemPickupController.HasItem)
            {
                EntityHandle droppedItem = m_itemPickupController.PickedHandle;
                Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(droppedItem);
                
                if (equipment != null)
                {
                    m_viewModel.RegisterEquipment(droppedItem);
                    m_itemPickupController.ClearPickup();
                }
                else
                {
                    Debug.LogWarning("장비 아이템만 강화 슬롯에 올릴 수 있습니다.");
                }
            }
        }

        public override void Unbind()
        {
            if (m_slotWidget != null)
            {
                m_slotWidget.OnSlotDropped -= HandleItemDropped;
            }

            if (m_viewModel != null)
            {
                m_viewModel.Unbind();
                m_viewModel = null;
            }
        }
    }
}
