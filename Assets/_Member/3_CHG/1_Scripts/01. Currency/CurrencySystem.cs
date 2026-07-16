using System;
using DesktopCompanion.Entities;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public class CurrencySystem : SystemBase
    {
        private Entity_Player m_playerEntity;

        public int CurrentGold => m_playerEntity?.Gold ?? 0;

        public event Action<int> OnGoldChanged;

        public override void PostInitialize()
        {
            PlayerSystem playerSystem = SystemManager.GetSystem<PlayerSystem>();

            if (playerSystem == null)
            {
                Debug.LogError("[CurrencySystem] PlayerSystem을 찾을 수 없습니다.");
                return;
            }

            m_playerEntity = EntityManager.Get<Entity_Player>(playerSystem.PlayerHandle);

            if (m_playerEntity == null)
            {
                Debug.LogError("[CurrencySystem] Entity_Player를 찾을 수 없습니다.");
                return;
            }

            Debug.Log($"[CurrencySystem] 초기화 완료. CurrentGold: {CurrentGold}");
        }

        public bool AddGold(int amount)
        {
            if (m_playerEntity == null)
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            int nextGold = m_playerEntity.Gold + amount;

            if (nextGold < 0)
            {
                return false;
            }

            m_playerEntity.SetGold(nextGold);
            OnGoldChanged?.Invoke(nextGold);

            return true;
        }

#if UNITY_EDITOR
        public void SetGold(int gold)
        {
            if (m_playerEntity == null)
            {
                return;
            }

            int nextGold = Math.Max(0, gold);

            if (nextGold == m_playerEntity.Gold)
            {
                return;
            }

            m_playerEntity.SetGold(nextGold);
            OnGoldChanged?.Invoke(nextGold);
        }
#endif
    }
}