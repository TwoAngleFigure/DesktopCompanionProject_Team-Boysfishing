using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;
using UnityEngine; // Vector2를 사용하기 위해 추가

namespace DesktopCompanion.Views
{
    public class WorldMapViewModel : UIViewModelBase
    {
        private StageSystem m_stageSystem;
        private VoyageSystem m_voyageSystem; // ✨ 이동 좌표를 가져올 시스템 추가

        public RelayCommand<int> SelectAndMoveCommand { get; private set; }

        public override void Bind()
        {
            m_stageSystem = SystemManager.GetSystem<StageSystem>();
            m_voyageSystem = SystemManager.GetSystem<VoyageSystem>(); // ✨ 연결

            SelectAndMoveCommand = new RelayCommand<int>(stageId =>
            {
                m_stageSystem?.MoveToStage(stageId);
            });
        }

        public override void Unbind()
        {
            m_stageSystem = null;
            m_voyageSystem = null; // ✨ 해제
        }

        public IReadOnlyList<StageData> GetAllStageDatas()
        {
            if (m_stageSystem == null) return new List<StageData>();
            return m_stageSystem.GetAllStageDatas();
        }

        // ✨ View(UI)가 매 프레임 읽어갈 배의 실시간 논리적 위치
        public Vector2 CurrentShipPosition => m_voyageSystem?.CurrentLogicalPosition ?? Vector2.zero;
        public bool IsSystemReady => m_voyageSystem != null;
    }
}