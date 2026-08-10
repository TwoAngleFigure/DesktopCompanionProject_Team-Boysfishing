using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Systems
{
    public enum FishingStateSignal
    {
        None,
        BattleReady,
        BattleSucceeded,
        BattleTimedOut,
        BattleTargetInvalid
    }

    public enum FishingBattleOutcome
    {
        InProgress,
        Succeeded,
        TimedOut,
        InvalidTarget
    }

    public enum FishingCatchDisposition
    {
        Stored,
        Pending,
        Sold,
        Failed
    }

    public sealed class FishingAttempt
    {
        public BattleFishData BattleFishData { get; }
        public float Size { get; }
        public ItemQuality Quality { get; }
        public float BattleDuration { get; }
        public float AppliedBaitStat { get; }
        public float AppliedGroundbaitStat { get; }

        public FishingAttempt(
            BattleFishData battleFishData,
            float size,
            ItemQuality quality,
            float battleDuration,
            float appliedBaitStat,
            float appliedGroundbaitStat)
        {
            BattleFishData = battleFishData;
            Size = size;
            Quality = quality;
            BattleDuration = battleDuration;
            AppliedBaitStat = appliedBaitStat;
            AppliedGroundbaitStat = appliedGroundbaitStat;
        }
    }

    public sealed class FishingAttemptStartResult
    {
        public bool IsSuccess { get; }
        public FishingAttempt Attempt { get; }
        public EntityHandle BattleFishHandle { get; }

        private FishingAttemptStartResult(
            bool isSuccess,
            FishingAttempt attempt,
            EntityHandle battleFishHandle)
        {
            IsSuccess = isSuccess;
            Attempt = attempt;
            BattleFishHandle = battleFishHandle;
        }

        public static FishingAttemptStartResult Success(
            FishingAttempt attempt,
            EntityHandle battleFishHandle)
        {
            return new FishingAttemptStartResult(true, attempt, battleFishHandle);
        }

        public static FishingAttemptStartResult Failed()
        {
            return new FishingAttemptStartResult(false, null, default);
        }
    }

    public sealed class FishingCatchPreparation
    {
        public bool IsSuccess { get; }
        public FishingResultType FailureResultType { get; }
        public EntityHandle CaughtHandle { get; }
        public bool IsCollectionUpdated { get; }
        public FishCollectionUpdateResult CollectionResult { get; }
        public ItemDrop[] Drops { get; }
        public ItemQuality Quality { get; }
        public bool IsBoss { get; }

        private FishingCatchPreparation(
            bool isSuccess,
            FishingResultType failureResultType,
            EntityHandle caughtHandle,
            bool isCollectionUpdated,
            FishCollectionUpdateResult collectionResult,
            ItemDrop[] drops,
            ItemQuality quality,
            bool isBoss)
        {
            IsSuccess = isSuccess;
            FailureResultType = failureResultType;
            CaughtHandle = caughtHandle;
            IsCollectionUpdated = isCollectionUpdated;
            CollectionResult = collectionResult;
            Drops = drops;
            Quality = quality;
            IsBoss = isBoss;
        }

        public static FishingCatchPreparation Success(
            EntityHandle caughtHandle,
            bool isCollectionUpdated,
            FishCollectionUpdateResult collectionResult,
            ItemDrop[] drops,
            ItemQuality quality,
            bool isBoss)
        {
            return new FishingCatchPreparation(
                true,
                FishingResultType.Success,
                caughtHandle,
                isCollectionUpdated,
                collectionResult,
                drops,
                quality,
                isBoss);
        }

        public static FishingCatchPreparation Failed(FishingResultType resultType)
        {
            return new FishingCatchPreparation(
                false,
                resultType,
                default,
                false,
                default,
                null,
                default,
                false);
        }
    }

    public sealed class FishingCatchResolution
    {
        public FishingCatchDisposition Disposition { get; }
        public FishingResultType ResultType { get; }
        public EntityHandle CaughtHandle { get; }
        public int EarnedGold { get; }

        public FishingCatchResolution(
            FishingCatchDisposition disposition,
            FishingResultType resultType,
            EntityHandle caughtHandle,
            int earnedGold)
        {
            Disposition = disposition;
            ResultType = resultType;
            CaughtHandle = caughtHandle;
            EarnedGold = earnedGold;
        }
    }
}
