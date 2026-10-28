namespace DesktopCompanion.Systems
{
    public readonly struct FishCollectionStageInfo
    {
        public int StageDataId { get; }
        public string StageName { get; }

        public FishCollectionStageInfo(
            int stageDataId,
            string stageName)
        {
            StageDataId = stageDataId;
            StageName = stageName;
        }
    }
}
