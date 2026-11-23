using System;
using DesktopCompanion.Systems;
using DesktopCompanion.Data;
using DesktopCompanion.Domain.World;

namespace DesktopCompanion.Views
{
    public class StageViewModel : UIViewModelBase
    {
        private StageSystem m_stageSystem;
        private WorldSystemBridge m_worldBridge;
        private bool m_isCanceled = false;

        public readonly BindableProperty<string> CurrentStageName = new("위치 로드 중...");
        public readonly BindableProperty<bool> IsCancelButtonInteractable = new(false);

        public RelayCommand<int> MoveCommand { get; private set; }
        public RelayCommand CancelCommand { get; private set; }

        public override void Bind()
        {
            m_worldBridge = SystemManager.GetSystem<WorldSystemBridge>();
            m_stageSystem = SystemManager.GetSystem<StageSystem>();

            MoveCommand = new RelayCommand<int>(targetMapId =>
            {
                if (m_worldBridge != null) m_worldBridge.MoveToStage(targetMapId);
                else m_stageSystem?.MoveToStage(targetMapId);
            });

            CancelCommand = new RelayCommand(() =>
            {
                if (m_worldBridge != null) m_worldBridge.CancelTravel();
                else m_stageSystem?.CancelTravel();
            });

            if (m_worldBridge != null)
            {
                m_worldBridge.OnStageChanged += HandleStageChanged;
                m_worldBridge.OnTravelStarted += HandleTravelStarted;
                m_worldBridge.OnTravelCanceled += HandleTravelCanceled;
                m_worldBridge.OnVoyageStateChanged += HandleVoyageStateChanged;

                RefreshCurrentStage();
            }
            else if (m_stageSystem != null)
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
            if (m_worldBridge != null)
            {
                m_worldBridge.OnStageChanged -= HandleStageChanged;
                m_worldBridge.OnTravelStarted -= HandleTravelStarted;
                m_worldBridge.OnTravelCanceled -= HandleTravelCanceled;
                m_worldBridge.OnVoyageStateChanged -= HandleVoyageStateChanged;
                m_worldBridge = null;
            }

            if (m_stageSystem != null)
            {
                m_stageSystem.OnStageChanged -= HandleStageChanged;
                m_stageSystem.OnTravelStarted -= HandleTravelStarted;
                m_stageSystem.OnTravelCanceled -= HandleTravelCanceled;
                m_stageSystem.OnVoyageStateChanged -= HandleVoyageStateChanged;
                m_stageSystem = null;
            }
        }

        private void HandleTravelStarted(int targetMapId, float duration)
        {
            m_isCanceled = false;
            var currentState = m_worldBridge != null ? m_worldBridge.CurrentState : (m_stageSystem?.CurrentState ?? VoyageState.Departing);
            // 출항 가속(Departing) 동안에는 중단 버튼 비활성화, 이미 본 항해 중(Traveling) 목적지를 바꾼 경우 활성화 유지
            IsCancelButtonInteractable.Value = (currentState == VoyageState.Traveling);
        }

        private void HandleVoyageStateChanged(VoyageState state)
        {
            // 실제 본 항해 중(Traveling)일 때에만 중단 버튼 활성화
            IsCancelButtonInteractable.Value = (state == VoyageState.Traveling);
        }

        private void HandleStageChanged(int newStageDataId)
        {
            if (!m_isCanceled)
            {
                RefreshCurrentStage();
            }
            IsCancelButtonInteractable.Value = false;
        }

        private void HandleTravelCanceled()
        {
            m_isCanceled = true;
            CurrentStageName.Value = "항해 중지 (바다 위)";
            IsCancelButtonInteractable.Value = false;
        }

        private void RefreshCurrentStage()
        {
            var data = CurrentStageData;
            if (data != null)
            {
                CurrentStageName.Value = $"현재 지역: {data.Name}";
            }
        }

        public bool IsTraveling => m_worldBridge?.IsTraveling ?? (m_stageSystem?.IsTraveling ?? false);
        public bool IsCanceled => m_isCanceled;
        public float RemainingTravelTime => m_worldBridge?.RemainingTravelTime ?? (m_stageSystem?.RemainingTravelTime ?? 0f);
        public StageData CurrentStageData => m_worldBridge?.CurrentStageData ?? m_stageSystem?.CurrentStageData;
    }
}
