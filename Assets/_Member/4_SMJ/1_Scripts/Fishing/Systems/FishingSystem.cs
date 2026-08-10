using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using System;

namespace DesktopCompanion.Systems
{
    public class FishingSystem : SystemBase, ITickable
    {
        private const bool EnableFishingItemDebugLog = true;

        private readonly float baseBattleDuration = 10f;
        private readonly float minBattleDuration = 2f;

        private StageSystem m_stageSystem;
        private InventorySystem m_inventorySystem;
        private FishingFlowController m_flowController;
        private FishingCatchRoller m_catchRoller;
        private FishingTimingCalculator m_timingCalculator;
        private FishingCombatCalculator m_combatCalculator;

        public event Action<FishingState> OnStateChanged;
        public event Action<EntityHandle, FishCollectionUpdateResult>
            OnFishCaughtPresentation;
        public event Action<FishingResultType> OnFishingResult;
        public event Action<EntityHandle, int, int> OnBattleHpChanged;
        public event Action OnPendingCatchChanged;
        public event Action<FishingGrantedDropInfo> OnItemDropped;

        public FishingState State =>
            m_flowController?.State ?? FishingState.Stopped;
        public float WaitDuration =>
            m_flowController?.Session.WaitDuration ?? 0f;
        public float WaitTimeRemaining =>
            m_flowController?.Session.WaitTimeRemaining ?? 0f;
        public float BattleDuration =>
            m_flowController?.Session.BattleDuration ?? 0f;
        public float BattleTimeRemaining =>
            m_flowController?.Session.BattleTimeRemaining ?? 0f;
        public EntityHandle CurrentBattleFish =>
            m_flowController?.Session.CurrentBattleFish ?? default;
        public bool HasPendingCatch =>
            m_flowController?.Session.HasPendingCatch ?? false;
        public EntityHandle PendingCatch =>
            m_flowController?.Session.PendingCatch ?? default;

        public override void Initialize()
        {
            m_catchRoller = new FishingCatchRoller();
            m_timingCalculator = new FishingTimingCalculator();
            m_combatCalculator = new FishingCombatCalculator();
        }

        public override void PostInitialize()
        {
            m_stageSystem = SystemManager.GetSystem<StageSystem>();
            PlayerSystem playerSystem =
                SystemManager.GetSystem<PlayerSystem>();
            FishCollectionSystem collectionSystem =
                SystemManager.GetSystem<FishCollectionSystem>();
            FishingSettingSystem fishingSettingSystem =
                SystemManager.GetSystem<FishingSettingSystem>();
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
            ShopSystem shopSystem = SystemManager.GetSystem<ShopSystem>();

            FishingRewardProcessor rewardProcessor =
                m_inventorySystem == null
                    ? null
                    : new FishingRewardProcessor(
                        EntityManager,
                        m_inventorySystem);

            var attemptService = new FishingAttemptService(
                EntityManager,
                m_stageSystem,
                playerSystem,
                m_catchRoller,
                m_timingCalculator,
                baseBattleDuration,
                minBattleDuration,
                LogFishingItemDebug);
            var battleService = new FishingBattleService(
                EntityManager,
                playerSystem,
                m_combatCalculator);
            var catchResolver = new FishingCatchResolver(
                EntityManager,
                collectionSystem,
                fishingSettingSystem,
                rewardProcessor,
                shopSystem);

            m_flowController = new FishingFlowController(
                EntityManager,
                attemptService,
                battleService,
                catchResolver);

            m_flowController.OnStateChanged += HandleStateChanged;
            m_flowController.OnFishCaughtPresentation +=
                HandleFishCaughtPresentation;
            m_flowController.OnFishingResult += HandleFishingResult;
            m_flowController.OnBattleHpChanged += HandleBattleHpChanged;
            m_flowController.OnPendingCatchChanged +=
                HandlePendingCatchChanged;
            m_flowController.OnItemDropped += HandleItemDropped;

            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged += HandleInventoryChanged;
            }

            if (m_stageSystem != null)
            {
                m_stageSystem.OnStageChanged += HandleStageChanged;
            }
        }

        public void Tick(float deltaTime)
        {
            m_flowController?.Tick(deltaTime);
        }

        public void StartFishing()
        {
            m_flowController?.StartFishing();
        }

        public void StopFishing()
        {
            m_flowController?.StopFishing();
        }

        public void ManualAttack()
        {
            m_flowController?.ManualAttack();
        }

        public bool TryClaimPendingCatch()
        {
            return m_flowController != null &&
                m_flowController.TryClaimPendingCatch();
        }

        public bool TrySellPendingCatch(out int earnedGold)
        {
            if (m_flowController == null)
            {
                earnedGold = 0;
                return false;
            }

            return m_flowController.TrySellPendingCatch(out earnedGold);
        }

        public ItemData GetItemData(ItemType itemType, int dataId)
        {
            if (dataId <= 0)
            {
                return null;
            }

            return itemType switch
            {
                ItemType.Fish =>
                    DataManager.GetData<ItemData_Fish>(dataId),
                ItemType.Materials =>
                    DataManager.GetData<ItemData_Materials>(dataId),
                ItemType.Consumables =>
                    DataManager.GetData<ItemData_Consumables>(dataId),
                ItemType.Equipment =>
                    DataManager.GetData<ItemData_Equipment>(dataId),
                _ => null
            };
        }

        private static void LogFishingItemDebug(string message)
        {
            if (EnableFishingItemDebugLog)
            {
                UnityEngine.Debug.Log($"[FishingDebug] {message}");
            }
        }

        private void HandleStageChanged(int stageDataId)
        {
            m_flowController.HandleStageChanged(stageDataId);
        }

        private void HandleInventoryChanged()
        {
            m_flowController.HandleInventoryChanged();
        }

        private void HandleStateChanged(FishingState state)
        {
            OnStateChanged?.Invoke(state);
        }

        private void HandleFishCaughtPresentation(
            EntityHandle caughtHandle,
            FishCollectionUpdateResult collectionResult)
        {
            OnFishCaughtPresentation?.Invoke(caughtHandle, collectionResult);
        }

        private void HandleFishingResult(FishingResultType resultType)
        {
            OnFishingResult?.Invoke(resultType);
        }

        private void HandleBattleHpChanged(
            EntityHandle handle,
            int currentHp,
            int maxHp)
        {
            OnBattleHpChanged?.Invoke(handle, currentHp, maxHp);
        }

        private void HandlePendingCatchChanged()
        {
            OnPendingCatchChanged?.Invoke();
        }

        private void HandleItemDropped(FishingGrantedDropInfo dropInfo)
        {
            OnItemDropped?.Invoke(dropInfo);
        }
    }
}
