using DesktopCompanion.Core;
using DesktopCompanion.Entities;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public sealed class FishingCatchResolver
    {
        private readonly EntityManager m_entityManager;
        private readonly FishCollectionSystem m_collectionSystem;
        private readonly FishingSettingSystem m_fishingSettingSystem;
        private readonly FishingRewardProcessor m_rewardProcessor;
        private readonly ShopSystem m_shopSystem;

        public bool IsReady =>
            m_entityManager != null &&
            m_collectionSystem != null &&
            m_fishingSettingSystem != null &&
            m_rewardProcessor != null;

        public FishingCatchResolver(
            EntityManager entityManager,
            FishCollectionSystem collectionSystem,
            FishingSettingSystem fishingSettingSystem,
            FishingRewardProcessor rewardProcessor,
            ShopSystem shopSystem)
        {
            m_entityManager = entityManager;
            m_collectionSystem = collectionSystem;
            m_fishingSettingSystem = fishingSettingSystem;
            m_rewardProcessor = rewardProcessor;
            m_shopSystem = shopSystem;
        }

        public FishingCatchPreparation PrepareCatch(
            Entity_BattleFish battleFish)
        {
            if (battleFish == null)
            {
                return FishingCatchPreparation.Failed(
                    FishingResultType.Failed);
            }

            if (!IsReady)
            {
                Debug.LogWarning(
                    "[FishingSystem] 포획 정산에 필요한 시스템을 사용할 수 없습니다.");
                return FishingCatchPreparation.Failed(
                    FishingResultType.Failed);
            }

            FishingRewardResult createResult =
                m_rewardProcessor.TryCreateCaughtFish(
                    battleFish.BattleData.ItemFish,
                    battleFish.Size,
                    battleFish.Quality,
                    out EntityHandle caughtHandle);

            if (createResult != FishingRewardResult.Success)
            {
                FishingResultType resultType =
                    createResult == FishingRewardResult.InventoryFull
                        ? FishingResultType.InventoryFull
                        : FishingResultType.Failed;

                Debug.LogWarning(
                    $"[FishingSystem] 포획 물고기 생성 실패: result={createResult}");
                return FishingCatchPreparation.Failed(resultType);
            }

            bool isCollectionUpdated = TryUpdateCollection(
                battleFish,
                out FishCollectionUpdateResult collectionResult);

            return FishingCatchPreparation.Success(
                caughtHandle,
                isCollectionUpdated,
                collectionResult,
                battleFish.BattleData.Drops,
                battleFish.Quality,
                battleFish.BattleData.IsBoss);
        }

        public IReadOnlyList<FishingGrantedDropInfo> GrantDrops(
            FishingCatchPreparation preparation)
        {
            return m_rewardProcessor.Process(
                preparation.Drops,
                preparation.Quality,
                preparation.IsBoss);
        }

        public FishingCatchResolution FinalizeCatch(
            FishingCatchPreparation preparation,
            bool hasPendingCatch)
        {
            FishingRewardResult finalizeResult =
                m_rewardProcessor.TryFinalizeCaughtFish(
                    preparation.CaughtHandle);

            if (finalizeResult == FishingRewardResult.Success)
            {
                return new FishingCatchResolution(
                    FishingCatchDisposition.Stored,
                    FishingResultType.Success,
                    preparation.CaughtHandle,
                    0);
            }

            if (hasPendingCatch)
            {
                Debug.LogError(
                    "[FishingSystem] 이미 Pending 물고기가 존재합니다.");
                m_entityManager.Destroy(preparation.CaughtHandle);
                return new FishingCatchResolution(
                    FishingCatchDisposition.Failed,
                    FishingResultType.Failed,
                    preparation.CaughtHandle,
                    0);
            }

            if (finalizeResult != FishingRewardResult.InventoryFull)
            {
                Debug.LogWarning(
                    $"[FishingSystem] 포획 물고기 지급 실패로 Pending 처리합니다: " +
                    $"result={finalizeResult}");
                return CreatePendingResolution(preparation.CaughtHandle);
            }

            bool shouldCreatePending =
                FishingInventoryFullPolicyEvaluator.ShouldCreatePendingCatch(
                    m_fishingSettingSystem.CurrentInventoryFullPolicy,
                    m_fishingSettingSystem.CurrentRecordStopCriterion,
                    preparation.IsCollectionUpdated,
                    preparation.CollectionResult);

            if (shouldCreatePending)
            {
                return CreatePendingResolution(preparation.CaughtHandle);
            }

            int earnedGold = 0;
            bool isSold = m_shopSystem != null &&
                m_shopSystem.SellAcquiredItem(
                    preparation.CaughtHandle,
                    out earnedGold);

            if (!isSold)
            {
                Debug.LogWarning(
                    "[FishingSystem] 자동 판매에 실패하여 Pending 처리로 전환합니다.");
                return CreatePendingResolution(preparation.CaughtHandle);
            }

            Debug.Log(
                "[FishingSystem] 인벤토리 부족 물고기 자동 판매 완료. " +
                $"earnedGold={earnedGold}");

            return new FishingCatchResolution(
                FishingCatchDisposition.Sold,
                FishingResultType.Success,
                preparation.CaughtHandle,
                earnedGold);
        }

        public bool TryClaimPendingCatch(EntityHandle pendingHandle)
        {
            return m_rewardProcessor != null &&
                m_rewardProcessor.TryFinalizeCaughtFish(pendingHandle) ==
                FishingRewardResult.Success;
        }

        public bool TrySellPendingCatch(
            EntityHandle pendingHandle,
            out int earnedGold)
        {
            earnedGold = 0;
            return m_shopSystem != null &&
                m_shopSystem.SellAcquiredItem(
                    pendingHandle,
                    out earnedGold);
        }

        private bool TryUpdateCollection(
            Entity_BattleFish battleFish,
            out FishCollectionUpdateResult collectionResult)
        {
            collectionResult = default;

            if (m_collectionSystem == null)
            {
                Debug.LogWarning(
                    "[FishingSystem] FishCollectionSystem을 사용할 수 없습니다.");
                return false;
            }

            if (!m_collectionSystem.TryRegisterCatch(
                    battleFish.BattleData.ItemFish.ID,
                    battleFish.Quality,
                    battleFish.Size,
                    out collectionResult))
            {
                Debug.LogWarning(
                    "[FishingSystem] 포획 물고기의 도감 반영에 실패했습니다.");
                return false;
            }

            return true;
        }

        private static FishingCatchResolution CreatePendingResolution(
            EntityHandle caughtHandle)
        {
            return new FishingCatchResolution(
                FishingCatchDisposition.Pending,
                FishingResultType.InventoryFull,
                caughtHandle,
                0);
        }
    }
}
