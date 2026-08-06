using System;
using DesktopCompanion.Systems;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    public class StageViewModel : UIViewModelBase
    {
        private StageSystem m_stageSystem;

        public readonly BindableProperty<string> CurrentStageName = new("위치 로드 중...");
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
                m_stageSystem.OnVoyageStateChanged += HandleVoyageStateChanged;

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
                m_stageSystem.OnVoyageStateChanged -= HandleVoyageStateChanged;
            }
            m_stageSystem = null;
        }

        private void HandleTravelStarted(int targetMapId, float duration)
        {
            // 출항 시퀀스(Departing 9초) 동안에는 중단 버튼 비활성화 (기획 의도 준수 및 팝인 방지)
            IsCancelButtonInteractable.Value = false;
        }

        private void HandleVoyageStateChanged(VoyageState state)
        {
            // 실제 본 항해 중(Traveling)일 때에만 중단 버튼 활성화!
            // Departing(출항) 및 Arriving(도착), Anchored(정박), Stopping(중지 중) 동안에는 비활성화
            IsCancelButtonInteractable.Value = (state == VoyageState.Traveling);
        }

        private void HandleStageChanged(int newStageDataId)
        {
            RefreshCurrentStage();
            IsCancelButtonInteractable.Value = false;
        }

        private void HandleTravelCanceled()
        {
            CurrentStageName.Value = "항해 중지 (바다 위)";
            IsCancelButtonInteractable.Value = false;
        }

        private void RefreshCurrentStage()
        {
            if (m_stageSystem != null && m_stageSystem.CurrentStageData != null)
            {
                CurrentStageName.Value = $"현재 지역: {m_stageSystem.CurrentStageData.Name}";
            }
        }

        public bool IsTraveling => m_stageSystem?.IsTraveling ?? false;
        public bool IsCanceled => SystemManager.GetSystem<VoyageSystem>()?.IsCanceled ?? false;
        public float RemainingTravelTime => m_stageSystem?.RemainingTravelTime ?? 0f;
        public StageData CurrentStageData => m_stageSystem?.CurrentStageData;
    }
}