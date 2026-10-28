using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using DesktopCompanion.Data;
using System.Collections.Generic;
using System;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 강화창의 ViewModel. 슬롯에 올라간 장비의 표시 상태를 <see cref="ReinforceVD"/> 하나로 내보낸다.
    /// 문장 조립은 하지 않는다 — 값만 담고 표시는 View가 맡는다.
    /// </summary>
    public class ReinforceViewModel : UIViewModelBase
    {
        private PlayerSystem m_playerSystem;
        private InventorySystem m_inventorySystem;
        private CurrencySystem m_currencySystem; // 골드 변경을 실시간으로 감지하기 위한 시스템 참조

        public readonly BindableProperty<EntityHandle> SelectedEquipment = new(default);

        /// <summary>창 표시 스냅샷. 장비 등록·재료 변동·강화 성공 때마다 새로 만들어진다.</summary>
        public readonly BindableProperty<ReinforceVD> Info = new(new ReinforceVD());

        /// <summary>
        /// 보유 골드. VD와 분리해 둔다 — 판매 등으로 자주 바뀌는데 VD에 섞으면 그때마다 행을 다시 그리게 된다.
        /// </summary>
        public readonly BindableProperty<int> CurrentGold = new(0);

        public RelayCommand ReinforceCommand { get; private set; }

        public override void Bind()
        {
            m_playerSystem = SystemManager.GetSystem<PlayerSystem>();
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();

            // [System 상태 변경 구독]
            // 플레이어의 스탯이 변경되거나 인벤토리의 재료가 변경될 때 UI를 실시간 갱신하기 위해 이벤트를 구독합니다.
            if (m_playerSystem != null)
            {
                m_playerSystem.OnStatChanged += HandleStateChanged;
            }
            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged += HandleInventoryChanged;
            }

            // 순수하게 '골드' 수치만 변경되는 상황(아이템 판매 등)을 즉각 캐치하기 위해 구독
            m_currencySystem = SystemManager.GetSystem<CurrencySystem>();
            if (m_currencySystem != null)
            {
                m_currencySystem.OnGoldChanged += HandleGoldChanged;
            }

            ReinforceCommand = new RelayCommand(TryReinforce);

            // 뷰모델 생성 시 초기 상태를 갱신하여 View가 빈 상태 안내를 표시할 수 있게 합니다.
            UpdateReinforcementInfo(SelectedEquipment.Value);
        }

        // 플레이어 스탯이 변경되었을 때 호출되며, 현재 올려둔 장비가 있다면 강화 정보를 즉시 갱신합니다.
        private void HandleStateChanged(EntityHandle handle)
        {
            if (SelectedEquipment.Value.Value != Guid.Empty)
            {
                UpdateReinforcementInfo(SelectedEquipment.Value);
            }
        }

        // 아이템 판매 등으로 보유 골드가 변경되면, 즉시 UI에 반영합니다.
        private void HandleGoldChanged(int newGold)
        {
            CurrentGold.Value = newGold;
        }

        // 인벤토리(강화 재료 등)가 변경되었을 때 호출되며, 현재 올려둔 장비가 있다면 보유 재료 개수를 즉시 갱신합니다.
        private void HandleInventoryChanged()
        {
            if (SelectedEquipment.Value.Value != Guid.Empty)
            {
                UpdateReinforcementInfo(SelectedEquipment.Value);
            }
        }

        public void RegisterEquipment(EntityHandle handle)
        {
            SelectedEquipment.Value = handle;
            UpdateReinforcementInfo(handle);
        }

        private void UpdateReinforcementInfo(EntityHandle handle)
        {
            // 장비 등록 여부와 상관없이 현재 플레이어의 골드는 항상 최신으로 갱신하여 표시되게 합니다.
            if (m_playerSystem != null && m_playerSystem.PlayerHandle.Value != Guid.Empty)
            {
                Entity_Player player = EntityManager.Get<Entity_Player>(m_playerSystem.PlayerHandle);
                if (player != null)
                {
                    CurrentGold.Value = player.Gold;
                }
            }

            Entity_Equipment equip = handle.Value != Guid.Empty
                ? EntityManager.Get<Entity_Equipment>(handle)
                : null;

            if (equip == null || equip.ItemData == null)
            {
                Info.Value = new ReinforceVD();   // HasEquipment = false → View가 빈 상태 안내를 켠다
                return;
            }

            ItemData_Equipment data = equip.ItemData;
            UpgradeStep nextStep = data.GetNextUpgradeStep(equip.UpgradeLevel);

            var vd = new ReinforceVD
            {
                HasEquipment = true,
                Handle = handle,
                Name = data.Name,
                Tier = data.Tier,
                UpgradeLevel = equip.UpgradeLevel,
                MaxUpgradeLevel = data.MaxUpgradeLevel,
                MountingArea = data.MountingArea,
                IsMaxLevel = nextStep == null,
                GoldCost = nextStep != null ? nextStep.GoldCost : 0,
            };

            BuildStats(vd, data, equip.UpgradeLevel, nextStep);
            BuildMaterials(vd, nextStep);

            Info.Value = vd;
        }

        /// <summary>
        /// 현재 효과를 먼저 깔고, 다음 단계에 같은 스탯이 있으면 그 차이를 증가분으로 채운다.
        /// 강화 단계 정의가 '도달 시 누적치'(ItemData_Equipment.GetModifiers)이므로 차이가 곧 증가분이다.
        /// </summary>
        private static void BuildStats(ReinforceVD vd, ItemData_Equipment data, int level, UpgradeStep nextStep)
        {
            StatModifier[] currentModifiers = data.GetModifiers(level);
            if (currentModifiers != null)
            {
                foreach (StatModifier modifier in currentModifiers)
                {
                    if (modifier == null) continue;

                    vd.Stats.Add(new ReinforceStatVD
                    {
                        Stat = modifier.Stat,
                        Operation = modifier.Operation,
                        Current = modifier.Value,
                        Delta = 0f,
                    });
                }
            }

            if (nextStep == null || nextStep.Modifiers == null)
            {
                return;
            }

            foreach (StatModifier modifier in nextStep.Modifiers)
            {
                if (modifier == null) continue;

                int index = vd.Stats.FindIndex(s => s.Stat == modifier.Stat && s.Operation == modifier.Operation);
                if (index >= 0)
                {
                    vd.Stats[index].Delta = modifier.Value - vd.Stats[index].Current;
                }
                else
                {
                    // 이번 강화로 처음 붙는 스탯 — 현재값 0에서 시작한다.
                    vd.Stats.Add(new ReinforceStatVD
                    {
                        Stat = modifier.Stat,
                        Operation = modifier.Operation,
                        Current = 0f,
                        Delta = modifier.Value,
                    });
                }
            }
        }

        /// <summary>
        /// 재료를 전 종류 담는다. 강화 성패를 판정하는 PlayerSystem이 전 종류를 검사하므로
        /// 표시도 같은 범위를 봐야 "충분해 보이는데 실패"가 생기지 않는다.
        /// </summary>
        private void BuildMaterials(ReinforceVD vd, UpgradeStep nextStep)
        {
            if (nextStep == null || nextStep.MaterialCosts == null)
            {
                return;
            }

            foreach (MaterialCost cost in nextStep.MaterialCosts)
            {
                if (cost == null || cost.Material == null) continue;

                // 인벤토리 시스템의 읽기 API로 현재 플레이어가 소지한 해당 재료의 실제 총량을 가져옵니다.
                int owned = m_inventorySystem != null
                    ? m_inventorySystem.GetTotalQuantityByDataId(ItemType.Materials, cost.Material.ID)
                    : 0;

                var material = new ReinforceMaterialVD
                {
                    Data = cost.Material,
                    Owned = owned,
                    Required = cost.Count,
                };
                vd.Materials.Add(material);

                if (material.IsEnough == false)
                {
                    vd.MaterialsEnough = false;
                }
            }
        }

        private void TryReinforce()
        {
            if (SelectedEquipment.Value.Value != Guid.Empty && m_playerSystem != null)
            {
                bool success = m_playerSystem.TryReinforceEquipment(SelectedEquipment.Value);
                if (success)
                {
                    UpdateReinforcementInfo(SelectedEquipment.Value);
                }
            }
        }

        // ViewModel 파괴 시 호출되며, 구독했던 System 이벤트들을 안전하게 해제하여 메모리 누수를 방지합니다.
        public override void Unbind()
        {
            if (m_playerSystem != null)
            {
                m_playerSystem.OnStatChanged -= HandleStateChanged;
            }
            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
            }
            if (m_currencySystem != null)
            {
                m_currencySystem.OnGoldChanged -= HandleGoldChanged;
            }

            m_playerSystem = null;
            m_inventorySystem = null;
            m_currencySystem = null;
        }
    }
}
