using DesktopCompanion.Data;

namespace DesktopCompanion.Systems
{
    public enum FishCollectionUpdateType
    {
        None,

        // 처음 잡은 어종
        Registered,

        // Size만 갱신
        BestSizeImproved,

        // Quality + Size 갱신
        BestQualityAndSizeImproved
    }

    public readonly struct FishCollectionUpdateResult
    {
        public int FishDataId { get; }
        public FishCollectionUpdateType UpdateType { get; }

        public ItemQuality PreviousBestQuality { get; }
        public ItemQuality CurrentBestQuality { get; }
        public float PreviousBestSize { get; }
        public float CurrentBestSize { get; }

        public bool IsRegistered => UpdateType == FishCollectionUpdateType.Registered;

        public bool IsBestQualityImproved => UpdateType == FishCollectionUpdateType.BestQualityAndSizeImproved;

        public bool IsBestSizeImproved => UpdateType == FishCollectionUpdateType.BestSizeImproved ||
            UpdateType == FishCollectionUpdateType.BestQualityAndSizeImproved;

        public FishCollectionUpdateResult(int fishDataId,
            FishCollectionUpdateType updateType,
            ItemQuality previousBestQuality,
            ItemQuality currentBestQuality,
            float previousBestSize,
            float currentBestSize)
        {
            FishDataId = fishDataId;
            UpdateType = updateType;
            PreviousBestQuality = previousBestQuality;
            CurrentBestQuality = currentBestQuality;
            PreviousBestSize = previousBestSize;
            CurrentBestSize = currentBestSize;
        }
    }

    public readonly struct FishCollectionRecord
    {
        public int FishDataId { get; }
        public ItemQuality BestQuality { get; }
        public float BestSize { get; }

        public FishCollectionRecord(int fishDataId, ItemQuality bestQuality, float bestSize)
        {
            FishDataId = fishDataId;
            BestQuality = bestQuality;
            BestSize = bestSize;
        }
    }
}