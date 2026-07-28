using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.Views
{
    public class FishingDebugController : UIViewBase
    {
        [Header("Forced Catch Override")]
        [SerializeField] private int m_forcedBattleFishDataId;
        [SerializeField] private float m_forcedSize;

        [Tooltip("Play 중 체크하면 모든 전투 물고기에 강제 ID와 길이를 적용합니다.")]
        [SerializeField] private bool m_enableForcedCatch;

        private FishingSystem m_fishingSystem;
        private bool m_isForcedCatchApplied;
        private int m_appliedBattleFishDataId;
        private float m_appliedSize;

        public override void Bind()
        {
            m_fishingSystem = SystemManager.GetSystem<FishingSystem>();
        }

        public override void Unbind()
        {
#if UNITY_EDITOR
            ClearForcedCatch();
#endif
            m_fishingSystem = null;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (m_fishingSystem == null)
            {
                return;
            }

            if (!m_enableForcedCatch)
            {
                ClearForcedCatch();
                return;
            }

            if (m_isForcedCatchApplied &&
                m_appliedBattleFishDataId == m_forcedBattleFishDataId &&
                Mathf.Approximately(m_appliedSize, m_forcedSize))
            {
                return;
            }

            ApplyForcedCatch();
#endif
        }

        private void ApplyForcedCatch()
        {
#if UNITY_EDITOR
            if (!m_fishingSystem.TrySetDebugCatchOverride(m_forcedBattleFishDataId, m_forcedSize))
            {
                return;
            }

            m_isForcedCatchApplied = true;
            m_appliedBattleFishDataId = m_forcedBattleFishDataId;
            m_appliedSize = m_forcedSize;
#endif
        }

        private void ClearForcedCatch()
        {
#if UNITY_EDITOR
            if (!m_isForcedCatchApplied || m_fishingSystem == null)
            {
                return;
            }

            m_fishingSystem.ClearDebugCatchOverride();
            m_isForcedCatchApplied = false;
#endif
        }
    }
}
