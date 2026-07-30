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
        [SerializeField] private DesktopCompanion.Views.InventoryItemTooltipView m_itemTooltip; // [추가] 인벤토리용 툴팁 컴포넌트 참조

        [Header("Tab Buttons")]
        [SerializeField] private Button m_tabPlayerEquipBtn;
        [SerializeField] private Button m_tabShipEquipBtn;
        [SerializeField] private Button m_tabStatsBtn;

        [Header("Tab Panels")]
        [SerializeField] private GameObject m_playerEquipPanel;
        [SerializeField] private GameObject m_shipEquipPanel;
        [SerializeField] private GameObject m_statsPanel;

        private void Awake()
        {
            if (m_itemTooltip != null)
            {
                // 인벤토리 창이 꺼져있어도 작동하도록 장비창 전용으로 툴팁 복제
                m_itemTooltip = Instantiate(m_itemTooltip, this.transform);
                // 툴팁 루트 컴포넌트 전체를 꺼버리면 내부의 m_tooltipRoot.SetActive(true)가 작동하지 않으므로, 정상적인 숨김 처리(Hide)를 호출합니다.
                m_itemTooltip.gameObject.SetActive(true);
                m_itemTooltip.Hide();
            }
        }

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
            if (m_itemTooltip == null) return;

            EntityHandle equippedItem = m_vm.GetEquippedHandleForArea(area);
            
            // 1. 빈 슬롯인 경우: 부위 이름 안내 툴팁 표시
            if (equippedItem.Equals(default(EntityHandle)))
            {
                DesktopCompanion.Views.InventorySlotViewData emptyData = new DesktopCompanion.Views.InventorySlotViewData
                {
                    IsEmpty = false,
                    ItemName = $"{area} 슬롯",
                    GradeText = "장착된 아이템 없음",
                    EffectText = "클릭하여 장착하거나 아이템을 드래그하세요.",
                    SellPriceText = ""
                };
                m_itemTooltip.Show(emptyData, null, slotRect);
                return;
            }

            Entity entity = EntityManager.Get(equippedItem);
            if (entity == null) return;

            // 2. 아이템이 장착된 경우
            string gradeText = "장착중";
            string effectText = "상세 정보는 인벤토리에서 확인하세요.";
            string assetKey = string.Empty;
            int quantity = 1;

            if (entity is Entity_Equipment equip && equip.ItemData != null)
            {
                gradeText = $"{equip.ItemData.Tier}티어 / 장비";
                if (equip.UpgradeLevel > 0) gradeText += $"\n+{equip.UpgradeLevel} 강화";
                assetKey = AssetKeys.Of(equip.ItemData, AssetUsage.Icon);
                effectText = $"현재 부위: {area}";
                string modifiersText = BuildModifiersText(equip.CurrentModifiers);
                if (!string.IsNullOrEmpty(modifiersText))
                {
                    effectText += $"\n{modifiersText}";
                }
            }
            else if (entity is Entity_Consumables cons && cons.ItemData != null)
            {
                gradeText = $"{cons.ItemData.Tier}티어 / 소모품";
                assetKey = AssetKeys.Of(cons.ItemData, AssetUsage.Icon);
                effectText = $"남은 개수: {cons.Quantity}개";
                string modifiersText = BuildModifiersText(cons.ItemData.Modifiers);
                if (!string.IsNullOrEmpty(modifiersText))
                {
                    effectText += $"\n{modifiersText}";
                }
                quantity = cons.Quantity;
            }
            // 미끼/떡밥이 만약 Entity_Materials로 처리되는 예외 상황 대비 (방어 코드)
            else if (entity is Entity_Materials mat && mat.ItemData != null)
            {
                gradeText = $"{mat.ItemData.Tier}티어 / 재료";
                assetKey = AssetKeys.Of(mat.ItemData, AssetUsage.Icon);
                effectText = $"남은 개수: {mat.Quantity}개";
                quantity = mat.Quantity;
            }
            else
            {
                // 기타 타입 방어 코드
                effectText = $"현재 부위: {area}\n상세 정보는 인벤토리에서 확인하세요.";
            }

            DesktopCompanion.Views.InventorySlotViewData dummyData = new DesktopCompanion.Views.InventorySlotViewData
            {
                IsEmpty = false,
                ItemName = entity.Name,
                GradeText = gradeText,
                EffectText = effectText,
                SellPriceText = ""
            };

            Sprite icon = null;
            if (!string.IsNullOrEmpty(assetKey))
            {
                AssetProvider.TryGet<Sprite>(assetKey, out icon);
            }

            m_itemTooltip.Show(dummyData, icon, slotRect);
        }

        private void HideTooltip()
        {
            if (m_itemTooltip != null)
            {
                m_itemTooltip.Hide();
            }
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

