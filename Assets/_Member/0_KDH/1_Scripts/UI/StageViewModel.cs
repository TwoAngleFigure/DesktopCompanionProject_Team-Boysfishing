using System;
using DesktopCompanion.Systems;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    public class StageViewModel : UIViewModelBase
    {
        private StageSystem m_stageSystem;

        public readonly BindableProperty<string> CurrentStageName = new("Loading Location...");
        public readonly BindableProperty<bool> IsCancelButtonInteractable = new(false);

        public RelayCommand<int> MoveCommand { get; private set; }
        public RelayCommand CancelCommand { get; private set; }

        public override void Bind()
        {
            m_stageSystem = SystemManager.GetSystem<StageSystem>();

            MoveCommand = new RelayCommand<int>(targetMapId => m_stageSystem?.MoveToStage(targetMapId));
            CancelCommand = new RelayCommand(() => m_stageSystem?.CancelTravel());

            if (m_stageSystem != null)
            {
                m_stageSystem.OnStageChanged += HandleStageChanged;
                m_stageSystem.OnTravelStarted += HandleTravelStarted;
                m_stageSystem.OnTravelCanceled += HandleTravelCanceled;

                RefreshCurrentStage();
            }
        }

        public override void Unbind()
        {
            if (m_stageSystem != null)
            {
                m_stageSystem.OnStageChanged -= HandleStageChanged;
                m_stageSystem.OnTravelStarted -= HandleTravelStarted;
                m_stageSystem.OnTravelCanceled -= HandleTravelCanceled;
            }
            m_stageSystem = null;
        }

        private void HandleTravelStarted(int targetMapId, float duration)
        {
            IsCancelButtonInteractable.Value = true;
        }

        private void HandleStageChanged(int newStageDataId)
        {
            RefreshCurrentStage();
            IsCancelButtonInteractable.Value = false;
        }

        private void HandleTravelCanceled()
        {
            CurrentStageName.Value = "Stop voyage (above the sea)";
            IsCancelButtonInteractable.Value = false;
        }

        private void RefreshCurrentStage()
        {
            if (m_stageSystem != null && m_stageSystem.CurrentStageData != null)
            {
                CurrentStageName.Value = $"Current Stage: {m_stageSystem.CurrentStageData.Name}";
            }
        }

        public bool IsTraveling => m_stageSystem?.IsTraveling ?? false;
        public float RemainingTravelTime => m_stageSystem?.RemainingTravelTime ?? 0f;
    }
}