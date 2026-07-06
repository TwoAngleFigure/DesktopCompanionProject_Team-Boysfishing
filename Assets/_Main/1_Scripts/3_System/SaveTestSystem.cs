using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// ★ Save/Load 파이프라인 검증용 임시 System — 검증 완료 후 제거해도 됨.
    /// 검증 항목:
    ///  ① 상태 왕복: 실행 횟수(launchCount)가 세션을 넘어 누적되는가
    ///  ② handle 보존: 저장했던 EntityHandle 그대로 Entity가 복원되는가(EntityManager.Restore)
    ///  ③ Data 참조 복원: dataId로 정의를 다시 연결해 값(이름 등)을 읽을 수 있는가
    ///
    /// 사용법: Play → 콘솔 로그 확인 → 종료(자동 저장) → 다시 Play → "복원" 로그 확인.
    /// </summary>
    public class SaveTestSystem : SystemBase, ISaveable
    {
        private int m_completedSessions;              // 지금까지 "완료(저장)된" 세션 수
        private EntityHandle? m_fishHandle;           // 세션 간 보존을 검증할 물고기
        private int m_fishDataId;

        public string SaveId => "save_test";
        public Type StateType => typeof(SaveTestState);

        [Serializable]
        public class SaveTestState
        {
            public int launchCount;
            public string fishHandle;   // EntityHandle.ToString("N") — 저장 경계에서만 문자열화
            public int fishDataId;
        }

        public override void Initialize()
        {
            // 기본 상태(첫 실행 가정). 세이브가 있으면 직후 RestoreState가 덮어쓴다(§15.3 복원 원칙).
            m_completedSessions = 0;
            m_fishHandle = null;
            Debug.Log("[SaveTestSystem] Initialize — 기본 상태(세이브 있으면 곧 복원됨)");
        }

        // ── 저장: 종료 시 SaveManager가 호출 ──
        public object CaptureState()
        {
            // 첫 세션이면 검증용 물고기를 하나 생성해 handle 보존 테스트 대상으로 삼는다.
            if (!m_fishHandle.HasValue)
            {
                IReadOnlyList<ItemData_Fish> allFish = DataManager.GetAll<ItemData_Fish>();
                if (allFish.Count > 0)
                {
                    m_fishDataId = allFish[0].ID;
                    m_fishHandle = EntityManager.Create<ItemData_Fish>(m_fishDataId);
                    Debug.Log($"[SaveTestSystem] 검증용 물고기 생성: {allFish[0].Name} handle={m_fishHandle}");
                }
                else
                {
                    Debug.LogWarning("[SaveTestSystem] ItemData_Fish 데이터가 없어 handle 검증은 생략(카운터만 저장)");
                }
            }

            var state = new SaveTestState
            {
                launchCount = m_completedSessions + 1,   // 이번 세션 포함
                fishHandle = m_fishHandle?.ToString(),
                fishDataId = m_fishDataId,
            };
            Debug.Log($"[SaveTestSystem] 저장: launchCount={state.launchCount}");
            return state;
        }

        // ── 복원: 부팅 시 SaveManager가 호출(세이브 있을 때만) ──
        public void RestoreState(object state)
        {
            var saved = (SaveTestState)state;
            m_completedSessions = saved.launchCount;
            Debug.Log($"[SaveTestSystem] 복원: 이전까지 완료된 세션 수 = {m_completedSessions}");

            if (!string.IsNullOrEmpty(saved.fishHandle) && EntityHandle.TryParse(saved.fishHandle, out EntityHandle handle))
            {
                m_fishDataId = saved.fishDataId;
                EntityHandle restored = EntityManager.Restore<ItemData_Fish>(handle, saved.fishDataId);
                var fish = EntityManager.Get<Entity_Fish>(restored);

                if (fish != null && restored.Equals(handle))
                {
                    m_fishHandle = restored;
                    Debug.Log($"[SaveTestSystem] ✅ 물고기 복원 성공: '{fish.Name}' handle 동일 보존 확인 ({handle})");
                }
                else
                {
                    Debug.LogError("[SaveTestSystem] ❌ 물고기 복원 실패 — handle/데이터 확인 필요");
                }
            }
        }
    }
}
