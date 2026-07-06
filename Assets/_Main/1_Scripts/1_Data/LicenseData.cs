using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>
    /// 낚시 라이센스 등급 정의(G7). "보스 클리어 → 해금 자격 → 재화 소모 → 등급 상승"
    /// 규칙 자체는 System(라이센스/스테이지) 영역.
    /// </summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/License")]
    public class LicenseData : GameData
    {
        [SerializeField] private int m_grade;                    // 라이센스 등급
        // 해금 조건 2가지: ① 특정 보스 처치 ② 골드 소모 (판정 로직은 System 영역)
        [SerializeField] private BattleFishData m_requiredBoss;  // 조건①: 처치해야 할 보스 (null=조건 없음)
        [SerializeField] private int m_upgradeGoldCost;          // 조건②: 이 등급으로 올리는 재화 비용 (TODO: 밸런스)

        public int Grade => m_grade;
        public BattleFishData RequiredBoss => m_requiredBoss;
        public int UpgradeGoldCost => m_upgradeGoldCost;
    }
}
