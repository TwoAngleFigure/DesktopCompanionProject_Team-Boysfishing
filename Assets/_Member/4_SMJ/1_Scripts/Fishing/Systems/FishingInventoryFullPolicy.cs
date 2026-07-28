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
}
