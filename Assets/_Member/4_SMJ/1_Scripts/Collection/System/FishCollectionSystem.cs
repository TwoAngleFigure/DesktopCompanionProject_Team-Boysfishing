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
            public int LastUpdatedOrder;
        }

        private readonly Dictionary<int, Entry> m_entries = new();
        private int m_updateSequence;

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
            m_updateSequence = 0;
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

            Dictionary<int, List<int>> stageDataIdsByFishDataId = BuildStageDataIdsByFishDataId();
            Dictionary<int, string> stageNamesByStageDataId = BuildStageNamesByStageDataId();

            foreach (ItemData_Fish fishData in fishDatas)
            {
                bool isRegistered = m_entries.TryGetValue(fishData.ID, out Entry record);

                IReadOnlyList<int> stageDataIds = stageDataIdsByFishDataId.TryGetValue(
                    fishData.ID,
                    out List<int> mappedStageDataIds) 
                    ? mappedStageDataIds 
                    : Array.Empty<int>();

                List<string> stageNames = new(stageDataIds.Count);

                foreach (int stageDataId in stageDataIds)
                {
                    if (stageNamesByStageDataId.TryGetValue(
                            stageDataId,
                            out string stageName))
                    {
                        stageNames.Add(stageName);
                    }
                }

                string productionMaterialName = fishData.AquariumMaterial != null
                    ? fishData.AquariumMaterial.Name
                    : "-";

                entries.Add(new FishCollectionDisplayEntry(
                    fishData.ID,
                    fishData.Name,
                    fishData.Rarity,
                    fishData.Tier,
                    stageDataIds,
                    stageNames,
                    productionMaterialName,
                    isRegistered,
                    isRegistered ? record.BestQuality : default,
                    isRegistered ? record.BestSize : 0f,
                    isRegistered ? record.LastUpdatedOrder : 0));
            }

            return entries;
        }

        public IReadOnlyList<FishCollectionStageInfo> GetAllStageInfos()
        {
            List<FishCollectionStageInfo> result = new();

            foreach (StageData stageData in DataManager.GetAll<StageData>())
            {
                if (stageData == null)
                {
                    continue;
                }

                result.Add(new FishCollectionStageInfo(
                    stageData.ID,
                    stageData.Name));
            }

            result.Sort(
                (left, right) =>
                    left.StageDataId.CompareTo(right.StageDataId));

            return result;
        }

        private Dictionary<int, string> BuildStageNamesByStageDataId()
        {
            Dictionary<int, string> result = new();

            foreach (StageData stageData in DataManager.GetAll<StageData>())
            {
                if (stageData != null)
                {
                    result.Add(stageData.ID, stageData.Name);
                }
            }

            return result;
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
                    BestSize = size,
                    LastUpdatedOrder = IssueUpdateOrder()
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

                    return false;
                }

                entry.BestSize = size;
                updateType = FishCollectionUpdateType.BestSizeImproved;
            }

            if (updateType != FishCollectionUpdateType.None)
            {
                entry.LastUpdatedOrder = IssueUpdateOrder();
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
                    bestSize = entry.BestSize,
                    lastUpdatedOrder = entry.LastUpdatedOrder
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
            m_updateSequence = 0;

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
                        BestSize = savedEntry.bestSize,
                        LastUpdatedOrder = savedEntry.lastUpdatedOrder
                    });

                if (savedEntry.lastUpdatedOrder > m_updateSequence)
                {
                    m_updateSequence = savedEntry.lastUpdatedOrder;
                }
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

        private int IssueUpdateOrder()
        {
            return ++m_updateSequence;
        }

        private Dictionary<int, List<int>> BuildStageDataIdsByFishDataId()
        {
            Dictionary<int, List<int>> result = new();

            foreach (StageData stageData in DataManager.GetAll<StageData>())
            {
                if (stageData == null)
                {
                    continue;
                }

                if (stageData.TierPools != null)
                {
                    foreach (TierPool tierPool in stageData.TierPools)
                    {
                        if (tierPool?.Entries == null)
                        {
                            continue;
                        }

                        foreach (FishPoolEntry poolEntry in tierPool.Entries)
                        {
                            AddStageDataId(
                                result,
                                poolEntry?.Fish,
                                stageData.ID);
                        }
                    }
                }

                AddStageDataId(result, stageData.Boss, stageData.ID);
            }

            foreach (List<int> stageDataIds in result.Values)
            {
                stageDataIds.Sort();
            }

            return result;
        }

        private static void AddStageDataId(
            Dictionary<int, List<int>> result,
            BattleFishData battleFishData,
            int stageDataId)
        {
            if (battleFishData?.ItemFish == null)
            {
                return;
            }

            int fishDataId = battleFishData.ItemFish.ID;

            if (!result.TryGetValue(fishDataId, out List<int> stageDataIds))
            {
                stageDataIds = new List<int>();
                result.Add(fishDataId, stageDataIds);
            }

            if (!stageDataIds.Contains(stageDataId))
            {
                stageDataIds.Add(stageDataId);
            }
        }
    }
}
