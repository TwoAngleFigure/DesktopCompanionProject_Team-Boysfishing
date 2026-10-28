using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;

namespace DesktopCompanion.Views
{
    public class WorldMapViewModel : UIViewModelBase
    {
        private StageSystem m_stageSystem;
        private WorldSystemBridge m_worldBridge;

        public RelayCommand<int> SelectAndMoveCommand { get; private set; }

        public override void Bind()
        {
            m_worldBridge = SystemManager.GetSystem<WorldSystemBridge>();
            m_stageSystem = SystemManager.GetSystem<StageSystem>();

            SelectAndMoveCommand = new RelayCommand<int>(stageId =>
            {
                if (m_worldBridge != null) m_worldBridge.MoveToStage(stageId);
                else m_stageSystem?.MoveToStage(stageId);
            });
        }

        public override void Unbind()
        {
            m_worldBridge = null;
            m_stageSystem = null;
        }

        public IReadOnlyList<StageData> GetAllStageDatas()
        {
            if (m_worldBridge != null)
            {
                return m_worldBridge.GetAllStageDatas();
            }

            if (m_stageSystem != null)
            {
                return m_stageSystem.GetAllStageDatas();
            }

            return new List<StageData>();
        }
    }
}
