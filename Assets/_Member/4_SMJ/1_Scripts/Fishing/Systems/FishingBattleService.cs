using DesktopCompanion.Core;
using DesktopCompanion.Entities;
using System;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public sealed class FishingBattleService
    {
        private readonly EntityManager m_entityManager;
        private readonly PlayerSystem m_playerSystem;
        private readonly FishingCombatCalculator m_combatCalculator;

        public event Action<EntityHandle, int, int> OnHpChanged;

        public FishingBattleService(
            EntityManager entityManager,
            PlayerSystem playerSystem,
            FishingCombatCalculator combatCalculator)
        {
            m_entityManager = entityManager;
            m_playerSystem = playerSystem;
            m_combatCalculator = combatCalculator;
        }

        public FishingBattleOutcome Tick(
            FishingSession session,
            float deltaTime)
        {
            if (session.AdvanceBattle(deltaTime) <= 0f)
            {
                return FishingBattleOutcome.TimedOut;
            }

            if (session.AdvanceAutoAttack(deltaTime) > 0f)
            {
                return FishingBattleOutcome.InProgress;
            }

            int damage = 1;
            float attackInterval = 1f;

            if (m_playerSystem != null)
            {
                damage = Mathf.RoundToInt(
                    m_playerSystem.BaseDamagePerClick *
                    m_playerSystem.BaseAutoDamagePerHitMultiply);
                attackInterval = m_playerSystem.BaseAutoSpeedPerTime;
            }

            Debug.Log($"[FishingSystem] 자동 공격: damage={damage}");

            FishingBattleOutcome outcome = ApplyDamage(session, damage);
            session.ResetAutoAttack(attackInterval);
            return outcome;
        }

        public FishingBattleOutcome ManualAttack(FishingSession session)
        {
            int damage = CalculateManualDamage();

            Debug.Log($"[FishingSystem] 수동 공격: damage={damage}");

            return ApplyDamage(session, damage);
        }

        private FishingBattleOutcome ApplyDamage(
            FishingSession session,
            int damage)
        {
            Entity_BattleFish battleFish =
                m_entityManager.Get<Entity_BattleFish>(
                    session.CurrentBattleFish);

            if (battleFish == null)
            {
                Debug.LogWarning(
                    "[FishingSystem] 데미지 적용 실패: 현재 전투 물고기가 없습니다.");
                return FishingBattleOutcome.InvalidTarget;
            }

            battleFish.ApplyDamage(damage);

            OnHpChanged?.Invoke(
                session.CurrentBattleFish,
                battleFish.CurrentHp,
                battleFish.BattleData.MaxHp);

            Debug.Log(
                $"[FishingSystem] 물고기 HP : {battleFish.Name} " +
                $"HP={battleFish.CurrentHp}/{battleFish.BattleData.MaxHp}");

            return battleFish.CurrentHp <= 0
                ? FishingBattleOutcome.Succeeded
                : FishingBattleOutcome.InProgress;
        }

        private int CalculateManualDamage()
        {
            if (m_playerSystem == null)
            {
                Debug.LogWarning(
                    "[FishingSystem] PlayerSystem을 찾지 못해 기본 수동 공격 데미지를 사용합니다.");
                return 1;
            }

            return m_combatCalculator.CalculateManualDamage(
                m_playerSystem.BaseDamagePerClick,
                m_playerSystem.BaseManualDamagePerHitMultiply,
                m_playerSystem.BaseCriticalChance,
                m_playerSystem.BaseCriticalMultiply);
        }
    }
}
