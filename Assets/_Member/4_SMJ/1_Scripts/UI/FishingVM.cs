using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class FishingVM : UIViewModelBase
    {
        private FishingSystem m_fishingSystem;

        public readonly BindableProperty<string> StateText = new("Stopped");
        public readonly BindableProperty<string> HpText = new("-");
        public readonly BindableProperty<float> HpRatio = new(0f);
        public readonly BindableProperty<string> ResultText = new("");
        public readonly BindableProperty<string> CatchInfoText = new("획득 정보: -");
        public readonly BindableProperty<string> ToggleButtonText = new("낚시 시작");

        public RelayCommand ToggleFishingState { get; private set; }
        public RelayCommand ManualAttack { get; private set; }

        // Debug HUD
        public readonly BindableProperty<string> DebugWaitTimeText = new("입질 대기: -");

        public readonly BindableProperty<string> DebugBattleTimeText = new("전투 시간: -");

        public override void Bind()
        {
            m_fishingSystem = SystemManager.GetSystem<FishingSystem>();

            if (m_fishingSystem == null)
            {
                StateText.Value = "낚시 시스템 없음";
                return;
            }

            m_fishingSystem.OnStateChanged += HandleStateChanged;
            m_fishingSystem.OnBattleStarted += HandleBattleStarted;
            m_fishingSystem.OnBattleHpChanged += HandleBattleHpChanged;
            m_fishingSystem.OnFishCaught += HandleFishCaught;
            m_fishingSystem.OnBattleFailed += HandleBattleFailed;
            ToggleFishingState = new RelayCommand(ToggleFishing);
            ManualAttack = new RelayCommand(ExecuteManualAttack, CanManualAttack);

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
                DebugWaitTimeText.Value = "대기 시간: -";
                DebugBattleTimeText.Value = "전투 시간: -";
                return;
            }

            DebugWaitTimeText.Value =
                $"대기 시간: {m_fishingSystem.WaitDuration:0.0}초 / {ClampZero(m_fishingSystem.WaitTimeRemaining):0.0}초";

            DebugBattleTimeText.Value =
                $"전투 시간: {m_fishingSystem.BattleDuration:0.0}초 / {ClampZero(m_fishingSystem.BattleTimeRemaining):0.0}초";
        }

        private void HandleStateChanged(FishingState state)
        {
            switch (state)
            {
                case FishingState.Stopped:
                    StateText.Value = $"현재 상태: {state}";
                    HpText.Value = "-";
                    HpRatio.Value = 0f;
                    ToggleButtonText.Value = "낚시 시작";
                    break;

                case FishingState.Waiting:
                    StateText.Value = $"현재 상태: {state}";
                    HpText.Value = "-";
                    HpRatio.Value = 0f;
                    ToggleButtonText.Value = "낚시 중지";
                    break;

                case FishingState.Battling:
                    StateText.Value = $"현재 상태: {state}";
                    ToggleButtonText.Value = "낚시 중지";
                    break;
            }
        }

        private void HandleBattleStarted(EntityHandle fishHandle)
        {
            ResultText.Value = "결과: -";
            CatchInfoText.Value = "획득 정보: -";
        }

        private void HandleBattleHpChanged(EntityHandle fishHandle, int currentHp, int maxHp)
        {
            HpText.Value = $"{currentHp} / {maxHp}";
            HpRatio.Value = maxHp > 0 ? (float)currentHp / maxHp : 0f;
        }

        private void HandleFishCaught(EntityHandle fishHandle)
        {
            Entity_Fish fish = EntityManager.Get<Entity_Fish>(fishHandle);

            if (fish == null)
            {
                ResultText.Value = "결과: 포획 성공";
                CatchInfoText.Value = "획득 정보: -";
                HpText.Value = "-";
                HpRatio.Value = 0f;
                return;
            }

            ResultText.Value = $"결과: {fish.Name} 포획 성공";
            CatchInfoText.Value =                
                $"크기: {fish.Size:0.00}\n" +
                $"품질: {fish.Quality}\n" +
                $"희귀도: {fish.Rarity}";
            HpText.Value = "-";
            HpRatio.Value = 0f;
        }

        private void HandleBattleFailed(EntityHandle fishHandle)
        {
            ResultText.Value = "결과: 포획 실패";
            CatchInfoText.Value = "획득 정보: -";
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
