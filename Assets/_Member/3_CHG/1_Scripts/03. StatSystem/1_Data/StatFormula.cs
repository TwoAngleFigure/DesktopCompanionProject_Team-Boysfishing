using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>시트·JSON에서 제공하는 세부스탯별 수식 정의. Stat은 enum 이름으로 지정할 수 있다.</summary>
    [System.Serializable]
    public class StatFormula
    {
        [SerializeField] private PlayerStat_New m_stat = PlayerStat_New.HealthPoint;
        [Tooltip("변수: Str, Tec, Luk, BaseValue. 비어 있으면 해당 항목의 기본 수식을 사용한다.")]
        [SerializeField] private string m_formula;

        public PlayerStat_New Stat => m_stat;
        public string Formula => m_formula;

        public StatFormula() { }
        public StatFormula(PlayerStat_New stat, string formula)
        {
            m_stat = stat;
            m_formula = formula;
        }
    }
}
