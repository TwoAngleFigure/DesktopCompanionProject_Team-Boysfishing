using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 무작위 보상 테이블을 굴린다. 조합·낚시 드랍·보상 등 굴릴 곳이 어디든 테이블 ID로만 참조한다.
    ///
    /// 후보 문자열 파싱은 로드 시 한 번만 한다 — 드랍처럼 잦은 호출에서 매번 파싱하지 않기 위함이다.
    /// 가중치 합도 함께 캐시해 굴릴 때는 난수 하나와 순회만 남긴다.
    /// </summary>
    public class RandomTableSystem : SystemBase
    {
        /// <summary>테이블 후보 하나. 가중치는 1 이상만 담긴다.</summary>
        public readonly struct Entry
        {
            public readonly ItemType ItemType;
            public readonly int DataId;
            public readonly int Weight;

            public Entry(ItemType itemType, int dataId, int weight)
            {
                ItemType = itemType;
                DataId = dataId;
                Weight = weight;
            }
        }

        private readonly Dictionary<int, List<Entry>> m_tables = new();
        private readonly Dictionary<int, int> m_weightSums = new();

        public override void PostInitialize()
        {
            m_tables.Clear();
            m_weightSums.Clear();

            if (this.DataManager == null)
            {
                Debug.LogError("[RandomTableSystem] DataManager 참조를 찾을 수 없습니다!");
                return;
            }

            foreach (RandomTableData table in this.DataManager.GetAll<RandomTableData>())
            {
                if (table == null)
                {
                    continue;
                }

                List<Entry> entries = ParseEntries(table);
                if (entries.Count == 0)
                {
                    Debug.LogWarning($"[RandomTableSystem] 후보가 비어 있는 테이블: {table.ID}");
                    continue;
                }

                int sum = 0;
                foreach (Entry entry in entries)
                {
                    sum += entry.Weight;
                }

                m_tables[table.ID] = entries;
                m_weightSums[table.ID] = sum;
            }

            Debug.Log($"[RandomTableSystem] {m_tables.Count}개의 랜덤 테이블을 로드했습니다.");
        }

        /// <summary>
        /// 테이블을 굴려 결과 하나를 정한다. 테이블이 없거나 후보가 비면 false다.
        /// 호출자는 실패를 먼저 처리한 뒤 비용을 소모해야 한다.
        /// </summary>
        public bool TryRoll(int tableId, out ItemType itemType, out int dataId)
        {
            itemType = default;
            dataId = 0;

            if (!m_tables.TryGetValue(tableId, out List<Entry> entries))
            {
                Debug.LogError($"[RandomTableSystem] 랜덤 테이블을 찾을 수 없습니다: {tableId}");
                return false;
            }

            // 누적 가중치: [0, 총합)에서 뽑아 구간이 걸리는 첫 후보를 고른다.
            int roll = UnityEngine.Random.Range(0, m_weightSums[tableId]);
            foreach (Entry entry in entries)
            {
                roll -= entry.Weight;
                if (roll < 0)
                {
                    itemType = entry.ItemType;
                    dataId = entry.DataId;
                    return true;
                }
            }

            // 가중치 합과 순회가 어긋날 일은 없지만, 빈손으로 돌아가지 않도록 마지막 후보로 닫는다.
            Entry last = entries[entries.Count - 1];
            itemType = last.ItemType;
            dataId = last.DataId;
            return true;
        }

        /// <summary>테이블 정의를 돌려준다. 표시 계층이 이름·아이콘·설명을 만들 때 쓴다.</summary>
        public RandomTableData GetTable(int tableId)
            => this.DataManager != null ? this.DataManager.GetData<RandomTableData>(tableId) : null;

        /// <summary>해당 ID의 테이블이 있는지. 표시 계층이 확정 지급과 구분할 때 쓴다.</summary>
        public bool HasTable(int tableId) => m_tables.ContainsKey(tableId);

        // "타입:ID:가중치;..." 파싱. 잘못된 토큰은 건너뛰되 무엇이 왜 빠졌는지 남긴다.
        private static List<Entry> ParseEntries(RandomTableData table)
        {
            var entries = new List<Entry>();
            if (string.IsNullOrEmpty(table.Entries))
            {
                return entries;
            }

            foreach (string token in table.Entries.Split(';'))
            {
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                string[] parts = token.Split(':');
                if (parts.Length < 3)
                {
                    Debug.LogError($"[RandomTableSystem] 후보 표기가 잘못됐습니다: '{token}' (테이블 {table.ID})");
                    continue;
                }

                // 재료 표기(ItemData_Materials:...)와 섞여 들어와도 받아들인다.
                string typeName = parts[0].Replace("ItemData_", "");
                if (!Enum.TryParse(typeName, out ItemType itemType))
                {
                    Debug.LogError($"[RandomTableSystem] 알 수 없는 아이템 타입: '{parts[0]}' (테이블 {table.ID})");
                    continue;
                }

                if (!int.TryParse(parts[1], out int dataId) || !int.TryParse(parts[2], out int weight))
                {
                    Debug.LogError($"[RandomTableSystem] 숫자 파싱 실패: '{token}' (테이블 {table.ID})");
                    continue;
                }

                if (weight <= 0)
                {
                    Debug.LogWarning($"[RandomTableSystem] 가중치가 0 이하라 제외합니다: '{token}' (테이블 {table.ID})");
                    continue;
                }

                entries.Add(new Entry(itemType, dataId, weight));
            }
            return entries;
        }
    }
}
