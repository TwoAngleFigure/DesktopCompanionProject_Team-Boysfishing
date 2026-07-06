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
        private readonly Dictionary<Type, SystemBase> m_systems = new();

        public SystemManager(EntityManager entityManager, DataManager dataManager)
        {
            m_entityManager = entityManager;
            m_dataManager = dataManager;
        }

        /// <summary>등록된 모든 System 열거(예: GameManager가 ISaveable 수집 시 사용).</summary>
        public IEnumerable<SystemBase> AllSystems => m_systems.Values;

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
        }

        /// <summary>등록된 System을 타입으로 조회한다(System 간 참조 포함).</summary>
        public T GetSystem<T>() where T : SystemBase
            => m_systems.TryGetValue(typeof(T), out var system) ? (T)system : null;

        /// <summary>모든 System 등록 후 일괄 초기화(참조가 안전해지는 시점).</summary>
        public void InitializeAll()
        {
            foreach (var system in m_systems.Values)
            {
                system.Initialize();
            }
        }
    }
}
