using System;
using System.Collections.Generic;
using DesktopCompanion.Save;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 게임 사건의 누적 집계를 보유하는 단일 진입점.
    /// 출처 시스템이 RecordXxx를 호출하면 집계를 갱신하고, 기록 변경 이벤트와 사건 이벤트를 차례로 발행한다.
    /// 기록을 어떻게 해석할지는 소비자의 책임이다 — 이 클래스는 판단하지 않는다.
    /// 다른 System을 참조하지 않는 기반 시스템이다.
    ///
    /// 집계 규칙: 사건 하나는 출처 전체 키와 대상 ID 키(대상이 있을 때)를 함께 올린다. 기록은 증가만 한다.
    /// 발행 순서: 집계 갱신 → OnRecordsChanged → 출처별 사건 이벤트. 어느 구독자든 조회하면 갱신된 값을 얻는다.
    /// 구독자 규약: 이벤트는 출처 시스템의 호출 스택 안에서 발생하므로 핸들러에서 출처 시스템을 다시 호출하지 않는다.
    /// </summary>
    public class RecordSystem : SystemBase, ISaveable
    {
        private readonly Dictionary<RecordKey, long> m_values = new();

        /// <summary>
        /// 이번 사건으로 바뀐 키 전부와 각각의 이전 값·새 값. 집계 갱신 직후, 사건 이벤트보다 먼저 발행한다.
        /// 한 사건의 변경을 목록 하나로 보내야 두 키를 함께 쓰는 조건이 한 사건으로 두 번 달성 판정되지 않는다.
        /// </summary>
        public event Action<IReadOnlyList<RecordChange>> OnRecordsChanged;

        public event Action<FishCaughtInfo> OnFishCaught;
        public event Action<int> OnFishingFailed;
        public event Action<ItemRecordInfo> OnItemDropped;
        public event Action<ItemCraftInfo> OnItemCrafted;
        public event Action<ItemTradeInfo> OnItemBought;
        public event Action<ItemTradeInfo> OnItemSold;
        public event Action<int> OnGoldEarned;
        public event Action<int> OnGoldSpent;
        public event Action<int> OnStageArrived;

        /// <summary>현재 보유한 전체 집계. 디버그·표시용 열람 전용이다.</summary>
        public IReadOnlyDictionary<RecordKey, long> Values => m_values;

        // ── 생명주기 ──

        public override void Initialize() => m_values.Clear();   // 원시 기본값. 저장이 있으면 RestoreState가 덮어쓴다.

        // ── 조회 ──

        /// <summary>키의 누적값. 기록된 적 없으면 0이다.</summary>
        public long GetValue(RecordKey key) => m_values.TryGetValue(key, out long value) ? value : 0;

        /// <summary>targetId가 0이면 출처 전체 키의 누적값이다.</summary>
        public long GetValue(RecordSource source, int targetId = 0) => GetValue(new RecordKey(source, targetId));

        // ── 기록(출처별) ──
        // 실제 게임 플레이 사건에서만 호출한다. 세이브 복원·초기값 설정·에디터 치트 경로에서는 호출하지 않는다.

        public void RecordFishCaught(FishCaughtInfo info)
        {
            if (Accumulate(RecordSource.FishCaught, info.FishId, 1))
            {
                OnFishCaught?.Invoke(info);
            }
        }

        public void RecordFishingFailed(int fishId)
        {
            if (Accumulate(RecordSource.FishingFailed, fishId, 1))
            {
                OnFishingFailed?.Invoke(fishId);
            }
        }

        public void RecordItemDropped(ItemRecordInfo info)
        {
            if (Accumulate(RecordSource.ItemDropped, info.ItemId, info.Count))
            {
                OnItemDropped?.Invoke(info);
            }
        }

        public void RecordItemCrafted(ItemCraftInfo info)
        {
            if (Accumulate(RecordSource.ItemCrafted, info.ItemId, info.Count))
            {
                OnItemCrafted?.Invoke(info);
            }
        }

        public void RecordItemBought(ItemTradeInfo info)
        {
            if (Accumulate(RecordSource.ItemBought, info.ItemId, info.Count))
            {
                OnItemBought?.Invoke(info);
            }
        }

        public void RecordItemSold(ItemTradeInfo info)
        {
            if (Accumulate(RecordSource.ItemSold, info.ItemId, info.Count))
            {
                OnItemSold?.Invoke(info);
            }
        }

        public void RecordGoldEarned(int amount)
        {
            if (Accumulate(RecordSource.GoldEarned, 0, amount))
            {
                OnGoldEarned?.Invoke(amount);
            }
        }

        public void RecordGoldSpent(int amount)
        {
            if (Accumulate(RecordSource.GoldSpent, 0, amount))
            {
                OnGoldSpent?.Invoke(amount);
            }
        }

        public void RecordStageArrived(int stageId)
        {
            if (Accumulate(RecordSource.StageArrived, stageId, 1))
            {
                OnStageArrived?.Invoke(stageId);
            }
        }

        // ── 집계 ──

        /// <summary>
        /// 출처 전체 키와 대상 ID 키를 올리고 바뀐 키를 목록 하나로 발행한다.
        /// 증가량이 0 이하면 기록하지 않는다 — 기록은 증가만 한다.
        /// </summary>
        private bool Accumulate(RecordSource source, int targetId, long amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[RecordSystem] {source}: 증가량이 0 이하라 기록하지 않는다 ({amount})");
                return false;
            }

            // 호출마다 새로 만든다. 핸들러에서 다른 기록 함수를 호출해도 진행 중인 목록이 바뀌지 않는다.
            var changes = new List<RecordChange>(2);
            Add(new RecordKey(source), amount, changes);
            if (targetId > 0)
            {
                Add(new RecordKey(source, targetId), amount, changes);
            }

            OnRecordsChanged?.Invoke(changes);
            return true;
        }

        private void Add(RecordKey key, long amount, List<RecordChange> changes)
        {
            long oldValue = GetValue(key);
            long newValue = oldValue + amount;
            m_values[key] = newValue;
            changes.Add(new RecordChange(key, oldValue, newValue));
        }

        // ── 저장 ──

        public string SaveId => "record";
        public Type StateType => typeof(RecordSave);

        public object CaptureState()
        {
            var save = new RecordSave();
            foreach (KeyValuePair<RecordKey, long> pair in m_values)
            {
                save.entries.Add(new RecordSave.Entry
                {
                    source = pair.Key.Source.ToString(),
                    targetId = pair.Key.TargetId,
                    value = pair.Value,
                });
            }

            return save;
        }

        public void RestoreState(object state)
        {
            if (state is not RecordSave save || save.entries == null)
            {
                return;
            }

            m_values.Clear();
            foreach (RecordSave.Entry entry in save.entries)
            {
                // 이름이 바뀌거나 삭제된 출처는 건너뛴다. 항목 하나 때문에 기록 전체 복원이 실패하지 않게 한다.
                if (Enum.TryParse(entry.source, out RecordSource source) == false || source == RecordSource.None)
                {
                    Debug.LogWarning($"[RecordSystem] 알 수 없는 출처의 저장값을 건너뛴다: {entry.source}");
                    continue;
                }

                m_values[new RecordKey(source, entry.targetId)] = entry.value;
            }
        }
    }
}
