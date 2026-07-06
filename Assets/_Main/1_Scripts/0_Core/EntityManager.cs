using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Core
{
    /// <summary>
    /// 모든 Entity의 생명주기(생성·소멸)와 조회를 중앙에서 담당하는 레지스트리.
    /// 도메인(아이템/전투 등)을 알지 못하며, Data 타입→Entity 매핑(팩토리)은 외부에서 등록받는다.
    /// EntityHandle 발급 단일 지점.
    /// </summary>
    public class EntityManager
    {
        private readonly DataManager m_dataManager;
        private readonly Dictionary<EntityHandle, Entity> m_entities = new();
        private readonly Dictionary<Type, Func<EntityHandle, GameData, Entity>> m_factories = new();

        /// <summary>Entity 소멸 시 발행. System들이 자기 소속에서 해당 핸들을 정리한다.</summary>
        public event Action<EntityHandle> OnEntityDestroyed;

        public EntityManager(DataManager dataManager) => m_dataManager = dataManager;

        /// <summary>Data 타입 → Entity 생성 매핑 등록. GameManager(조립 루트)가 부팅 시 호출.</summary>
        public void RegisterFactory(Type dataType, Func<EntityHandle, GameData, Entity> factory)
            => m_factories[dataType] = factory;

        /// <summary>Data로부터 Entity를 생성·등록하고 핸들을 반환한다.</summary>
        public EntityHandle Create<TData>(int dataId) where TData : GameData
        {
            var data = m_dataManager.GetData<TData>(dataId);
            if (data == null)
            {
                Debug.LogError($"[EntityManager] Data 없음 {typeof(TData).Name} id={dataId}");
                return default;
            }
            if (!m_factories.TryGetValue(data.GetType(), out var factory))
            {
                Debug.LogError($"[EntityManager] 팩토리 미등록 type={data.GetType().Name}");
                return default;
            }

            var handle = EntityHandle.New();
            var entity = factory(handle, data);
            m_entities.Add(handle, entity);
            return handle;
        }

        /// <summary>
        /// 세이브 로드 전용(D15): 저장된 handle로 Entity를 재등록해 세이브 간 참조(장착 목록 등)를 보존한다.
        /// 신규 생성은 Create를 사용할 것.
        /// </summary>
        public EntityHandle Restore<TData>(EntityHandle handle, int dataId) where TData : GameData
        {
            if (handle.Value == Guid.Empty)
            {
                Debug.LogError("[EntityManager] Restore: 유효하지 않은 handle");
                return default;
            }
            if (m_entities.ContainsKey(handle))
            {
                Debug.LogError($"[EntityManager] Restore: 이미 등록된 handle {handle}");
                return handle;
            }

            var data = m_dataManager.GetData<TData>(dataId);
            if (data == null)
            {
                Debug.LogError($"[EntityManager] Restore: Data 없음 {typeof(TData).Name} id={dataId}");
                return default;
            }
            if (!m_factories.TryGetValue(data.GetType(), out var factory))
            {
                Debug.LogError($"[EntityManager] Restore: 팩토리 미등록 type={data.GetType().Name}");
                return default;
            }

            var entity = factory(handle, data);
            m_entities.Add(handle, entity);
            return handle;
        }

        public bool TryGet(EntityHandle id, out Entity entity) => m_entities.TryGetValue(id, out entity);

        public Entity Get(EntityHandle id) => m_entities.TryGetValue(id, out var entity) ? entity : null;

        public T Get<T>(EntityHandle id) where T : Entity => Get(id) as T;

        public void Destroy(EntityHandle id)
        {
            if (m_entities.Remove(id))
            {
                OnEntityDestroyed?.Invoke(id);
            }
        }
    }
}