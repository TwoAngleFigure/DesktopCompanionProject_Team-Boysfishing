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
        [SerializeField] private TextMeshProUGUI m_battleStatsText; // [변경] 전투 스탯
        [SerializeField] private TextMeshProUGUI m_autoBattleStatsText; // [변경] 자동 전투 스탯
        [SerializeField] private TextMeshProUGUI m_rewardStatsText; // [변경] 보상 및 기타 스탯
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
                            if (!equippedItem.Equals(default(EntityHandle)))
                            {
                                if (TryGetEquipmentOrConsumableData(equippedItem, out _, out string assetKey, out _))
                                {
                                    Sprite icon = null;
                                    if (!string.IsNullOrEmpty(assetKey))
                                    {
                                        AssetProvider.TryGet<Sprite>(assetKey, out icon);
                                    }
                                    m_itemPickupController.BeginEquipmentPickup(clickedArea, equippedItem, icon);
                                }
                            }
                        }
                        else
                        {
                            if (m_itemPickupController.Source == ItemPickupSource.Equipment)
                            {
                                // 장비창 장비 선택 중
                                if (m_itemPickupController.SourceEquipmentArea == clickedArea)
                                {
                                    // 2. 같은 슬롯 클릭 -> 장착 해제
                                    m_vm.EquipCommand.Execute((clickedArea, default(EntityHandle)));
                                    m_itemPickupController.ClearPickup();
                                }
                                // 3. 다른 장비 슬롯 클릭 -> 무반응
                            }
                            else if (m_itemPickupController.Source == ItemPickupSource.Inventory)
                            {
                                // 인벤토리 장비 선택 중
                                EntityHandle pickedItem = m_itemPickupController.PickedHandle;
                                if (TryGetEquipmentOrConsumableData(pickedItem, out EquipmentMountingArea itemArea, out _, out _))
                                {
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
                            if (TryGetEquipmentOrConsumableData(droppedItem, out EquipmentMountingArea itemArea, out _, out _))
                            {
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
                            if (TryGetEquipmentOrConsumableData(equippedItem, out _, out string assetKey, out _))
                            {
                                Sprite icon = null;
                                if (!string.IsNullOrEmpty(assetKey))
                                {
                                    AssetProvider.TryGet<Sprite>(assetKey, out icon);
                                }
                                // 장비 전용 픽업 메서드 호출 (아이콘 포함)
                                m_itemPickupController.BeginEquipmentPickup(dragArea, equippedItem, icon);
                            }
                            else
                            {
                                // 알파 실패시에도 픽업은 시도 (아이콘 없이)
                                m_itemPickupController.BeginEquipmentPickup(dragArea, equippedItem, null);
                            }
                            
                            // 빈 칸으로 만들기(장비 해제)
                            m_vm.EquipCommand.Execute((dragArea, default(EntityHandle)));
                        }
                    },

                    // 4. 드래그 종료: 허공이나 인벤토리에 드롭했을 때 잔상 제거
                    onEndDragAction: dragArea =>
                    {
                        if (m_itemPickupController != null && m_itemPickupController.HasItem)
                        {
                            if (m_itemPickupController.Source == ItemPickupSource.Equipment && m_itemPickupController.SourceEquipmentArea == dragArea)
                            {
                                // onBeginDragAction에서 이미 장착 해제(인벤토리로 이동)되었으므로 잔상만 지워줌
                                m_itemPickupController.ClearPickup();
                            }
                        }
                    },

                    // 5. 마우스 오버 시 툴팁 표시
                    onPointerEnterAction: hoverArea =>
                    {
                        ShowTooltip(hoverArea, slot.transform as RectTransform);
                    },

                    // 6. 마우스 아웃 시 툴팁 숨김
                    onPointerExitAction: hoverArea =>
                    {
                        HideTooltip();
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
                EntityHandle equippedItem = m_vm.GetEquippedHandleForArea(slot.Area);
                
                Sprite icon = null;
                int quantity = 0;
                if (!equippedItem.Equals(default(EntityHandle)))
                {
                    if (TryGetEquipmentOrConsumableData(equippedItem, out _, out string assetKey, out quantity))
                    {
                        if (!string.IsNullOrEmpty(assetKey))
                        {
                            AssetProvider.TryGet<Sprite>(assetKey, out icon);
                        }
                    }
                }

                slot.RefreshSlotUI(itemName, icon, quantity);
            }

            if (m_battleStatsText != null)
            {
                m_battleStatsText.text = m_vm.GetBattleStatsFormattedText();
            }
            if (m_autoBattleStatsText != null)
            {
                m_autoBattleStatsText.text = m_vm.GetAutoBattleStatsFormattedText();
            }
            if (m_rewardStatsText != null)
            {
                m_rewardStatsText.text = m_vm.GetRewardStatsFormattedText();
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

        // [추가됨] Entity_Equipment 또는 Entity_Consumables 양쪽 모두에서 필요한 정보(장착부위, 아이콘, 수량)를 안전하게 빼오는 헬퍼 함수
        private bool TryGetEquipmentOrConsumableData(EntityHandle handle, out EquipmentMountingArea area, out string assetKey, out int quantity)
        {
            area = default;
            assetKey = string.Empty;
            quantity = 0;

            if (handle.Equals(default(EntityHandle))) return false;

            Entity entity = EntityManager.Get(handle);
            if (entity is Entity_Equipment equip)
            {
                if (equip.ItemData != null)
                {
                    area = equip.ItemData.MountingArea;
                    assetKey = AssetKeys.Of(equip.ItemData, AssetUsage.Icon);
                    quantity = 1;
                    return true;
                }
            }
            else if (entity is Entity_Consumables cons)
            {
                if (cons.ItemData != null)
                {
                    area = cons.ItemData.MountingArea;
                    assetKey = AssetKeys.Of(cons.ItemData, AssetUsage.Icon);
                    quantity = cons.Quantity;
                    return true;
                }
            }

            return false;
        }

        private void ShowTooltip(EquipmentMountingArea area, RectTransform slotRect)
        {
            EntityHandle equippedItem = m_vm.GetEquippedHandleForArea(area);
            
            // 1. 빈 슬롯인 경우 툴팁 생략 (새 툴팁 시스템은 빈 툴팁 데이터 형태가 없으므로)
            if (equippedItem.Equals(default(EntityHandle)))
            {
                return;
            }

            Entity entity = EntityManager.Get(equippedItem);
            if (entity == null) return;

            Sprite icon = null;
            if (TryGetEquipmentOrConsumableData(equippedItem, out _, out string assetKey, out _))
            {
                if (!string.IsNullOrEmpty(assetKey))
                {
                    AssetProvider.TryGet<Sprite>(assetKey, out icon);
                }
            }

            // 2. 새 툴팁 시스템(PJW님 개발)의 Builder를 이용해 데이터를 조립하고 띄움
            DesktopCompanion.Views.ItemTooltipData data = DesktopCompanion.Views.ItemTooltipBuilder.FromEntity(equippedItem, EntityManager);
            if (data != null)
            {
                DesktopCompanion.Views.ItemTooltipController.Request(null, data, icon, slotRect);
            }
        }

        private void HideTooltip()
        {
            DesktopCompanion.Views.ItemTooltipController.Dismiss(null);
        }

        // =======================================================
        // [추가] 스탯 텍스트 파싱 헬퍼 메서드 (1_KSS 한정 독립적 사용)
        // =======================================================
        private string BuildModifiersText(DesktopCompanion.Data.StatModifier[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0)
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            for (int i = 0; i < modifiers.Length; i++)
            {
                DesktopCompanion.Data.StatModifier modifier = modifiers[i];

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(GetPlayerStatText(modifier.Stat));
                builder.Append(' ');
                builder.Append(GetModifierValueText(modifier.Stat, modifier.Value));
            }

            return builder.ToString();
        }

        private string GetModifierValueText(DesktopCompanion.Data.PlayerStat stat, float value)
        {
            if (IsPercentStat(stat))
            {
                return $"{GetSignedNumber(value * 100f)}%";
            }

            return GetSignedNumber(value);
        }

        private bool IsPercentStat(DesktopCompanion.Data.PlayerStat stat)
        {
            return stat == DesktopCompanion.Data.PlayerStat.CriticalChance
                || stat == DesktopCompanion.Data.PlayerStat.ProbabilityAtFishSize
                || stat == DesktopCompanion.Data.PlayerStat.ProbabilityAtFishRarity;
        }

        private string GetSignedNumber(float value)
        {
            return value >= 0f
                ? $"+{value:0.##}"
                : $"{value:0.##}";
        }

        private string GetPlayerStatText(DesktopCompanion.Data.PlayerStat stat)
        {
            switch (stat)
            {
                case DesktopCompanion.Data.PlayerStat.DamagePerClick:
                    return "클릭 피해력";
                case DesktopCompanion.Data.PlayerStat.ManualDamagePerHitMultiply:
                    return "수동 피해 배율";
                case DesktopCompanion.Data.PlayerStat.BattleTimeVariable:
                    return "전투 시간 변경";
                case DesktopCompanion.Data.PlayerStat.CriticalChance:
                    return "크리티컬 확률";
                case DesktopCompanion.Data.PlayerStat.CriticalMultiply:
                    return "크리티컬 배율";
                case DesktopCompanion.Data.PlayerStat.AutoBattleCooltime:
                    return "자동 전투 쿨타임";
                case DesktopCompanion.Data.PlayerStat.AutoSpeedPerTime:
                    return "자동 전투 속도";
                case DesktopCompanion.Data.PlayerStat.AutoDamagePerHitMultiply:
                    return "자동 피해 배율";
                case DesktopCompanion.Data.PlayerStat.MapMovementSpeedPerTime:
                    return "이동 속도";
                case DesktopCompanion.Data.PlayerStat.InventorySize:
                    return "인벤토리 크기";
                case DesktopCompanion.Data.PlayerStat.ProbabilityAtFishSize:
                    return "큰 물고기 확률";
                case DesktopCompanion.Data.PlayerStat.ProbabilityAtFishRarity:
                    return "희귀 물고기 확률";
                case DesktopCompanion.Data.PlayerStat.GoldGettingMultiply:
                    return "골드 획득 배율";
                default:
                    return stat.ToString();
            }
        }
    }
}

