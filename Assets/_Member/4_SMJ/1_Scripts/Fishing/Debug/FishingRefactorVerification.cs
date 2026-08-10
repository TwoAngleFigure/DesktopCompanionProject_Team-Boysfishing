#if UNITY_EDITOR
using DesktopCompanion.Data;
using System;
using UnityEditor;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// asmdef 없이 실행하는 낚시 순수 로직 회귀 검증 명령이다.
    /// </summary>
    public static class FishingRefactorVerification
    {
        [MenuItem("Tools/DesktopCompanion/Fishing 리팩토링 검증")]
        public static void Run()
        {
            int verifiedCaseCount = Verify();

            Debug.Log(
                $"[FishingRefactorVerification] 검증 성공: " +
                $"{verifiedCaseCount}개 케이스");
        }

        public static int Verify()
        {
            int verifiedCaseCount = 0;

            VerifyWeightCalculations(ref verifiedCaseCount);
            VerifyWeightedSelection(ref verifiedCaseCount);
            VerifySizeRoll(ref verifiedCaseCount);
            VerifyTimingCalculations(ref verifiedCaseCount);
            VerifyCombatCalculations(ref verifiedCaseCount);
            VerifyInventoryFullPolicies(ref verifiedCaseCount);

            return verifiedCaseCount;
        }

        private static void VerifyWeightCalculations(ref int caseCount)
        {
            AssertApproximately(
                "성급 1단계 기본 가중치",
                40f,
                FishingWeightCalculator.CalculateQualityWeight(
                    ItemQuality.OneStar,
                    100f),
                ref caseCount);

            AssertApproximately(
                "성급 2단계 미끼 가중치",
                60f,
                FishingWeightCalculator.CalculateQualityWeight(
                    ItemQuality.TwoStar,
                    100f),
                ref caseCount);

            AssertApproximately(
                "전설 희귀도 떡밥 가중치",
                80f,
                FishingWeightCalculator.CalculateRarityWeight(
                    ItemRarity.Legendary,
                    100f),
                ref caseCount);
        }

        private static void VerifyWeightedSelection(ref int caseCount)
        {
            float[] weights = { 40f, 30f, 30f };

            AssertEqual(
                "누적 가중치 시작 경계",
                0,
                CreateRoller(0f).SelectWeightedIndex(weights),
                ref caseCount);

            AssertEqual(
                "누적 가중치 동일 경계",
                0,
                CreateRoller(40f).SelectWeightedIndex(weights),
                ref caseCount);

            AssertEqual(
                "누적 가중치 다음 구간",
                1,
                CreateRoller(40.001f).SelectWeightedIndex(weights),
                ref caseCount);

            AssertEqual(
                "빈 희귀도 구간 제외",
                1,
                CreateRoller(0f).SelectWeightedIndex(
                    new[] { 0f, 40f, 30f }),
                ref caseCount);

            AssertEqual(
                "전체 가중치 없음",
                -1,
                CreateRoller(0f).SelectWeightedIndex(
                    new[] { 0f, 0f, 0f }),
                ref caseCount);

            AssertEqual(
                "성급 추첨 경계",
                ItemQuality.OneStar,
                CreateRoller(40f).RollFishQuality(0f),
                ref caseCount);
        }

        private static void VerifySizeRoll(ref int caseCount)
        {
            int observedMin = 0;
            int observedMax = 0;
            var upperExclusiveRoller = new FishingCatchRoller(
                (min, max) => min,
                (min, max) =>
                {
                    observedMin = min;
                    observedMax = max;
                    return max - 1;
                });

            AssertApproximately(
                "일반 성급 크기 상한 제외",
                1.9f,
                upperExclusiveRoller.RollSizeInRange(1f, 2f, false),
                ref caseCount);
            AssertEqual(
                "일반 성급 크기 최소 스텝",
                10,
                observedMin,
                ref caseCount);
            AssertEqual(
                "일반 성급 크기 최대 스텝",
                20,
                observedMax,
                ref caseCount);

            var inclusiveMaxRoller = new FishingCatchRoller(
                (min, max) => min,
                (min, max) => max - 1);

            AssertApproximately(
                "5성 크기 상한 포함",
                2f,
                inclusiveMaxRoller.RollSizeInRange(1f, 2f, true),
                ref caseCount);
        }

        private static void VerifyTimingCalculations(ref int caseCount)
        {
            var timing = new FishingTimingCalculator(
                (min, max) => 1f);

            AssertApproximately(
                "최소 크기 전투시간",
                13f,
                timing.CalculateBattleDuration(
                    10f, 2f, 10f, 10f, 0f, 100f, 0f),
                ref caseCount);

            AssertApproximately(
                "최대 크기 전투시간",
                7f,
                timing.CalculateBattleDuration(
                    10f, 2f, 10f, 10f, 0f, 100f, 100f),
                ref caseCount);

            AssertApproximately(
                "전투시간 최솟값",
                2f,
                timing.CalculateBattleDuration(
                    10f, 2f, 1f, 100f, 0f, 100f, 100f),
                ref caseCount);

            AssertApproximately(
                "입질 대기시간 계산",
                20f,
                timing.CalculateNextFishingDelay(5f, 3),
                ref caseCount);

            AssertApproximately(
                "입질 대기시간 최소 제한",
                3f,
                timing.CalculateNextFishingDelay(100f, 0),
                ref caseCount);

            AssertApproximately(
                "입질 대기시간 최대 제한",
                60f,
                timing.CalculateNextFishingDelay(0f, 100),
                ref caseCount);
        }

        private static void VerifyCombatCalculations(ref int caseCount)
        {
            AssertEqual(
                "치명타 수동 피해",
                60,
                new FishingCombatCalculator(() => 0.1f)
                    .CalculateManualDamage(10, 2f, 0.2f, 3f),
                ref caseCount);

            AssertEqual(
                "치명타 동일 경계 제외",
                20,
                new FishingCombatCalculator(() => 0.2f)
                    .CalculateManualDamage(10, 2f, 0.2f, 3f),
                ref caseCount);

            AssertEqual(
                "수동 피해 최솟값",
                1,
                new FishingCombatCalculator(() => 1f)
                    .CalculateManualDamage(0, 0f, 0f, 1f),
                ref caseCount);
        }

        private static void VerifyInventoryFullPolicies(ref int caseCount)
        {
            FishCollectionUpdateResult none = CreateCollectionResult(
                FishCollectionUpdateType.None);
            FishCollectionUpdateResult registered = CreateCollectionResult(
                FishCollectionUpdateType.Registered);
            FishCollectionUpdateResult bestSize = CreateCollectionResult(
                FishCollectionUpdateType.BestSizeImproved);
            FishCollectionUpdateResult bestQualityAndSize = CreateCollectionResult(
                FishCollectionUpdateType.BestQualityAndSizeImproved);

            AssertEqual(
                "항상 중지 정책",
                true,
                EvaluatePolicy(
                    InventoryFullPolicy.StopAndAsk,
                    RecordStopCriterion.Quality,
                    true,
                    none),
                ref caseCount);

            AssertEqual(
                "항상 판매 정책",
                false,
                EvaluatePolicy(
                    InventoryFullPolicy.AlwaysSell,
                    RecordStopCriterion.Quality,
                    false,
                    registered),
                ref caseCount);

            AssertEqual(
                "도감 갱신 실패 시 중지",
                true,
                EvaluatePolicy(
                    InventoryFullPolicy.StopOnRecordUpdate,
                    RecordStopCriterion.Quality,
                    false,
                    none),
                ref caseCount);

            AssertEqual(
                "신규 등록 시 중지",
                true,
                EvaluatePolicy(
                    InventoryFullPolicy.StopOnRecordUpdate,
                    RecordStopCriterion.Size,
                    true,
                    registered),
                ref caseCount);

            AssertEqual(
                "성급 기록 갱신 시 중지",
                true,
                EvaluatePolicy(
                    InventoryFullPolicy.StopOnRecordUpdate,
                    RecordStopCriterion.Quality,
                    true,
                    bestQualityAndSize),
                ref caseCount);

            AssertEqual(
                "크기만 갱신 시 성급 기준 계속",
                false,
                EvaluatePolicy(
                    InventoryFullPolicy.StopOnRecordUpdate,
                    RecordStopCriterion.Quality,
                    true,
                    bestSize),
                ref caseCount);

            AssertEqual(
                "크기 기록 갱신 시 중지",
                true,
                EvaluatePolicy(
                    InventoryFullPolicy.StopOnRecordUpdate,
                    RecordStopCriterion.Size,
                    true,
                    bestSize),
                ref caseCount);

            AssertEqual(
                "기록 갱신 없음",
                false,
                EvaluatePolicy(
                    InventoryFullPolicy.StopOnRecordUpdate,
                    RecordStopCriterion.Size,
                    true,
                    none),
                ref caseCount);
        }

        private static FishingCatchRoller CreateRoller(float floatRoll)
        {
            return new FishingCatchRoller(
                (min, max) => floatRoll,
                (min, max) => min);
        }

        private static FishCollectionUpdateResult CreateCollectionResult(
            FishCollectionUpdateType updateType)
        {
            return new FishCollectionUpdateResult(
                1,
                updateType,
                ItemQuality.OneStar,
                ItemQuality.TwoStar,
                1f,
                2f);
        }

        private static bool EvaluatePolicy(
            InventoryFullPolicy policy,
            RecordStopCriterion criterion,
            bool isCollectionUpdated,
            FishCollectionUpdateResult collectionResult)
        {
            return FishingInventoryFullPolicyEvaluator.ShouldCreatePendingCatch(
                policy,
                criterion,
                isCollectionUpdated,
                collectionResult);
        }

        private static void AssertEqual<T>(
            string caseName,
            T expected,
            T actual,
            ref int caseCount)
        {
            caseCount++;

            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"[FishingRefactorVerification] {caseName} 실패: " +
                    $"expected={expected}, actual={actual}");
            }
        }

        private static void AssertApproximately(
            string caseName,
            float expected,
            float actual,
            ref int caseCount)
        {
            caseCount++;

            if (!Mathf.Approximately(expected, actual))
            {
                throw new InvalidOperationException(
                    $"[FishingRefactorVerification] {caseName} 실패: " +
                    $"expected={expected}, actual={actual}");
            }
        }
    }
}
#endif
