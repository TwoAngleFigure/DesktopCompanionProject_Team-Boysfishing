using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Core
{
    /// <summary>
    /// 모든 System 인스턴스를 보유·초기화하고 타입으로 접근을 제공한다.
    /// System에 EntityManager와 자기 자신(SystemManager)을 주입한다.
    /// 구체 System 타입은 모른다(등록은 GameManager가 담당) — EntityManager의 팩토리 등록과 동일한 패턴.
    /// <see cref="GetSystem{T}"/>가 System 간 참조 획득 통로(D11).
    /// </summary>
    public class SystemManager
    {
        private readonly EntityManager m_entityManager;
        private readonly DataManager m_dataManager;
        private readonly Dictionary<Type, SystemBase> m_systems = new();   // 타입 조회용
        private readonly List<SystemBase> m_ordered = new();                // 등록 순서 보존(초기화·저장 순서)
        private readonly List<ITickable> m_tickables = new();               // 매 프레임 Tick 대상(등록 순서)

        public SystemManager(EntityManager entityManager, DataManager dataManager)
        {
            m_entityManager = entityManager;
            m_dataManager = dataManager;
        }

        /// <summary>등록된 모든 System을 등록 순서대로 열거(예: GameManager가 ISaveable 수집 시 사용).</summary>
        public IEnumerable<SystemBase> AllSystems => m_ordered;

        /// <summary>System 인스턴스를 등록하고 의존성을 주입한다. GameManager가 부팅 시 호출.</summary>
        public void Register<T>(T system) where T : SystemBase
        {
            var type = typeof(T);
            if (m_systems.ContainsKey(type))
            {
                Debug.LogError($"[SystemManager] 이미 등록된 System: {type.Name}");
                return;
            }
            system.Inject(m_entityManager, m_dataManager, this);
            m_systems.Add(type, system);
            m_ordered.Add(system);
            if (system is ITickable tickable)
            {
                m_tickables.Add(tickable);
            }
        }

        /// <summary>등록된 System을 타입으로 조회한다(System 간 참조 포함).</summary>
        public T GetSystem<T>() where T : SystemBase
            => m_systems.TryGetValue(typeof(T), out var system) ? (T)system : null;

        /// <summary>
        /// Phase 1(생산): 각 System이 자기 원시 상태만 구성한다(등록 순서대로).
        /// 이 단계 뒤에 SaveData 복원(RestoreState)이 원시 상태를 덮어쓰고, 그다음 Phase 2가 실행된다.
        /// </summary>
        public void InitializePhase1()
        {
            foreach (var system in m_ordered)
            {
                system.Initialize();
            }
        }

        /// <summary>
        /// Phase 2(소비·배선·파생): 타 System 조회·구독·파생 계산.
        /// SaveData 복원 이후에 호출되므로 파생 계산이 복원된 값을 반영한다.
        /// 전원이 Phase 1을 마친 뒤 실행되므로 크로스 System 의존이 순서와 무관하게 안전하다.
        /// </summary>
        public void InitializePhase2()
        {
            foreach (var system in m_ordered)
            {
                system.PostInitialize();
            }
        }

        /// <summary>ITickable을 구현한 System을 등록 순서대로 매 프레임 갱신한다(GameManager.Update가 중계).</summary>
        public void TickAll(float deltaTime)
        {
            for (int i = 0; i < m_tickables.Count; i++)
            {
                m_tickables[i].Tick(deltaTime);
            }
        }
    }
}
