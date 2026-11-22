using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;
using UnityEngine;

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

        // View(UI)가 매 프레임 읽어갈 배의 실시간 논리적 위치
        public Vector2 CurrentShipPosition => m_worldBridge != null
            ? m_worldBridge.CurrentLogicalPosition
            : (m_stageSystem != null ? m_stageSystem.CurrentLogicalPosition : Vector2.zero);

        public bool IsSystemReady => m_worldBridge != null || m_stageSystem != null;
    }
}
