using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using DesktopCompanion.Data;
using System.Text;
using System;
using UnityEngine;

namespace DesktopCompanion.Views
{
    public class ReinforceViewModel : UIViewModelBase
    {
        private PlayerSystem m_playerSystem;
        private InventorySystem m_inventorySystem;

        public readonly BindableProperty<EntityHandle> SelectedEquipment = new(default);
        public readonly BindableProperty<int> RequiredGold = new(0);
        public readonly BindableProperty<int> CurrentGold = new(0);
        public readonly BindableProperty<string> RequiredMaterialText = new("");
        public readonly BindableProperty<string> StatIncreaseText = new("");

        public RelayCommand ReinforceCommand { get; private set; }

        public override void Bind()
        {
            m_playerSystem = SystemManager.GetSystem<PlayerSystem>();
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
            
            // [System 상태 변경 구독] 
            // 플레이어의 골드가 변경되거나(스탯 변경 이벤트), 인벤토리의 재료가 변경될 때 UI를 실시간 갱신하기 위해 이벤트를 구독합니다.
            if (m_playerSystem != null)
            {
                m_playerSystem.OnStatChanged += HandleStateChanged;
            }
            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged += HandleInventoryChanged;
            }

            ReinforceCommand = new RelayCommand(TryReinforce);
            
            // 뷰모델 생성 시 초기 상태를 갱신하여 빈 문자열 대신 안내 문구가 표시되도록 합니다.
            UpdateReinforcementInfo(SelectedEquipment.Value);
        }

        // 플레이어 스탯(골드 등)이 변경되었을 때 호출되며, 현재 올려둔 장비가 있다면 강화 정보를 즉시 갱신합니다.
        private void HandleStateChanged(EntityHandle handle)
        {
            if (SelectedEquipment.Value.Value != Guid.Empty)
            {
                UpdateReinforcementInfo(SelectedEquipment.Value);
            }
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

            if (handle.Value == Guid.Empty)
            {
                RequiredGold.Value = 0;
                StatIncreaseText.Value = "장비를 등록해주세요.";
                RequiredMaterialText.Value = "장비를 등록해주세요.";
                return;
            }

            Entity_Equipment equip = EntityManager.Get<Entity_Equipment>(handle);
            if (equip == null) return;
            
            UpgradeStep nextStep = equip.ItemData.GetNextUpgradeStep(equip.UpgradeLevel);
            
            if (nextStep == null)
            {
                StatIncreaseText.Value = "최대 강화 레벨입니다.";
                RequiredGold.Value = 0;
                RequiredMaterialText.Value = "재료 요구 없음";
                return;
            }

            // 1. 필요 골드 설정
            RequiredGold.Value = nextStep.GoldCost;
            
            // 2. 필요 재료 문자열 완성
            if (nextStep.MaterialCosts != null && nextStep.MaterialCosts.Length > 0)
            {
                var mat = nextStep.MaterialCosts[0];
                int currentMat = 0; 
                
                // 인벤토리 시스템의 읽기 API(GetTotalQuantityByDataId)를 활용하여 
                // 현재 플레이어가 소지하고 있는 해당 재료의 실제 총량을 가져옵니다.
                if (m_inventorySystem != null && mat.Material != null)
                {
                    currentMat = m_inventorySystem.GetTotalQuantityByDataId(DesktopCompanion.Data.ItemType.Materials, mat.Material.ID);
                }

                RequiredMaterialText.Value = $"{mat.Material?.Name ?? "재료"} {currentMat} / {mat.Count}";
            }
            else
            {
                RequiredMaterialText.Value = "필요 재료 없음";
            }

            // 3. 스탯 증가량 문자열 완성
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<color=#5BC0EB><b>[{equip.ItemData.Name}]</b></color>");
            sb.AppendLine("-------------------");
            
            // 현재 스탯 가져오기 (0강이면 기본 효과, 그 이상이면 해당 레벨의 누적 효과)
            var currentModifiers = equip.ItemData.GetModifiers(equip.UpgradeLevel);

            if (nextStep.Modifiers != null)
            {
                foreach (var nextMod in nextStep.Modifiers)
                {
                    // 현재 스탯에서 같은 종류의 스탯 값 찾기
                    float currValue = 0f;
                    if (currentModifiers != null)
                    {
                        foreach (var currMod in currentModifiers)
                        {
                            if (currMod.Stat == nextMod.Stat)
                            {
                                currValue = currMod.Value;
                                break;
                            }
                        }
                    }

                    sb.AppendLine($"{GetStatNameKR(nextMod.Stat)} : {currValue} <color=#00FF00>-> {nextMod.Value}</color>");
                }
            }
            
            StatIncreaseText.Value = sb.ToString();
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

        private string GetStatNameKR(PlayerStat stat)
        {
            return stat switch {
                PlayerStat.DamagePerClick => "클릭 데미지",
                PlayerStat.ManualDamagePerHitMultiply => "수동 타격 배율",
                PlayerStat.BattleTimeVariable => "전투 시간 변수",
                PlayerStat.CriticalChance => "크리티컬 확률",
                PlayerStat.CriticalMultiply => "크리티컬 배율",
                PlayerStat.AutoBattleCooltime => "자동 공격 쿨타임",
                PlayerStat.AutoSpeedPerTime => "자동 공격 속도",
                PlayerStat.AutoDamagePerHitMultiply => "자동 타격 배율",
                _ => stat.ToString()
            };
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

            m_playerSystem = null;
            m_inventorySystem = null;
        }
    }
}
