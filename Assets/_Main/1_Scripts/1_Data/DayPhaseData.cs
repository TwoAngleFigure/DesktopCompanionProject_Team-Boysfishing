using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>
    /// 시간대 정의. 시간대 1개당 1행이다.
    /// 한 시간대는 다음 시간대의 시작 시각까지이고, 시작 시각이 가장 늦은 시간대는 자정을 넘어
    /// 첫 시간대의 시작 시각까지 이어진다. Name은 표시 이름, Key는 시간 조건(Time=)에서 참조하는 이름이다.
    /// </summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Time/DayPhase")]
    public class DayPhaseData : GameData
    {
        [Tooltip("시간 조건에서 참조하는 이름. 영문자로 시작한다. 예: Night")]
        [SerializeField] private string m_key;

        [Tooltip("시작 시각(0 이상 24 미만)")]
        [SerializeField] private float m_startHour;

        public string Key => m_key;
        public float StartHour => m_startHour;
    }
}
