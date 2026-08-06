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

        public RelayCommand<int> SelectAndMoveCommand { get; private set; }

        public override void Bind()
        {
            m_stageSystem = SystemManager.GetSystem<StageSystem>();

            SelectAndMoveCommand = new RelayCommand<int>(stageId =>
            {
                m_stageSystem?.MoveToStage(stageId);
            });
        }

        public override void Unbind()
        {
            m_stageSystem = null;
        }

        public IReadOnlyList<StageData> GetAllStageDatas()
        {
            if (m_stageSystem == null)
            {
                return new List<StageData>();
            }

            return m_stageSystem.GetAllStageDatas();
        }
    }
}
