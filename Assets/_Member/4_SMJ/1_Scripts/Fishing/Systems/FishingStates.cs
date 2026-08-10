using System;

namespace DesktopCompanion.Systems
{
    public abstract class FishingStateBase
    {
        public abstract FishingState Id { get; }

        public virtual void Enter()
        {
        }

        public abstract FishingStateSignal Tick(float deltaTime);

        public virtual void Exit()
        {
        }
    }

    public sealed class FishingStateMachine
    {
        private FishingStateBase m_currentState;

        public event Action<FishingState> OnStateChanged;

        public FishingStateBase CurrentState => m_currentState;
        public FishingState State => m_currentState.Id;

        public FishingStateMachine(FishingStateBase initialState)
        {
            m_currentState = initialState ??
                throw new ArgumentNullException(nameof(initialState));
            m_currentState.Enter();
        }

        public FishingStateSignal Tick(float deltaTime)
        {
            return m_currentState.Tick(deltaTime);
        }

        public bool ChangeState(FishingStateBase nextState)
        {
            if (nextState == null)
            {
                throw new ArgumentNullException(nameof(nextState));
            }

            if (ReferenceEquals(m_currentState, nextState))
            {
                return false;
            }

            m_currentState.Exit();
            m_currentState = nextState;
            m_currentState.Enter();
            OnStateChanged?.Invoke(m_currentState.Id);
            return true;
        }
    }

    public sealed class FishingStoppedState : FishingStateBase
    {
        public override FishingState Id => FishingState.Stopped;

        public override FishingStateSignal Tick(float deltaTime)
        {
            return FishingStateSignal.None;
        }
    }

    public sealed class FishingWaitingState : FishingStateBase
    {
        private readonly FishingSession m_session;

        public override FishingState Id => FishingState.Waiting;

        public FishingWaitingState(FishingSession session)
        {
            m_session = session;
        }

        public override FishingStateSignal Tick(float deltaTime)
        {
            return m_session.AdvanceWaiting(deltaTime) <= 0f
                ? FishingStateSignal.BattleReady
                : FishingStateSignal.None;
        }
    }

    public sealed class FishingBattlingState : FishingStateBase
    {
        private readonly FishingSession m_session;
        private readonly FishingBattleService m_battleService;

        public override FishingState Id => FishingState.Battling;

        public FishingBattlingState(
            FishingSession session,
            FishingBattleService battleService)
        {
            m_session = session;
            m_battleService = battleService;
        }

        public override FishingStateSignal Tick(float deltaTime)
        {
            return ConvertOutcome(m_battleService.Tick(m_session, deltaTime));
        }

        public FishingStateSignal ManualAttack()
        {
            return ConvertOutcome(m_battleService.ManualAttack(m_session));
        }

        private static FishingStateSignal ConvertOutcome(
            FishingBattleOutcome outcome)
        {
            return outcome switch
            {
                FishingBattleOutcome.Succeeded => FishingStateSignal.BattleSucceeded,
                FishingBattleOutcome.TimedOut => FishingStateSignal.BattleTimedOut,
                FishingBattleOutcome.InvalidTarget => FishingStateSignal.BattleTargetInvalid,
                _ => FishingStateSignal.None
            };
        }
    }
}
