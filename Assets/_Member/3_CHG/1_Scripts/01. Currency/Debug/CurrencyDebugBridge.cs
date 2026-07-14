#if UNITY_EDITOR

using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.Debugging
{
    public class CurrencyDebugBridge : MonoBehaviour
    {
        [SerializeField] private int m_addGoldAmount = 1000;

        public void AddGold()
        {
            if (CurrencyDebugSystem.Instance == null)
            {
                Debug.LogError("[CurrencyDebugBridge] CurrencyDebugSystem이 초기화되지 않았습니다.");
                return;
            }

            CurrencyDebugSystem.Instance.AddGold(m_addGoldAmount);
        }
    }
}

#endif