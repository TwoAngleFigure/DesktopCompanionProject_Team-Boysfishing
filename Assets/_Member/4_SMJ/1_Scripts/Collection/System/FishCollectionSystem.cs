using DesktopCompanion.Data;
using DesktopCompanion.Save;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public class FishCollectionSystem : SystemBase, ISaveable
    {
        private class Entry
        {
            public ItemQuality BestQuality;
            public float BestSize;
        }

        private readonly Dictionary<int, Entry> m_entries = new();

        /// <summary>
        /// 신규 어종 등록 또는 기존 기록 갱신 시 발생
        /// </summary>
        public event Action<FishCollectionUpdateResult> OnCollectionUpdated;

        public string SaveId => "fishCollection";
        public Type StateType => typeof(FishCollectionSave);

        public int RegisteredCount => m_entries.Count;

        public override void Initialize()
        {
            m_entries.Clear();
        }

        /// <summary>
        /// 해당 물고기 종이 도감에 등록되어 있는지 확인
        /// </summary>
        public bool IsRegistered(int fishDataId)
        {
            return m_entries.ContainsKey(fishDataId);
        }

        public bool TryGetRecord(int fishDataId, out FishCollectionRecord record)
        {
            record = default;

            if (!m_entries.TryGetValue(fishDataId, out Entry entry))
            {
                return false;
            }

            record = new FishCollectionRecord(fishDataId, entry.BestQuality, entry.BestSize);

            return true;
        }

        /// <summary>
        /// 등록된 모든 도감 기록을 물고기 ID 순서로 반환
        /// </summary>
        public IReadOnlyList<FishCollectionRecord> GetAllRecords()
        {
            List<int> fishDataIds = new(m_entries.Keys);
            fishDataIds.Sort();

            List<FishCollectionRecord> records = new(fishDataIds.Count);

            foreach (int fishDataId in fishDataIds)
            {
                Entry entry = m_entries[fishDataId];

                records.Add(new FishCollectionRecord(fishDataId, entry.BestQuality, entry.BestSize));
            }

            return records;
        }

        /// <summary>
        /// 모든 물고기 종의 도감 표시 정보를 ID 순서로 반환.
        /// 아직 잡지 않은 종도 IsRegistered = false로 포함.
        /// </summary>
        public IReadOnlyList<FishCollectionDisplayEntry> GetAllDisplayEntries()
        {
            List<ItemData_Fish> fishDatas = new(DataManager.GetAll<ItemData_Fish>());

            fishDatas.Sort((left, right) => left.ID.CompareTo(right.ID));

            List<FishCollectionDisplayEntry> entries = new(fishDatas.Count);

            foreach (ItemData_Fish fishData in fishDatas)
            {
                bool isRegistered = m_entries.TryGetValue(fishData.ID, out Entry record);

                entries.Add(new FishCollectionDisplayEntry(
                    fishData.ID,
                    fishData.Name,
                    isRegistered,
                    isRegistered ? record.BestQuality : default,
                    isRegistered ? record.BestSize : 0f));
            }

            return entries;
        }

        /// <summary>
        /// 포획한 물고기를 도감에 반영한다.
        ///
        /// 반환값:
        /// - true: 입력이 유효하며 정상적으로 판정.
        ///         기록 변경이 없을 수도 있음(UpdateType.None).
        /// - false: 유효하지 않은 ID, 품질, 사이즈거나
        ///          사이즈-품질 불변식 위반.
        /// </summary>
        public bool TryRegisterCatch(int fishDataId,
            ItemQuality quality,
            float size,
            out FishCollectionUpdateResult result)
        {
            result = default;

            if (!ValidateRecord(fishDataId, quality, size, "RegisterCatch"))
            {
                return false;
            }

            // 최초 포획: 최고 품질과 최대 크기를 초기 기록으로 설정.
            // 등록 알림만 표시하기 위해 UpdateType은 Registered만 반환.
            if (!m_entries.TryGetValue(fishDataId, out Entry entry))
            {
                entry = new Entry
                {
                    BestQuality = quality,
                    BestSize = size
                };

                m_entries.Add(fishDataId, entry);

                result = new FishCollectionUpdateResult(
                    fishDataId,
                    FishCollectionUpdateType.Registered,
                    quality,
                    quality,
                    size,
                    size);

                OnCollectionUpdated?.Invoke(result);
                return true;
            }

            ItemQuality previousBestQuality = entry.BestQuality;
            float previousBestSize = entry.BestSize;

            FishCollectionUpdateType updateType = FishCollectionUpdateType.None;

            // 최고 품질 갱신
            if (quality > previousBestQuality)
            {
                // 최고 품질이 상승했다면 최대 크기도 반드시 상승해야 한다.
                if (size <= previousBestSize)
                {
                    Debug.LogError(
                        "[FishCollectionSystem] 도감 갱신 실패. " +
                        "품질은 상승했지만 크기가 증가하지 않았습니다. " +
                        $"fishDataId={fishDataId}, " +
                        $"previousQuality={previousBestQuality}, " +
                        $"caughtQuality={quality}, " +
                        $"previousSize={previousBestSize:0.###}, " +
                        $"caughtSize={size:0.###}");

                    return false;
                }

                entry.BestQuality = quality;
                entry.BestSize = size;

                updateType = FishCollectionUpdateType.BestQualityAndSizeImproved;
            }
            else if (size > previousBestSize)
            {
                // 더 큰 개체가 더 낮은 품질을 갖는 경우는 존재하면 안된다.
                if (quality < previousBestQuality)
                {
                    Debug.LogError(
                        "[FishCollectionSystem] 도감 갱신 실패. " +
                        "크기는 증가했지만 품질이 낮아졌습니다. " +
                        $"fishDataId={fishDataId}, " +
                        $"previousQuality={previousBestQuality}, " +
                        $"caughtQuality={quality}, " +
                        $"previousSize={previousBestSize:0.###}, " +
                        $"caughtSize={size:0.###}");

                    return false;
                }

                entry.BestSize = size;
                updateType = FishCollectionUpdateType.BestSizeImproved;
            }

            result = new FishCollectionUpdateResult(
              fishDataId,
              updateType,
              previousBestQuality,
              entry.BestQuality,
              previousBestSize,
              entry.BestSize);

            if (updateType != FishCollectionUpdateType.None)
            {
                OnCollectionUpdated?.Invoke(result);
            }

            return true;
        }


        public object CaptureState()
        {
            FishCollectionSave save = new();

            List<int> fishDataIds = new(m_entries.Keys);
            fishDataIds.Sort();

            foreach (int fishDataId in fishDataIds)
            {
                Entry entry = m_entries[fishDataId];

                save.entries.Add(new FishCollectionSave.Entry
                {
                    fishDataId = fishDataId,
                    bestQuality = entry.BestQuality,
                    bestSize = entry.BestSize
                });
            }

            return save;
        }

        public void RestoreState(object state)
        {
            if (state is not FishCollectionSave save)
            {
                Debug.LogWarning(
                    "[FishCollectionSystem] 도감 복원 실패. " +
                    $"잘못된 상태 타입: {state?.GetType().Name ?? "null"}");

                return;
            }

            m_entries.Clear();

            if (save.entries == null)
            {
                return;
            }

            foreach (FishCollectionSave.Entry savedEntry in save.entries)
            {
                if (savedEntry == null)
                {
                    Debug.LogWarning(
                        "[FishCollectionSystem] null 도감 저장 항목을 건너뜁니다.");

                    continue;
                }

                if (!ValidateRecord(
                        savedEntry.fishDataId,
                        savedEntry.bestQuality,
                        savedEntry.bestSize,
                        "RestoreState"))
                {
                    continue;
                }

                if (m_entries.ContainsKey(savedEntry.fishDataId))
                {
                    Debug.LogWarning(
                        "[FishCollectionSystem] 중복된 도감 저장 항목을 건너뜁니다. " +
                        $"fishDataId={savedEntry.fishDataId}");

                    continue;
                }

                m_entries.Add(
                    savedEntry.fishDataId,
                    new Entry
                    {
                        BestQuality = savedEntry.bestQuality,
                        BestSize = savedEntry.bestSize
                    });
            }

            Debug.Log(
                $"[FishCollectionSystem] 도감 복원 완료. " +
                $"registeredCount={m_entries.Count}");
        }

        private bool ValidateRecord(int fishDataId,
            ItemQuality quality,
            float size,
            string context)
        {
            if (fishDataId <= 0)
            {
                Debug.LogWarning($"[FishCollectionSystem] {context} 실패. 유효하지 않은 fishDataId={fishDataId}");

                return false;
            }

            ItemData_Fish fishData = DataManager.GetData<ItemData_Fish>(fishDataId);

            if (fishData == null)
            {
                Debug.LogWarning(
                    $"[FishCollectionSystem] {context} 실패. " +
                    $"ItemData_Fish를 찾을 수 없습니다. " +
                    $"fishDataId={fishDataId}");

                return false;
            }

            if (!Enum.IsDefined(typeof(ItemQuality), quality))
            {
                Debug.LogWarning(
                    $"[FishCollectionSystem] {context} 실패. " +
                    $"유효하지 않은 품질입니다. " +
                    $"fishDataId={fishDataId}, quality={quality}");

                return false;
            }

            if (size <= 0f ||
               float.IsNaN(size) ||
               float.IsInfinity(size))
            {
                Debug.LogWarning(
                    $"[FishCollectionSystem] {context} 실패. " +
                    $"유효하지 않은 크기입니다. " +
                    $"fishDataId={fishDataId}, size={size}");

                return false;
            }

            return true;
        }
    }
}