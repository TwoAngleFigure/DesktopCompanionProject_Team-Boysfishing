using DesktopCompanion.Entities;
using System;

namespace DesktopCompanion.Systems
{
    public sealed class FishingSession
    {
        public EntityHandle CurrentBattleFish { get; private set; }
        public EntityHandle PendingCatch { get; private set; }
        public FishingAttempt CurrentAttempt { get; private set; }
        public float WaitDuration { get; private set; }
        public float WaitTimeRemaining { get; private set; }
        public float BattleDuration { get; private set; }
        public float BattleTimeRemaining { get; private set; }
        public float AutoAttackTimeRemaining { get; private set; }
        public bool IsResolvingPending { get; private set; }

        public bool HasPendingCatch => PendingCatch.Value != Guid.Empty;

        public void BeginWaiting(float duration)
        {
            WaitDuration = duration;
            WaitTimeRemaining = duration;
            BattleDuration = 0f;
            BattleTimeRemaining = 0f;
            AutoAttackTimeRemaining = 0f;
            CurrentAttempt = null;
        }

        public float AdvanceWaiting(float deltaTime)
        {
            WaitTimeRemaining -= deltaTime;
            return WaitTimeRemaining;
        }

        public void BeginBattle(
            FishingAttempt attempt,
            EntityHandle battleFishHandle)
        {
            CurrentAttempt = attempt;
            CurrentBattleFish = battleFishHandle;
            BattleDuration = attempt.BattleDuration;
            BattleTimeRemaining = attempt.BattleDuration;
            AutoAttackTimeRemaining = 0f;
        }

        public float AdvanceBattle(float deltaTime)
        {
            BattleTimeRemaining -= deltaTime;
            return BattleTimeRemaining;
        }

        public float AdvanceAutoAttack(float deltaTime)
        {
            AutoAttackTimeRemaining -= deltaTime;
            return AutoAttackTimeRemaining;
        }

        public void ResetAutoAttack(float attackInterval)
        {
            AutoAttackTimeRemaining = attackInterval;
        }

        public void SetPendingCatch(EntityHandle caughtHandle)
        {
            PendingCatch = caughtHandle;
        }

        public void ClearPendingCatch()
        {
            PendingCatch = default;
        }

        public bool TryBeginPendingResolution()
        {
            if (IsResolvingPending)
            {
                return false;
            }

            IsResolvingPending = true;
            return true;
        }

        public void EndPendingResolution()
        {
            IsResolvingPending = false;
        }

        public void ResetProgress()
        {
            CurrentBattleFish = default;
            CurrentAttempt = null;
            WaitDuration = 0f;
            WaitTimeRemaining = 0f;
            BattleDuration = 0f;
            BattleTimeRemaining = 0f;
            AutoAttackTimeRemaining = 0f;
        }
    }
}
