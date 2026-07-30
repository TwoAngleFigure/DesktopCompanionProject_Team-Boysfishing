using System;
using System.Collections.Generic;
using System.Text;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class InventoryViewModel : UIViewModelBase
    {
        private const int AquariumProduceCount = 1;

        private InventorySystem m_inventorySystem;
        private PlayerSystem m_playerSystem;
        private CurrencySystem m_currencySystem;
        private ShopSystem m_shopSystem;

        private ItemType m_currentTab = ItemType.Fish;
        private int m_selectedSlotIndex = -1;

        // ViewModel -> View
        public readonly BindableProperty<List<InventorySlotViewData>> Slots = new(new List<InventorySlotViewData>());
        public readonly BindableProperty<InventorySlotViewData> SelectedSlot = new(null);
        public readonly BindableProperty<ItemType> CurrentTab = new(ItemType.Fish);
        public readonly BindableProperty<int> Gold = new(0);

        // View -> ViewModel
        public RelayCommand<ItemType> SelectTabCommand { get; private set; }
        public RelayCommand<int> SelectSlotCommand { get; private set; }

        public override void Bind()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
            m_playerSystem = SystemManager.GetSystem<PlayerSystem>();
            m_currencySystem = SystemManager.GetSystem<CurrencySystem>();
            m_shopSystem = SystemManager.GetSystem<ShopSystem>();

            SelectTabCommand = new RelayCommand<ItemType>(SelectTab);
            SelectSlotCommand = new RelayCommand<int>(SelectSlot);

            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged += HandleInventoryChanged;
            }

            if (m_currencySystem != null)
            {
                m_currencySystem.OnGoldChanged += HandleGoldChanged;

                //최초 값 리딩
                Gold.Value = m_currencySystem.CurrentGold;
            }
            RefreshSlots();
            RefreshSelectedSlot();
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

            m_inventorySystem = null;
            m_playerSystem = null;
            m_currencySystem = null;
            m_shopSystem = null;
        }

        private void HandleGoldChanged(int currentGold)
        {
            Gold.Value = currentGold;
        }

        private void HandleInventoryChanged()
        {
            RefreshSlots();
            RefreshSelectedSlot();
        }

        private void SelectTab(ItemType tab)
        {
            m_currentTab = NormalizeTab(tab);
            CurrentTab.Value = m_currentTab;

            m_selectedSlotIndex = -1;

            RefreshSlots();
            RefreshSelectedSlot();
        }

        private void SelectSlot(int slotIndex)
        {
            m_selectedSlotIndex = slotIndex;

            RefreshSlots();
            RefreshSelectedSlot();
        }

        private void RefreshSlots()
        {
            List<InventorySlotViewData> result = new();

            int slotCount = 0;

            if (m_inventorySystem != null)
            {
                slotCount = m_inventorySystem.GetMaxSlotCount(m_currentTab);
            }

            for (int i = 0; i < slotCount; i++)
            {
                result.Add(ResolveSlotData(i));
            }

            Slots.Value = result;
        }

        private void RefreshSelectedSlot()
        {
            if (m_selectedSlotIndex < 0)
            {
                SelectedSlot.Value = null;
                return;
            }

            SelectedSlot.Value = ResolveSlotData(m_selectedSlotIndex);
        }

        private InventorySlotViewData ResolveSlotData(int slotIndex)
        {
            bool isSelected = slotIndex == m_selectedSlotIndex;

            if (m_inventorySystem == null)
            {
                return InventorySlotViewData.Empty(slotIndex, m_currentTab, isSelected);
            }

            bool hasHandle = m_inventorySystem.GetHandleAt(m_currentTab, slotIndex, out EntityHandle handle);

            if (!hasHandle)
            {
                return InventorySlotViewData.Empty(slotIndex, m_currentTab, isSelected);
            }

            Entity entity = EntityManager.Get(handle);

            if (entity == null)
            {
                return InventorySlotViewData.Empty(slotIndex, m_currentTab, isSelected);
            }

            return CreateSlotViewData(slotIndex, handle, entity, isSelected);
        }

        private InventorySlotViewData CreateSlotViewData(int slotIndex, EntityHandle handle, Entity entity, bool isSelected)
        {
            ItemType actualItemType = GetItemType(entity);
            string iconKey = BuildIconKey(entity);

            InventorySlotViewData viewData = new()
            {
                SlotIndex = slotIndex,
                IsEmpty = false,

                SlotType = m_currentTab,
                ItemType = actualItemType,

                Handle = handle,

                ItemName = entity.Name,
                IconKey = iconKey,
                DataId = entity.DataId,

                Tier = 0,
                UnitSellPrice = 0,

                Size = 0f,
                Rarity = ItemRarity.Normal,
                Quality = ItemQuality.OneStar,

                MountingArea = default,
                UpgradeLevel = 0,

                Quantity = 0,

                GradeText = string.Empty,
                EffectText = string.Empty,
                SellPriceText = string.Empty,

                IsSelected = isSelected
            };

            ApplyRuntimeValues(viewData, entity);

            return viewData;
        }

        /// <summary>
        /// 실제 Entity 타입에 따라 UI 표시 데이터를 구성
        /// </summary>
        private void ApplyRuntimeValues(InventorySlotViewData viewData, Entity entity)
        {
            if (entity is Entity_Fish fish)
            {
                ApplyFishValues(viewData, fish);
                return;
            }

            if (entity is Entity_Equipment equipment)
            {
                ApplyEquipmentValues(viewData, equipment);
                return;
            }

            if (entity is Entity_Materials materials)
            {
                ApplyMaterialValues(viewData, materials);
                return;
            }

            if (entity is Entity_Consumables consumables)
            {
                ApplyConsumableValues(viewData, consumables);
            }
        }

        private void ApplyFishValues(InventorySlotViewData viewData, Entity_Fish fish)
        {
            ItemData_Fish itemData = fish.ItemData;

            viewData.Size = fish.Size;
            viewData.Rarity = fish.Rarity;
            viewData.Quality = fish.Quality;

            if (itemData == null)
            {
                viewData.GradeText =
                    $"{GetRarityText(fish.Rarity)} / {GetQualityText(fish.Quality)}\n" +
                    $"크기 {fish.Size:F1}cm";

                viewData.EffectText = "수족관 생산 정보 없음";
                viewData.SellPriceText = "판매 정보 없음";
                return;
            }

            viewData.Tier = itemData.Tier;

            // F1: 소수점 첫째 자리까지 고정 표시
            viewData.GradeText =
                $"{itemData.Tier}티어 / {GetRarityText(fish.Rarity)} / {GetQualityText(fish.Quality)}\n" +
                $"크기 {fish.Size:F1}cm";

            viewData.EffectText = BuildFishEffectText(fish);

            ApplySellPrice(viewData, itemData);
        }

        private void ApplyEquipmentValues(InventorySlotViewData viewData, Entity_Equipment equipment)
        {
            ItemData_Equipment itemData = equipment.ItemData;

            viewData.UpgradeLevel = equipment.UpgradeLevel;

            if (itemData == null)
            {
                viewData.GradeText = equipment.UpgradeLevel > 0
                    ? $"+{equipment.UpgradeLevel} 강화"
                    : "";

                viewData.EffectText = "장비 정보 없음";
                viewData.SellPriceText = "판매 정보 없음";
                return;
            }

            viewData.Tier = itemData.Tier;
            viewData.MountingArea = itemData.MountingArea;

            string mountingAreaText = GetMountingAreaText(itemData.MountingArea);

            string upgradeText = equipment.UpgradeLevel > 0
                ? $"+{equipment.UpgradeLevel} 강화"
                : "";

            viewData.GradeText =
                $"{itemData.Tier}티어 / {mountingAreaText}\n" +
                upgradeText;

            viewData.EffectText = BuildEquipmentEffectText(equipment);

            ApplySellPrice(viewData, itemData);
        }

        private void ApplyMaterialValues(InventorySlotViewData viewData, Entity_Materials materials)
        {
            ItemData_Materials itemData = materials.ItemData;

            viewData.Quantity = materials.Quantity;

            if (itemData == null)
            {
                viewData.GradeText = "재료";
                viewData.EffectText = $"보유 수량 {materials.Quantity}개";
                viewData.SellPriceText = "판매 정보 없음";
                return;
            }

            viewData.Tier = itemData.Tier;
            viewData.GradeText = $"{itemData.Tier}티어 / 재료";
            viewData.EffectText = $"보유 수량 {materials.Quantity}개";

            ApplySellPrice(viewData, itemData);
        }

        private void ApplyConsumableValues(InventorySlotViewData viewData, Entity_Consumables consumables)
        {
            ItemData_Consumables itemData = consumables.ItemData;

            viewData.Quantity = consumables.Quantity;

            if (itemData == null)
            {
                viewData.GradeText = "소모품";
                viewData.EffectText =
                    $"보유 수량 {consumables.Quantity}개\n" +
                    "효과 정보 없음";

                viewData.SellPriceText = "판매 정보 없음";
                return;
            }

            viewData.Tier = itemData.Tier;
            viewData.MountingArea = itemData.MountingArea;

            string categoryText = GetMountingAreaText(itemData.MountingArea);

            viewData.GradeText = $"{itemData.Tier}티어 / {categoryText}";
            viewData.EffectText = BuildConsumableDetailText(consumables);

            ApplySellPrice(viewData, itemData);
        }

        /// <summary>
        /// 물고기의 품질에 따라 수족관 생산 시간을 감소
        ///
        /// 최종 시간
        /// = 기본 생산 시간 - 감소량 × (성급 - 1)
        /// </summary>
        private string BuildFishEffectText(Entity_Fish fish)
        {
            ItemData_Fish itemData = fish.ItemData;

            if (itemData == null || itemData.AquariumMaterial == null)
            {
                return "수족관 생산 정보 없음";
            }

            int qualityStep = Math.Max(0, (int)fish.Quality - 1);

            float productionTime =
                itemData.AquariumProduceTime
                - itemData.AquariumDecreaseCount * qualityStep;

            productionTime = Math.Max(1f, productionTime);

            return $"{itemData.AquariumMaterial.Name} {productionTime:0.#}초마다 {AquariumProduceCount}개 생성";
        }

        /// <summary>
        /// 현재 강화 단계에서 적용되는 장비 스탯을 표시
        /// </summary>
        private string BuildEquipmentEffectText(Entity_Equipment equipment)
        {
            StringBuilder builder = new();

            AppendModifiers(builder, equipment.CurrentModifiers);

            return builder.Length > 0
                ? builder.ToString()
                : "적용 스탯 없음";
        }

        /// <summary>
        /// 소모품의 보유 수량과 실제 효과를 표시
        /// </summary>
        private string BuildConsumableDetailText(Entity_Consumables consumables)
        {
            ItemData_Consumables itemData = consumables.ItemData;
            StringBuilder builder = new();

            builder.Append($"보유 수량 {consumables.Quantity}개");

            StringBuilder effectBuilder = new();

            AppendModifiers(effectBuilder, itemData.Modifiers);

            if (itemData.SummonTarget != null)
            {
                if (effectBuilder.Length > 0)
                {
                    effectBuilder.AppendLine();
                }

                effectBuilder.Append(itemData.SummonTarget.Name);
                effectBuilder.Append(" 소환");
            }

            builder.AppendLine();

            if (effectBuilder.Length > 0)
            {
                builder.Append(effectBuilder);
            }
            else
            {
                builder.Append("효과 정보 없음");
            }

            return builder.ToString();
        }

        /// <summary>
        /// StatModifier 배열을 사람이 읽을 수 있는 문구로 변환
        /// </summary>
        private void AppendModifiers(StringBuilder builder, StatModifier[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < modifiers.Length; i++)
            {
                StatModifier modifier = modifiers[i];

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(GetPlayerStatText(modifier.Stat));
                builder.Append(' ');
                builder.Append(GetModifierValueText(modifier.Stat, modifier.Value));
            }
        }

        private string GetModifierValueText(PlayerStat stat, float value)
        {
            if (IsPercentStat(stat))
            {
                return $"{GetSignedNumber(value * 100f)}%";
            }

            return GetSignedNumber(value);
        }

        private bool IsPercentStat(PlayerStat stat)
        {
            return stat == PlayerStat.CriticalChance
                || stat == PlayerStat.ProbabilityAtFishSize
                || stat == PlayerStat.ProbabilityAtFishRarity;
        }

        private string GetSignedNumber(float value)
        {
            return value >= 0f
                ? $"+{value:0.##}"
                : $"{value:0.##}";
        }

        /// <summary>
        /// 판매가 표시
        /// 현재는 임시로 판매가 그대로 반환
        /// 후에 ShopSystem 추가되면 API 따와서 PlayerStat 반영
        /// </summary>
        private void ApplySellPrice(InventorySlotViewData viewData, ItemData itemData)
        {
            int unitSellPrice = CalculateUnitSellPrice(viewData, itemData);

            viewData.UnitSellPrice = unitSellPrice;
            viewData.SellPriceText = BuildSellPriceText(unitSellPrice);
        }

        private int CalculateUnitSellPrice(InventorySlotViewData viewData, ItemData itemData)
        {
            if (m_shopSystem != null)
            {
                return m_shopSystem.CalculateSellGold(viewData.Handle, 1);
            }

            return itemData.BasePrice;
        }

        private string BuildSellPriceText(int unitSellPrice)
        {
            if (unitSellPrice <= 0)
            {
                return "판매 불가";
            }

            return $"개당 판매가 {unitSellPrice:N0} G";
        }

        private string GetPlayerStatText(PlayerStat stat)
        {
            switch (stat)
            {
                case PlayerStat.DamagePerClick:
                    return "클릭 공격력";

                case PlayerStat.ManualDamagePerHitMultiply:
                    return "수동 공격 배율";

                case PlayerStat.BattleTimeVariable:
                    return "전투 시간";

                case PlayerStat.CriticalChance:
                    return "크리티컬 확률";

                case PlayerStat.CriticalMultiply:
                    return "크리티컬 배율";

                case PlayerStat.AutoBattleCooltime:
                    return "자동 낚시 간격";

                case PlayerStat.AutoSpeedPerTime:
                    return "자동 공격 속도";

                case PlayerStat.AutoDamagePerHitMultiply:
                    return "자동 공격 배율";

                case PlayerStat.MapMovementSpeedPerTime:
                    return "이동 속도";

                case PlayerStat.InventorySize:
                    return "인벤토리 크기";

                case PlayerStat.ProbabilityAtFishSize:
                    return "높은 성급 등장 확률";

                case PlayerStat.ProbabilityAtFishRarity:
                    return "높은 등급 등장 확률";

                case PlayerStat.GoldGettingMultiply:
                    return "골드 획득 배율";

                default:
                    return stat.ToString();
            }
        }

        private string GetRarityText(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Normal:
                    return "일반";

                case ItemRarity.Uncommon:
                    return "고급";

                case ItemRarity.Rare:
                    return "희귀";

                case ItemRarity.Epic:
                    return "영웅";

                case ItemRarity.Legendary:
                    return "전설";

                default:
                    return rarity.ToString();
            }
        }

        private string GetQualityText(ItemQuality quality)
        {
            switch (quality)
            {
                case ItemQuality.OneStar:
                    return "★";

                case ItemQuality.TwoStar:
                    return "★★";

                case ItemQuality.ThreeStar:
                    return "★★★";

                case ItemQuality.FourStar:
                    return "★★★★";

                case ItemQuality.FiveStar:
                    return "★★★★★";

                default:
                    return string.Empty;
            }
        }

        private string GetMountingAreaText(EquipmentMountingArea mountingArea)
        {
            switch (mountingArea)
            {
                case EquipmentMountingArea.FishingRod:
                    return "낚싯대";

                case EquipmentMountingArea.FishingLine:
                    return "낚싯줄";

                case EquipmentMountingArea.Reel:
                    return "릴";

                case EquipmentMountingArea.Lure:
                    return "루어";

                case EquipmentMountingArea.Hat:
                    return "모자";

                case EquipmentMountingArea.Uniform:
                    return "한벌옷";

                case EquipmentMountingArea.Gloves:
                    return "장갑";

                case EquipmentMountingArea.Engine:
                    return "엔진";

                case EquipmentMountingArea.Storage:
                    return "물고기 창고";

                case EquipmentMountingArea.GPS:
                    return "GPS";

                case EquipmentMountingArea.Bait:
                    return "미끼";

                case EquipmentMountingArea.Groundbait:
                    return "떡밥";

                default:
                    return mountingArea.ToString();
            }
        }

        private ItemType GetItemType(Entity entity)
        {
            if (entity is Entity_Fish)
            {
                return ItemType.Fish;
            }

            if (entity is Entity_Equipment)
            {
                return ItemType.Equipment;
            }

            if (entity is Entity_Materials)
            {
                return ItemType.Materials;
            }

            if (entity is Entity_Consumables)
            {
                return ItemType.Consumables;
            }

            return m_currentTab;
        }

        private string BuildIconKey(Entity entity)
        {
            ItemData itemData = GetItemData(entity);

            if (itemData == null)
            {
                return string.Empty;
            }

            return AssetKeys.Of(itemData, AssetUsage.Icon);
        }


        private ItemData GetItemData(Entity entity)
        {
            if (entity is Entity_Fish fish)
            {
                return fish.ItemData;
            }

            if (entity is Entity_Equipment equipment)
            {
                return equipment.ItemData;
            }

            if (entity is Entity_Materials materials)
            {
                return materials.ItemData;
            }

            if (entity is Entity_Consumables consumables)
            {
                return consumables.ItemData;
            }

            return null;
        }

        private ItemType NormalizeTab(ItemType tab)
        {
            if (tab == ItemType.Consumables)
            {
                return ItemType.Materials;
            }

            return tab;
        }

        public bool SwapSlots(int fromIndex, int toIndex)
        {
            if (m_inventorySystem == null)
            {
                return false;
            }

            return m_inventorySystem.SwapSlots(CurrentTab.Value, fromIndex, toIndex);
        }

        public bool EquipEquipmentAtSlot(int slotIndex)
        {
            if (m_playerSystem == null || m_inventorySystem == null || EntityManager == null)
            {
                return false;
            }

            if (!m_inventorySystem.GetHandleAt(ItemType.Equipment, slotIndex, out EntityHandle equipmentHandle))
            {
                return false;
            }

            Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(equipmentHandle);

            if (equipment == null || equipment.ItemData == null)
            {
                return false;
            }

            m_playerSystem.Equip(equipment.ItemData.MountingArea, equipmentHandle);
            return true;
        }

        public bool PlaceEquippedItemAtSlot(EquipmentMountingArea sourceArea, int targetSlotIndex)
        {
            if (m_playerSystem == null || m_inventorySystem == null || EntityManager == null)
            {
                return false;
            }

            int slotCount = m_inventorySystem.GetMaxSlotCount(ItemType.Equipment);

            if (targetSlotIndex < 0 || targetSlotIndex >= slotCount)
            {
                return false;
            }

            // 해제 전, 현재 장착 중인 장비 핸들 보관
            EntityHandle beforeEquipHandle = m_playerSystem.GetEquippedItemHandle(sourceArea);

            if (beforeEquipHandle.Equals(default(EntityHandle)))
            {
                return false;
            }

            // 사용자가 클릭한 인벤토리 슬롯 장비 조회
            bool hasTargetItem = m_inventorySystem.GetHandleAt(
                ItemType.Equipment,
                targetSlotIndex,
                out EntityHandle targetHandle);

            // 클릭한 슬롯에 장비가 있다면 같은 장착 부위인지 확인
            if (hasTargetItem)
            {
                Entity_Equipment targetEquipment = EntityManager.Get<Entity_Equipment>(targetHandle);

                if (targetEquipment == null || targetEquipment.ItemData == null)
                {
                    return false;
                }

                // 다른 부위 장비는 교환 불가
                if (targetEquipment.ItemData.MountingArea != sourceArea)
                {
                    return false;
                }

                m_playerSystem.Equip(sourceArea, targetHandle);
            }
            else
            {
                // 빈 슬롯 클릭 -> 기존 장비만 해제
                m_playerSystem.Equip(sourceArea, default(EntityHandle));
            }

            // 해제 장비가 반환된 인벤토리 슬롯 탐색
            int returnedSlotIndex = FindEquipmentSlotIndex(beforeEquipHandle);

            if (returnedSlotIndex < 0)
            {
                return false;
            }

            if (returnedSlotIndex == targetSlotIndex)
            {
                return true;
            }

            // 해제장비를 사용자가 클릭한 슬롯으로 이동
            return m_inventorySystem.SwapSlots(
                ItemType.Equipment,
                returnedSlotIndex,
                targetSlotIndex);
        }

        private int FindEquipmentSlotIndex(EntityHandle handle)
        {
            if (m_inventorySystem == null)
            {
                return -1;
            }

            int slotCount = m_inventorySystem.GetMaxSlotCount(ItemType.Equipment);

            for (int i = 0; i < slotCount; i++)
            {
                if (!m_inventorySystem.GetHandleAt(ItemType.Equipment, i, out EntityHandle slotHandle))
                {
                    continue;
                }

                if (slotHandle.Equals(handle))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}