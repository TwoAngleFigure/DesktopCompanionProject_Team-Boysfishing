namespace DesktopCompanion.Systems
{
    public enum InventoryFullPolicy
    {
        StopAndAsk = 0,
        AlwaysSell = 1,
        StopOnRecordUpdate = 2,
    }

    public enum RecordStopCriterion
    {
        Quality = 0,
        Size = 1,
    }

    public static class FishingInventoryFullPolicyEvaluator
    {
        public static bool ShouldCreatePendingCatch(
            InventoryFullPolicy policy,
            RecordStopCriterion recordStopCriterion,
            bool isCollectionUpdated,
            FishCollectionUpdateResult collectionResult)
        {
            switch (policy)
            {
                case InventoryFullPolicy.StopAndAsk:
                    return true;

                case InventoryFullPolicy.AlwaysSell:
                    return false;

                case InventoryFullPolicy.StopOnRecordUpdate:
                    if (!isCollectionUpdated)
                    {
                        return true;
                    }

                    if (collectionResult.IsRegistered)
                    {
                        return true;
                    }

                    return recordStopCriterion == RecordStopCriterion.Quality
                        ? collectionResult.IsBestQualityImproved
                        : collectionResult.IsBestSizeImproved;

                default:
                    return true;
            }
        }
    }
}
