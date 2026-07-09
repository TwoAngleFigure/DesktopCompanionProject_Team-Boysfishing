using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;

namespace DesktopCompanion.Core
{
    /// <summary>
    /// 모든 <see cref="GameData"/>(SO)를 타입별로 인덱싱해 보관하는 읽기 전용 공급자.
    /// 기본은 Resources 일괄 로드, 테스트 시 DataSet 주입(수동 Play 검증, D9).
    /// </summary>
    public class DataManager
    {
        // 타입 → (ID → data). 타입이 다르면 ID가 겹쳐도 무방.
        private readonly Dictionary<Type, Dictionary<int, GameData>> m_store = new();

        /// <summary>
        /// 소스 우선순위(D5·D14): ① injected(테스트 주입) ② StreamingAssets/Data의 JSON ③ Resources/Data의 SO.
        /// 소비자는 소스를 모른다 — 서버 전환 시 JSON 경로만 교체.
        /// </summary>
        public void Load(IEnumerable<GameData> injected = null)
        {
            m_store.Clear();

            IEnumerable<GameData> source = injected;
            string sourceName = "테스트 주입 DataSet";
            if (source == null)
            {
                string jsonDirectory = System.IO.Path.Combine(Application.streamingAssetsPath, "Data");
                if (JsonDataLoader.TryLoadAll(jsonDirectory, out var fromJson))
                {
                    source = fromJson;
                    sourceName = "JSON(StreamingAssets/Data)";
                }
                else
                {
                    source = Resources.LoadAll<GameData>("Data");
                    sourceName = "SO(Resources/Data)";
                }
            }
            foreach (var data in source)
            {
                if (data == null)
                {
                    continue;
                }

                var type = data.GetType();
                if (!m_store.TryGetValue(type, out var table))
                {
                    table = new Dictionary<int, GameData>();
                    m_store.Add(type, table);
                }

                if (table.ContainsKey(data.ID))
                {
                    Debug.LogError($"[DataManager] 중복 {type.Name} ID={data.ID} ({data.name})");
                    continue;
                }
                table.Add(data.ID, data);
            }

            // 소스 무관 공통 로드 확인 로그 (테스트 SO 주입·SO 폴백·JSON 모두)
            int total = 0;
            var summary = new System.Text.StringBuilder();
            foreach (var pair in m_store)
            {
                total += pair.Value.Count;
                summary.Append($"  {pair.Key.Name}: {pair.Value.Count}건\n");
            }
            if (total == 0)
            {
                Debug.LogWarning($"[DataManager] 콘텐츠 로드({sourceName}): 0건 — 데이터 소스 확인 필요");
            }
            else
            {
                Debug.Log($"[DataManager] 콘텐츠 로드({sourceName}): 총 {total}건, {m_store.Count}개 타입\n{summary}");
            }
        }

        /// <summary>
        /// JSON/SO 로드 완료 후, 테스트 SO를 적용한다(수동 Play 검증, D9).
        /// 같은 (타입·ID)가 있으면 교체하고, 없으면 신규로 추가한다.
        /// </summary>
        public void OverrideWithTestData(IEnumerable<GameData> testData)
        {
            if (testData == null)
            {
                return;
            }

            int overridden = 0;
            int added = 0;
            foreach (var data in testData)
            {
                if (data == null)
                {
                    continue;
                }

                var type = data.GetType();
                if (!m_store.TryGetValue(type, out var table))
                {
                    table = new Dictionary<int, GameData>();
                    m_store.Add(type, table);
                }

                if (table.ContainsKey(data.ID))
                {
                    table[data.ID] = data;   // 기존 항목을 테스트 SO로 교체
                    overridden++;
                }
                else
                {
                    table.Add(data.ID, data);   // JSON에 없던 ID는 신규 추가
                    added++;
                }
            }

            Debug.Log($"[DataManager] 테스트 적용: 교체 {overridden}건, 추가 {added}건");
        }

        /// <summary>구체 타입 T의 단건을 ID로 조회한다(예: GetData&lt;ItemData_Fish&gt;(1)).</summary>
        public T GetData<T>(int id) where T : GameData
            => m_store.TryGetValue(typeof(T), out var table) && table.TryGetValue(id, out var data)
                ? (T)data
                : null;

        /// <summary>로드된 모든 GameData 열거(타입 무관). 에디터 검증 툴 등 전수 순회용.</summary>
        public IEnumerable<GameData> GetAllLoaded()
        {
            foreach (var table in m_store.Values)
            {
                foreach (var data in table.Values)
                {
                    yield return data;
                }
            }
        }

        /// <summary>구체 타입 T의 원본 Data 전체를 반환한다(예: 모든 StageData). 없으면 빈 리스트.</summary>
        public IReadOnlyList<T> GetAll<T>() where T : GameData
        {
            var result = new List<T>();
            if (m_store.TryGetValue(typeof(T), out var table))
            {
                foreach (var data in table.Values)
                {
                    result.Add((T)data);
                }
            }
            return result;
        }
    }
}
