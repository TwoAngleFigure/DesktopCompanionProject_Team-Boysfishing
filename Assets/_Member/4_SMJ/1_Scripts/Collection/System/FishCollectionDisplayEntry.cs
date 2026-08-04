using DesktopCompanion.Data;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    public readonly struct FishCollectionDisplayEntry
    {
        public int FishDataId { get; }
        public string FishName { get; }

        public ItemRarity Rarity { get; }
        public int Tier { get; }
        public IReadOnlyList<int> StageDataIds { get; }

        public bool IsRegistered { get; }
        public ItemQuality BestQuality { get; }
        public float BestSize { get; }
        public int LastUpdatedOrder { get; }

        public FishCollectionDisplayEntry(
            int fishDataId,
            string fishName,
            ItemRarity rarity,
            int tier,
            IReadOnlyList<int> stageDataIds,
            bool isRegistered,
            ItemQuality bestQuality,
            float bestSize,
            int lastUpdatedOrder)
        {
            FishDataId = fishDataId;
            FishName = fishName;
            Rarity = rarity;
            Tier = tier;
            IsRegistered = isRegistered;
            StageDataIds = stageDataIds;
            BestQuality = bestQuality;
            BestSize = bestSize;
            LastUpdatedOrder = lastUpdatedOrder;
        }
    }
}