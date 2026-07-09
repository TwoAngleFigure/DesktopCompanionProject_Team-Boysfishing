using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class FishingVM : UIViewModelBase
    {
        private FishingSystem m_fishingSystem;

        public readonly BindableProperty<string> StateText = new("Stopped");
        public readonly BindableProperty<string> BattleFishNameText = new("-");
        public readonly BindableProperty<string> HpText = new("-");
        public readonly BindableProperty<float> HpRatio = new(0f);
        public readonly BindableProperty<string> ResultText = new("");
        public readonly BindableProperty<string> ToggleButtonText = new("Start Fishing");

        public RelayCommand ToggleFishingState { get; private set; }

        // Debug HUD
        public readonly BindableProperty<string> DebugWaitTimeText = new("WaitTime: -");

        public readonly BindableProperty<string> DebugBattleTimeText = new("BattleTime: -");

        public override void Bind()
        {
            m_fishingSystem = SystemManager.GetSystem<FishingSystem>();

            if (m_fishingSystem == null)
            {
                StateText.Value = "FishingSystem is null";
                return;
            }

            m_fishingSystem.OnStateChanged += HandleStateChanged;
            m_fishingSystem.OnBattleStarted += HandleBattleStarted;
            m_fishingSystem.OnBattleHpChanged += HandleBattleHpChanged;
            m_fishingSystem.OnFishCaught += HandleFishCaught;
            m_fishingSystem.OnBattleFailed += HandleBattleFailed;
            ToggleFishingState = new RelayCommand(ToggleFishing);

            HandleStateChanged(m_fishingSystem.State);
            RefreshDebugTime();
        }

        public override void Unbind()
        {
            if (m_fishingSystem != null)
            {
                m_fishingSystem.OnStateChanged -= HandleStateChanged;
                m_fishingSystem.OnBattleStarted -= HandleBattleStarted;
                m_fishingSystem.OnBattleHpChanged -= HandleBattleHpChanged;
                m_fishingSystem.OnFishCaught -= HandleFishCaught;
                m_fishingSystem.OnBattleFailed -= HandleBattleFailed;
            }

            m_fishingSystem = null;
        }

        // Debug HUD
        public void RefreshDebugTime()
        {
            if (m_fishingSystem == null)
            {
                DebugWaitTimeText.Value = "WaitTime: -";
                DebugBattleTimeText.Value = "BattleTime: -";
                return;
            }

            DebugWaitTimeText.Value =
                $"WaitTime: {m_fishingSystem.WaitDuration:0.0}s / {ClampZero(m_fishingSystem.WaitTimeRemaining):0.0}s";

            DebugBattleTimeText.Value =
                $"BattleTime: {m_fishingSystem.BattleDuration:0.0}s / {ClampZero(m_fishingSystem.BattleTimeRemaining):0.0}s";
        }

        private void HandleStateChanged(FishingState state)
        {
            switch (state)
            {
                case FishingState.Stopped:
                    StateText.Value = $"CurrentState: {state}";
                    BattleFishNameText.Value = $"FishName: - ";
                    HpText.Value = "-";
                    HpRatio.Value = 0f;
                    ToggleButtonText.Value = "Start Fishing";
                    break;

                case FishingState.Waiting:
                    StateText.Value = $"CurrentState: {state}";
                    BattleFishNameText.Value = $"FishName: - ";
                    HpText.Value = "-";
                    HpRatio.Value = 0f;
                    ToggleButtonText.Value = "Stop Fishing";
                    break;

                case FishingState.Battling:
                    StateText.Value = $"CurrentState: {state}";
                    ToggleButtonText.Value = "Stop Fishing";
                    break;
            }
        }

        private void HandleBattleStarted(EntityHandle fishHandle, string fishName)
        {
            ResultText.Value = "Result: -";
            BattleFishNameText.Value = $"FishName: {fishName}"; ;
        }

        private void HandleBattleHpChanged(EntityHandle fishHandle, int currentHp, int maxHp)
        {
            HpText.Value = $"{currentHp} / {maxHp}";
            HpRatio.Value = maxHp > 0 ? (float)currentHp / maxHp : 0f;
        }

        private void HandleFishCaught(EntityHandle fishHandle, string fishName)
        {
            ResultText.Value = $"Result: {fishName} Caught";
            BattleFishNameText.Value = "BattleFishNameText: -";
            HpText.Value = "-";
            HpRatio.Value = 0f;
        }

        private void HandleBattleFailed(EntityHandle fishHandle)
        {
            ResultText.Value = "Result: Failed";
            BattleFishNameText.Value = "BattleFishNameText: -";
            HpText.Value = "-";
            HpRatio.Value = 0f;
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

        private float ClampZero(float value)
        {
            return value > 0f ? value : 0f;
        }
    }
}
