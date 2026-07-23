using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Views
{
    public class ReinforceWindowView : UIWindowBase
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
            if (m_itemPickupController == null)
            {
                m_itemPickupController = GameObject.FindAnyObjectByType<ItemPickupController>();
            }

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

            // [MVVM 바인딩] ViewModel의 상태 변화를 UI에 연결합니다. 
            // .Bind()를 사용하면 구독과 동시에 현재 값이 콜백으로 즉시 전달되어 초기 UI가 제대로 갱신됩니다.
            if (m_statText != null)
            {
                m_viewModel.StatIncreaseText.Bind(UpdateStatText);
            }
            if (m_materialText != null)
            {
                m_viewModel.RequiredMaterialText.Bind(UpdateMaterialText);
            }
            if (m_goldText != null)
            {
                m_viewModel.RequiredGold.Bind(UpdateGoldUI);
                m_viewModel.CurrentGold.Bind(UpdateCurrentGoldUI);
            }
            if (m_reinforceBtn != null)
            {
                m_reinforceBtn.onClick.AddListener(() => m_viewModel.ReinforceCommand.Execute());
            }
            // 마우스 드래그 앤 드롭 외에도, '클릭하여 집기(Click-to-Pickup)' 방식을 지원하기 위해 
            // 강화 슬롯을 클릭했을 때의 이벤트를 추가로 구독합니다.
            if (m_slotWidget != null)
            {
                m_slotWidget.OnSlotDropped += HandleItemDropped;
                m_slotWidget.OnSlotClicked += HandleSlotClicked;
            }
        }

        // 강화 슬롯을 마우스로 클릭했을 때 호출되는 함수
        private void HandleSlotClicked(EntityHandle currentHandle)
        {
            if (m_itemPickupController != null && m_itemPickupController.HasItem)
            {
                // 현재 마우스 포인터에 아이템을 들고 있다면 드롭한 것과 동일하게 처리합니다.
                HandleItemDropped();
            }
            else
            {
                // 들고 있는 아이템이 없는데 슬롯을 클릭했다면, 슬롯에 올라가 있는 장비를 해제합니다.
                if (currentHandle.Value != System.Guid.Empty)
                {
                    m_viewModel.RegisterEquipment(default);
                }
            }
        }

        private void UpdateGoldUI(int requiredGold)
        {
            if (m_viewModel.SelectedEquipment.Value.Value == System.Guid.Empty)
            {
                m_goldText.text = "";
                return;
            }
            
            int current = m_viewModel.CurrentGold.Value;
            string colorHex = current >= requiredGold ? "#00FF00" : "#FF0000";
            m_goldText.text = $"필요 골드 <color={colorHex}>{current}</color> / {requiredGold}";
        }

        private void UpdateCurrentGoldUI(int currentGold)
        {
            UpdateGoldUI(m_viewModel.RequiredGold.Value);
        }

        private void UpdateStatText(string text)
        {
            m_statText.text = text;
        }

        private void UpdateMaterialText(string text)
        {
            m_materialText.text = text;
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

        // 강화창 UI가 닫힐 때 호출되며, 메모리 누수를 막기 위해 구독했던 이벤트들을 모두 해제합니다.
        public override void Unbind()
        {
            if (m_slotWidget != null)
            {
                m_slotWidget.OnSlotDropped -= HandleItemDropped;
                m_slotWidget.OnSlotClicked -= HandleSlotClicked;
            }

            if (m_viewModel != null)
            {
                // .Bind()로 연결한 메서드들은 반드시 .Unbind()로 동일하게 해제해주어야 합니다.
                if (m_statText != null)
                {
                    m_viewModel.StatIncreaseText.Unbind(UpdateStatText);
                }
                if (m_materialText != null)
                {
                    m_viewModel.RequiredMaterialText.Unbind(UpdateMaterialText);
                }
                if (m_goldText != null)
                {
                    m_viewModel.RequiredGold.Unbind(UpdateGoldUI);
                    m_viewModel.CurrentGold.Unbind(UpdateCurrentGoldUI);
                }

                m_viewModel.Unbind();
                m_viewModel = null;
            }
        }
    }
}
