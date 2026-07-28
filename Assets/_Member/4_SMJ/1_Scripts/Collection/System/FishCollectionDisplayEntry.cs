using DesktopCompanion.Data;

namespace DesktopCompanion.Systems
{
    public readonly struct FishCollectionDisplayEntry
    {
        public int FishDataId { get; }
        public string FishName { get; }

        public bool IsRegistered { get; }
        public ItemQuality BestQuality { get; }
        public float BestSize { get; }

        public FishCollectionDisplayEntry(
            int fishDataId,
            string fishName,
            bool isRegistered,
            ItemQuality bestQuality,
            float bestSize)
        {
            FishDataId = fishDataId;
            FishName = fishName;
            IsRegistered = isRegistered;
            BestQuality = bestQuality;
            BestSize = bestSize;
        }
    }
}