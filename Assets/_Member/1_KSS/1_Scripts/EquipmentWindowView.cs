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
                        EntityHandle equippedItem = m_vm.GetEquippedHandleForArea(clickedArea);

                        if (m_itemPickupController == null) return;

                        if (!m_itemPickupController.HasItem)
                        {
                            // 1. 선택 없음 + 장착된 장비 슬롯 클릭 -> 장비 선택 (실제 장비는 해제되지 않음)
                            if (!equippedItem.Equals(default(EntityHandle)))
                            {
                                Sprite icon = null;
                                Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(equippedItem);
                                if (equipment != null && equipment.ItemData != null && !string.IsNullOrEmpty(equipment.ItemData.AssetKey))
                                {
                                    AssetProvider.TryGet<Sprite>(equipment.ItemData.AssetKey, out icon);
                                }
                                m_itemPickupController.BeginEquipmentPickup(clickedArea, equippedItem, icon);
                            }
                        }
                        else
                        {
                            if (m_itemPickupController.Source == ItemPickupSource.Equipment)
                            {
                                // 장비창 장비 선택 중
                                if (m_itemPickupController.SourceEquipmentArea == clickedArea)
                                {
                                    // 2. 같은 슬롯 클릭 -> 선택 취소
                                    m_itemPickupController.ClearPickup();
                                }
                                // 3. 다른 장비 슬롯 클릭 -> 무반응
                            }
                            else if (m_itemPickupController.Source == ItemPickupSource.Inventory)
                            {
                                // 인벤토리 장비 선택 중
                                EntityHandle pickedItem = m_itemPickupController.PickedHandle;
                                Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(pickedItem);

                                if (equipment != null)
                                {
                                    EquipmentMountingArea itemArea = equipment.ItemData.MountingArea;

                                    if (itemArea == clickedArea)
                                    {
                                        // 4. 같은 장착 부위 슬롯 클릭 -> EquipCommand 실행, 선택 해제
                                        m_vm.EquipCommand.Execute((clickedArea, pickedItem));
                                        m_itemPickupController.ClearPickup();
                                    }
                                    // 5. 다른 장착 부위 슬롯 클릭 -> 무반응, 선택 유지
                                }
                            }
                        }
                    },

                    // 2. 드롭: 장착 (규격 검사 추가!)
                    onDropAction: dropArea =>
                    {
                        if (m_itemPickupController != null && m_itemPickupController.HasItem)
                        {
                            EntityHandle droppedItem = m_itemPickupController.PickedHandle;
                            Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(droppedItem);

                            if (equipment != null)
                            {
                                // ⚠️ 주의: 변수명(MountingArea)이 에러나면 ItemData_Equipment의 실제 변수명으로 수정하세요!
                                EquipmentMountingArea itemArea = equipment.ItemData.MountingArea;

                                if (itemArea == dropArea)
                                {
                                    m_vm.EquipCommand.Execute((dropArea, droppedItem));
                                    m_itemPickupController.ClearPickup();
                                }
                                else
                                {
                                    Debug.LogWarning($"장착 불가: {itemArea} 아이템을 {dropArea} 칸에 장착할 수 없습니다.");
                                    
                                    // 원래 위치로 되돌리기 (취소 처리)
                                    if (m_itemPickupController.Source == ItemPickupSource.Equipment)
                                    {
                                        m_vm.EquipCommand.Execute((m_itemPickupController.SourceEquipmentArea, droppedItem));
                                    }
                                    m_itemPickupController.ClearPickup();
                                }
                            }
                        }
                    },

                    // 3. 드래그 시작: 장착 해제 및 아이템 들기 (안전 검사 추가)
                    onBeginDragAction: dragArea =>
                    {
                        EntityHandle equippedItem = m_vm.GetEquippedHandleForArea(dragArea);

                        if (!equippedItem.Equals(default(EntityHandle)) && m_itemPickupController != null && !m_itemPickupController.HasItem)
                        {
                            Sprite icon = null;
                            Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(equippedItem);
                            if (equipment != null && equipment.ItemData != null && !string.IsNullOrEmpty(equipment.ItemData.AssetKey))
                            {
                                AssetProvider.TryGet<Sprite>(equipment.ItemData.AssetKey, out icon);
                            }

                            // 장비 전용 픽업 메서드 호출 (아이콘 포함)
                            m_itemPickupController.BeginEquipmentPickup(dragArea, equippedItem, icon);
                            
                            // 빈 칸으로 만들기(장비 해제)
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

