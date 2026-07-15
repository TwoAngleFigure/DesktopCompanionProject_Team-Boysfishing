using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.Views
{
    public class FishingVM : UIViewModelBase
    {
        private FishingSystem m_fishingSystem;

        public readonly BindableProperty<string> StateText = new("대기 중");
        public readonly BindableProperty<string> HpText = new("-");
        public readonly BindableProperty<float> HpRatio = new(0f);
        public readonly BindableProperty<float> BattleTimeRemainingRatio = new(0f);
        public readonly BindableProperty<bool> IsBattleGaugeVisible = new(false);
        public readonly BindableProperty<string> ToggleButtonText = new("낚시 시작");

        public RelayCommand ToggleFishingState { get; private set; }
        public RelayCommand ManualAttack { get; private set; }

        // Debug HUD
        public readonly BindableProperty<string> DebugWaitTimeText = new("입질 대기: -");

        public override void Bind()
        {
            m_fishingSystem = SystemManager.GetSystem<FishingSystem>();

            if (m_fishingSystem == null)
            {
                StateText.Value = "낚시 시스템 없음";
                return;
            }

            m_fishingSystem.OnStateChanged += HandleStateChanged;
            m_fishingSystem.OnBattleHpChanged += HandleBattleHpChanged;
            ToggleFishingState = new RelayCommand(ToggleFishing);
            ManualAttack = new RelayCommand(ExecuteManualAttack, CanManualAttack);

            HandleStateChanged(m_fishingSystem.State);
            RefreshRuntimeValues();
        }

        public override void Unbind()
        {
            if (m_fishingSystem != null)
            {
                m_fishingSystem.OnStateChanged -= HandleStateChanged;
                m_fishingSystem.OnBattleHpChanged -= HandleBattleHpChanged;
            }

            m_fishingSystem = null;
        }

        public void RefreshRuntimeValues()
        {
            if (m_fishingSystem == null)
            {
                DebugWaitTimeText.Value = "대기 시간: -";
                BattleTimeRemainingRatio.Value = 0f;
                return;
            }

            if (m_fishingSystem.State == FishingState.Waiting)
            {
                DebugWaitTimeText.Value =
                    $"대기 시간: " +
                    $"{m_fishingSystem.WaitDuration:0.0}초 / " +
                    $"{ClampZero(m_fishingSystem.WaitTimeRemaining):0.0}초";
            }
            else
            {
                DebugWaitTimeText.Value = "대기 시간: -";
            }

            if (m_fishingSystem.State == FishingState.Battling &&
                m_fishingSystem.BattleDuration > 0f)
            {
                BattleTimeRemainingRatio.Value = Mathf.Clamp01(
                    m_fishingSystem.BattleTimeRemaining /
                    m_fishingSystem.BattleDuration);
            }
            else
            {
                BattleTimeRemainingRatio.Value = 0f;
            }
        }

        private void HandleStateChanged(FishingState state)
        {
            switch (state)
            {
                case FishingState.Stopped:
                    StateText.Value = "휴식 중";
                    HpText.Value = "";
                    HpRatio.Value = 0f;
                    BattleTimeRemainingRatio.Value = 0f;
                    IsBattleGaugeVisible.Value = false;
                    ToggleButtonText.Value = "낚시 시작";
                    break;

                case FishingState.Waiting:
                    StateText.Value = "낚시 중";
                    HpText.Value = "";
                    HpRatio.Value = 0f;
                    BattleTimeRemainingRatio.Value = 0f;
                    IsBattleGaugeVisible.Value = false;

                    ToggleButtonText.Value = "낚시 중지";
                    break;

                case FishingState.Battling:
                    StateText.Value = "낚시 중";
                    BattleTimeRemainingRatio.Value = 1f;
                    IsBattleGaugeVisible.Value = true;
                    ToggleButtonText.Value = "낚시 중지";
                    break;
            }
        }

        private void HandleBattleHpChanged(EntityHandle fishHandle, int currentHp, int maxHp)
        {
            HpText.Value = $"{currentHp}";
            HpRatio.Value = maxHp > 0 ? (float)currentHp / maxHp : 0f;
        }


        private void ToggleFishing()
        {
            if (m_fishingSystem == null)
            {
                return;
            }

            if (m_fishingSystem.State == FishingState.Stopped)
            {
                m_fishingSystem.StartFishing();
            }
            else
            {
                m_fishingSystem.StopFishing();
            }
        }

        private bool CanManualAttack()
        {
            return m_fishingSystem != null && m_fishingSystem.State == FishingState.Battling;
        }

        private void ExecuteManualAttack()
        {
            m_fishingSystem.ManualAttack();
        }

        private float ClampZero(float value)
        {
            return value > 0f ? value : 0f;
        }
    }
}
