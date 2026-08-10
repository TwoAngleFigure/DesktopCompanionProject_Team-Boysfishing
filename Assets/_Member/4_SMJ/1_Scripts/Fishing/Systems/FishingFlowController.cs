using DesktopCompanion.Core;
using DesktopCompanion.Entities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public sealed class FishingFlowController
    {
        private readonly EntityManager m_entityManager;
        private readonly FishingAttemptService m_attemptService;
        private readonly FishingBattleService m_battleService;
        private readonly FishingCatchResolver m_catchResolver;
        private readonly FishingStoppedState m_stoppedState;
        private readonly FishingWaitingState m_waitingState;
        private readonly FishingBattlingState m_battlingState;
        private readonly FishingStateMachine m_stateMachine;

        public event Action<FishingState> OnStateChanged;
        public event Action<EntityHandle, FishCollectionUpdateResult>
            OnFishCaughtPresentation;
        public event Action<FishingResultType> OnFishingResult;
        public event Action<EntityHandle, int, int> OnBattleHpChanged;
        public event Action OnPendingCatchChanged;
        public event Action<FishingGrantedDropInfo> OnItemDropped;

        public FishingSession Session { get; }
        public FishingState State => m_stateMachine.State;

        public FishingFlowController(
            EntityManager entityManager,
            FishingAttemptService attemptService,
            FishingBattleService battleService,
            FishingCatchResolver catchResolver)
        {
            m_entityManager = entityManager;
            m_attemptService = attemptService;
            m_battleService = battleService;
            m_catchResolver = catchResolver;

            Session = new FishingSession();
            m_stoppedState = new FishingStoppedState();
            m_waitingState = new FishingWaitingState(Session);
            m_battlingState = new FishingBattlingState(
                Session,
                m_battleService);
            m_stateMachine = new FishingStateMachine(m_stoppedState);

            m_stateMachine.OnStateChanged += HandleStateChanged;
            m_battleService.OnHpChanged += HandleBattleHpChanged;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            FishingStateSignal signal = m_stateMachine.Tick(deltaTime);
            HandleStateSignal(signal);
        }

        public void StartFishing()
        {
            if (Session.HasPendingCatch)
            {
                Debug.LogWarning(
                    "[FishingSystem] Pending 물고기를 먼저 처리해야 합니다.");
                return;
            }

            if (State != FishingState.Stopped)
            {
                Debug.Log(
                    $"[FishingSystem] 이미 낚시 진행 중입니다. state={State}");
                return;
            }

            if (!CanStartFishing())
            {
                StopAfterPreparationFailure(
                    "필수 낚시 시스템을 사용할 수 없습니다.");
                return;
            }

            if (!TryScheduleNextFishing())
            {
                StopAfterPreparationFailure(
                    "입질 대기시간을 계산할 수 없습니다.");
                return;
            }

            Debug.Log(
                "[FishingSystem] 자동 낚시 시작 " +
                $"대기시간={Session.WaitTimeRemaining:0.00}초");
        }

        public void StopFishing()
        {
            Debug.Log("[FishingSystem] 자동 낚시 중지");

            ResetFishingProgress();
            m_stateMachine.ChangeState(m_stoppedState);
        }

        public void ManualAttack()
        {
            if (State != FishingState.Battling)
            {
                Debug.Log(
                    "[FishingSystem] 수동 공격 실패: 전투 중이 아닙니다.");
                return;
            }

            HandleStateSignal(m_battlingState.ManualAttack());
        }

        public bool TryClaimPendingCatch()
        {
            if (!Session.HasPendingCatch ||
                !Session.TryBeginPendingResolution())
            {
                return false;
            }

            EntityHandle pendingHandle = Session.PendingCatch;

            if (!m_catchResolver.TryClaimPendingCatch(pendingHandle))
            {
                Session.EndPendingResolution();
                return false;
            }

            Session.ClearPendingCatch();
            Session.EndPendingResolution();

            OnPendingCatchChanged?.Invoke();

            Debug.Log(
                "[FishingSystem] Pending 물고기 지급 완료. 낚시를 재개합니다.");

            StartFishing();
            return true;
        }

        public bool TrySellPendingCatch(out int earnedGold)
        {
            earnedGold = 0;

            if (!Session.HasPendingCatch)
            {
                return false;
            }

            EntityHandle pendingHandle = Session.PendingCatch;

            if (!m_catchResolver.TrySellPendingCatch(
                    pendingHandle,
                    out earnedGold))
            {
                Debug.LogWarning(
                    "[FishingSystem] Pending 물고기 판매에 실패했습니다.");
                return false;
            }

            Session.ClearPendingCatch();
            OnPendingCatchChanged?.Invoke();

            StartFishing();
            return true;
        }

        public void HandleStageChanged(int stageDataId)
        {
            if (State == FishingState.Stopped)
            {
                return;
            }

            ResetFishingProgress();
            m_stateMachine.ChangeState(m_waitingState);

            Debug.Log(
                $"[FishingSystem] 스테이지 변경 감지: " +
                $"stageId={stageDataId}, 낚시 풀 갱신");
        }

        public void HandleInventoryChanged()
        {
            if (!Session.HasPendingCatch || Session.IsResolvingPending)
            {
                return;
            }

            TryClaimPendingCatch();
        }

        private void HandleStateSignal(FishingStateSignal signal)
        {
            switch (signal)
            {
                case FishingStateSignal.BattleReady:
                    StartBattle();
                    break;

                case FishingStateSignal.BattleSucceeded:
                    CompleteBattle();
                    break;

                case FishingStateSignal.BattleTimedOut:
                    FailBattle("제한시간 초과");
                    break;

                case FishingStateSignal.BattleTargetInvalid:
                    FailBattle("현재 전투 물고기 없음");
                    break;
            }
        }

        private void StartBattle()
        {
            FishingAttemptStartResult result =
                m_attemptService.StartAttempt();

            if (!result.IsSuccess)
            {
                StopAfterPreparationFailure(
                    "전투 시작 준비에 실패했습니다.");
                return;
            }

            Session.BeginBattle(result.Attempt, result.BattleFishHandle);
            m_stateMachine.ChangeState(m_battlingState);

            Entity_BattleFish battleFish =
                m_entityManager.Get<Entity_BattleFish>(
                    result.BattleFishHandle);

            OnBattleHpChanged?.Invoke(
                result.BattleFishHandle,
                battleFish.CurrentHp,
                result.Attempt.BattleFishData.MaxHp);

            Debug.Log(
                $"[FishingSystem] 전투 시작: " +
                $"{result.Attempt.BattleFishData.Name}, " +
                $"HP={battleFish.CurrentHp}/{result.Attempt.BattleFishData.MaxHp}, " +
                $"Size={result.Attempt.Size:0.00}, " +
                $"Quality={result.Attempt.Quality}, " +
                $"제한시간={Session.BattleTimeRemaining:0.00}초");
        }

        private void CompleteBattle()
        {
            Entity_BattleFish battleFish =
                m_entityManager.Get<Entity_BattleFish>(
                    Session.CurrentBattleFish);
            FishingCatchPreparation preparation =
                m_catchResolver.PrepareCatch(battleFish);

            if (!preparation.IsSuccess)
            {
                FinishCurrentAttempt(preparation.FailureResultType);
                return;
            }

            OnFishCaughtPresentation?.Invoke(
                preparation.CaughtHandle,
                preparation.CollectionResult);

            IReadOnlyList<FishingGrantedDropInfo> grantedDrops =
                m_catchResolver.GrantDrops(preparation);

            foreach (FishingGrantedDropInfo grantedDrop in grantedDrops)
            {
                if (grantedDrop.IsValid)
                {
                    OnItemDropped?.Invoke(grantedDrop);
                }
            }

            FishingCatchResolution resolution =
                m_catchResolver.FinalizeCatch(
                    preparation,
                    Session.HasPendingCatch);

            ResolveCatch(resolution);
        }

        private void ResolveCatch(FishingCatchResolution resolution)
        {
            if (resolution.Disposition == FishingCatchDisposition.Pending)
            {
                Session.SetPendingCatch(resolution.CaughtHandle);
                StopFishing();
                OnPendingCatchChanged?.Invoke();
                OnFishingResult?.Invoke(resolution.ResultType);
                return;
            }

            if (resolution.Disposition == FishingCatchDisposition.Failed &&
                Session.HasPendingCatch)
            {
                StopFishing();
                OnFishingResult?.Invoke(resolution.ResultType);
                return;
            }

            FinishCurrentAttempt(resolution.ResultType);
        }

        private void FailBattle(string reason)
        {
            Debug.Log($"[FishingSystem] 포획 실패: reason={reason}");
            FinishCurrentAttempt(FishingResultType.Failed);
        }

        private void FinishCurrentAttempt(FishingResultType resultType)
        {
            ResetFishingProgress();

            if (!TryScheduleNextFishing())
            {
                StopAfterPreparationFailure(
                    "다음 입질 대기시간을 계산할 수 없습니다.");
                return;
            }

            OnFishingResult?.Invoke(resultType);
        }

        private bool TryScheduleNextFishing()
        {
            if (!m_attemptService.TryCalculateNextFishingDelay(
                    out float waitDuration))
            {
                return false;
            }

            Session.BeginWaiting(waitDuration);
            m_stateMachine.ChangeState(m_waitingState);

            Debug.Log(
                $"[FishingSystem] 다음 입질 대기: " +
                $"{Session.WaitTimeRemaining:0.00}초");
            return true;
        }

        private bool CanStartFishing()
        {
            return m_attemptService.IsReady && m_catchResolver.IsReady;
        }

        private void StopAfterPreparationFailure(string reason)
        {
            Debug.LogWarning(
                $"[FishingSystem] 낚시 준비 실패: reason={reason}");

            ResetFishingProgress();
            m_stateMachine.ChangeState(m_stoppedState);
            OnFishingResult?.Invoke(FishingResultType.Failed);
        }

        private void ResetFishingProgress()
        {
            m_entityManager.Destroy(Session.CurrentBattleFish);
            Session.ResetProgress();
        }

        private void HandleStateChanged(FishingState state)
        {
            OnStateChanged?.Invoke(state);
            Debug.Log($"[FishingSystem] 상태 변경: {state}");
        }

        private void HandleBattleHpChanged(
            EntityHandle handle,
            int currentHp,
            int maxHp)
        {
            OnBattleHpChanged?.Invoke(handle, currentHp, maxHp);
        }
    }
}
