using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using DesktopCompanion.Views;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class EquipmentWindowView : UIWindowBase
    {
        private readonly EquipmentViewModel m_vm = new();

        [Header("UI 연결")]
        [SerializeField] private TextMeshProUGUI m_allStatsText;
        [SerializeField] private EquipmentSlotWidget[] m_slots;

        // [추가됨] 배 장비창 텍스트 2개를 연결할 빈칸!
        [Header("Ship Equip UI")]
        [SerializeField] private TextMeshProUGUI m_engineSpeedText;
        [SerializeField] private TextMeshProUGUI m_storageSizeText;
        [SerializeField] private Button m_storageUpgradeBtn;//테스트용

        [Header("Inventory Link")]
        [SerializeField] private ItemPickupController m_itemPickupController;

        [Header("Tab Buttons")]
        [SerializeField] private Button m_tabPlayerEquipBtn;
        [SerializeField] private Button m_tabShipEquipBtn;
        [SerializeField] private Button m_tabStatsBtn;

        [Header("Tab Panels")]
        [SerializeField] private GameObject m_playerEquipPanel;
        [SerializeField] private GameObject m_shipEquipPanel;
        [SerializeField] private GameObject m_statsPanel;

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.OnEquipmentChanged += RefreshUI;

            foreach (var slot in m_slots)
            {
                slot.Bind(
                    onClickAction: clickedArea =>
                    {
                        m_vm.EquipCommand.Execute((clickedArea, default(EntityHandle)));
                    },
                    onDropAction: dropArea =>
                    {
                        if (m_itemPickupController != null && m_itemPickupController.HasItem)
                        {
                            EntityHandle droppedItem = m_itemPickupController.PickedHandle;

                            // 1. EntityHandle을 통해 실제 장비 엔티티를 가져옵니다.
                            Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(droppedItem);

                            if (equipment != null)
                            {
                                // 2. [중요] 해당 장비의 '장착 부위(Area)' 정보를 가져옵니다.
                                // *주의: 여기서 'ItemData.MountingArea' 부분은 실제 ItemData_Equipment 스크립트에
                                // 정의된 변수명(예: EquipArea, MountingArea 등)으로 바꿔야 합니다!
                                EquipmentMountingArea itemArea = equipment.ItemData.MountingArea;

                                // 3. 아이템의 부위(itemArea)와 슬롯의 부위(dropArea)가 일치하는지 확인!
                                if (itemArea == dropArea)
                                {
                                    m_vm.EquipCommand.Execute((dropArea, droppedItem));
                                    m_itemPickupController.ClearPickup();
                                }
                                else
                                {
                                    Debug.LogWarning($"❌ 장착 불가: 이 아이템은 {itemArea}용입니다. ({dropArea}에는 장착 불가)");
                                }
                            }
                        }
                    },
                    onBeginDragAction: dragArea =>
                    {
                        EntityHandle equippedItem = m_vm.GetEquippedHandleForArea(dragArea);

                        if (!equippedItem.Equals(default(EntityHandle)) && m_itemPickupController != null)
                        {
                            m_itemPickupController.BeginPickup(ItemType.Equipment, -1, equippedItem, null);
                            m_vm.EquipCommand.Execute((dragArea, default(EntityHandle)));
                        }
                    }
                );
            }

            if (m_tabPlayerEquipBtn != null) m_tabPlayerEquipBtn.onClick.AddListener(() => SwitchTab(0));
            if (m_tabShipEquipBtn != null) m_tabShipEquipBtn.onClick.AddListener(() => SwitchTab(1));
            if (m_tabStatsBtn != null) m_tabStatsBtn.onClick.AddListener(() => SwitchTab(2));
            if (m_storageUpgradeBtn != null) m_storageUpgradeBtn.onClick.AddListener(() => SystemManager.GetSystem<PlayerSystem>().UpgradeFishStorage());
            RefreshUI();
            SwitchTab(0);
        }

        public override void Unbind()
        {
            m_vm.OnEquipmentChanged -= RefreshUI;
            m_vm.Unbind();

            foreach (var slot in m_slots)
            {
                slot.Unbind();
            }

            if (m_tabPlayerEquipBtn != null) m_tabPlayerEquipBtn.onClick.RemoveAllListeners();
            if (m_tabShipEquipBtn != null) m_tabShipEquipBtn.onClick.RemoveAllListeners();
            if (m_tabStatsBtn != null) m_tabStatsBtn.onClick.RemoveAllListeners();
        }

        private void RefreshUI()
        {
            foreach (var slot in m_slots)
            {
                string itemName = m_vm.GetItemNameForArea(slot.Area);
                slot.RefreshSlotUI(itemName);
            }

            if (m_allStatsText != null)
            {
                m_allStatsText.text = m_vm.GetAllStatsFormattedText();
            }

            // [추가됨] 스탯창이 갱신될 때, 배 장비창의 텍스트도 자동으로 최신 스탯을 받아옵니다!
            if (m_engineSpeedText != null)
            {
                m_engineSpeedText.text = m_vm.GetEngineSpeedText();
            }
            if (m_storageSizeText != null)
            {
                m_storageSizeText.text = m_vm.GetStorageSizeText();
            }
        }

        private void SwitchTab(int tabIndex)
        {
            if (m_playerEquipPanel != null) m_playerEquipPanel.SetActive(tabIndex == 0);
            if (m_shipEquipPanel != null) m_shipEquipPanel.SetActive(tabIndex == 1);
            if (m_statsPanel != null) m_statsPanel.SetActive(tabIndex == 2);
        }
    }
}

