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
            
            ReinforceCommand = new RelayCommand(TryReinforce);
        }

        public void RegisterEquipment(EntityHandle handle)
        {
            SelectedEquipment.Value = handle;
            UpdateReinforcementInfo(handle);
        }

        private void UpdateReinforcementInfo(EntityHandle handle)
        {
            if (handle.Value == Guid.Empty)
            {
                RequiredGold.Value = 0;
                StatIncreaseText.Value = "장비를 등록해주세요.";
                RequiredMaterialText.Value = "";
                return;
            }

            Entity_Equipment equip = EntityManager.Get<Entity_Equipment>(handle);
            if (equip == null) return;

            Entity_Player player = EntityManager.Get<Entity_Player>(m_playerSystem.PlayerHandle);
            if (player != null)
            {
                CurrentGold.Value = player.Gold;
            }
            
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
            
            // 2. 필요 재료 문자열 생성
            if (nextStep.MaterialCosts != null && nextStep.MaterialCosts.Length > 0)
            {
                var mat = nextStep.MaterialCosts[0];
                // TODO: InventorySystem에서 실제 아이템 개수를 가져오는 로직 연동
                int currentMat = 0; 
                RequiredMaterialText.Value = $"{mat.Material?.Name ?? "재료"} {currentMat} / {mat.Count}";
            }
            else
            {
                RequiredMaterialText.Value = "필요 재료 없음";
            }

            // 3. 스탯 증가량 문자열 생성
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<color=#5BC0EB><b>[{equip.ItemData.Name}]</b></color>");
            sb.AppendLine("-------------------");
            
            // 현재 스탯 가져오기 (0강이면 기본 효과, 그 이상이면 해당 레벨의 누적 효과)
            var currentModifiers = equip.ItemData.GetModifiers(equip.UpgradeLevel);

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

                sb.AppendLine($"{GetStatNameKR(nextMod.Stat)} : {currValue} <color=#00FF00>▶ {nextMod.Value}</color>");
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

        public override void Unbind()
        {
            m_playerSystem = null;
            m_inventorySystem = null;
        }
    }
}
