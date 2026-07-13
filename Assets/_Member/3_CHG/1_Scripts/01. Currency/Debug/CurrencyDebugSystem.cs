#if UNITY_EDITOR

using UnityEngine;

namespace DesktopCompanion.Systems
{
    public class CurrencyDebugSystem : SystemBase
    {
        private CurrencySystem m_currencySystem;

        public static CurrencyDebugSystem Instance { get; private set; }

        public override void Initialize()
        {
            Instance = this;
        }

        public override void PostInitialize()
        {
            // 모든 시스템 등록과 플레이어 복원이 끝난 뒤 CurrencySystem을 조회합니다.
            m_currencySystem = SystemManager.GetSystem<CurrencySystem>();
        }

        public void AddGold(int amount)
        {
            if (m_currencySystem == null)
            {
                Debug.LogError("[CurrencyDebugSystem] CurrencySystem을 찾을 수 없습니다.");
                return;
            }

            bool result = m_currencySystem.AddGold(amount);

            Debug.Log(
                $"[CurrencyDebugSystem] AddGold / " +
                $"amount: {amount}, result: {result}, currentGold: {m_currencySystem.CurrentGold}");
        }

    }
}

#endif