using UnityEngine;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 팀원용 예제 템플릿. WorldViewBase를 상속해 '자기 System'만 구독하는 방법을 보여준다.
    /// 복사해서 각자 도메인(예: FishingWorldView)으로 바꿔 쓰면 된다.
    ///
    /// 규칙:
    /// - 씬 오브젝트에 이 컴포넌트를 붙이면 OnEnable에서 스스로 WorldManager에 등록된다.
    /// - 구독(+=)은 Bind(), 해제(-=)는 Unbind()에만 둔다. WorldManager는 건드리지 않는다.
    /// - Action 페이로드 표준: Action&lt;EntityHandle, …원시값&gt; (Entity 참조는 받지 않는다).
    /// </summary>
    public class SampleStageWorldView : WorldViewBase
    {
        private StageSystem m_stageSystem;

        // 의존성 주입 후 WorldManager가 호출한다. 여기서 '자기 System'만 구독한다.
        public override void Bind()
        {
            m_stageSystem = SystemManager.GetSystem<StageSystem>();
            if (m_stageSystem == null)
            {
                Debug.LogWarning("[SampleStageWorldView] StageSystem 없음 — 구독 생략");
                return;
            }

            m_stageSystem.OnStageChanged += HandleStageChanged;
            m_stageSystem.OnTravelStarted += HandleTravelStarted;
        }

        // Bind와 1:1 대칭. OnDisable에서 호출된다.
        public override void Unbind()
        {
            if (m_stageSystem == null)
            {
                return;
            }

            m_stageSystem.OnStageChanged -= HandleStageChanged;
            m_stageSystem.OnTravelStarted -= HandleTravelStarted;
            m_stageSystem = null;
        }

        // ── 핸들러: System 상태 변화 → 자기 월드 오브젝트 갱신 ──

        private void HandleStageChanged(int stageDataId)
        {
            // TODO: 새 스테이지 오브젝트를 AssetProvider로 생성/배치.
            Debug.Log($"[SampleStageWorldView] 스테이지 변경: {stageDataId}");
        }

        private void HandleTravelStarted(int targetStageDataId, float duration)
        {
            // TODO: 현재 오브젝트를 카메라 밖으로 이동(퇴장 연출) 시작.
            Debug.Log($"[SampleStageWorldView] 이동 시작: target={targetStageDataId}, duration={duration:0.00}s");
        }
    }
}
