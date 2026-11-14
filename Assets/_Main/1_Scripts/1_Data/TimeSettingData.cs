using UnityEngine;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Data
{
    /// <summary>
    /// 게임 시계의 설정. 1행짜리(ID = 1)다.
    /// TimeSystem이 부팅 시 읽어 시간 변환 규칙(TimeConverter)을 만든다.
    /// 테스트 데이터 경로(GameManager.m_useTestData)에 에셋을 넣으면 시트 값을 대체한다.
    /// </summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Time/Setting")]
    public class TimeSettingData : GameData
    {
        [Tooltip("하루 길이. 현실 시간(분). 이 시간이 흐르면 게임 내 하루(0시~24시)가 지난다")]
        [SerializeField] private float m_dayLengthMinutes = 24f;

        [Tooltip("계절당 일수. 이 일수가 지나면 계절이 바뀐다")]
        [SerializeField] private int m_daysPerSeason = 28;

        [Tooltip("흐름 방식 기본값. Offline=꺼도 흐름 / Unfocused=실행 중이면 흐름 / Focused=포커스 중에만 흐름")]
        [SerializeField] private TimeFlowMode m_flowMode = TimeFlowMode.Unfocused;

        [Tooltip("새 게임의 시작 시각(0 이상 24 미만)")]
        [SerializeField] private float m_startHour = 6f;

        public float DayLengthMinutes => m_dayLengthMinutes;
        public int DaysPerSeason => m_daysPerSeason;
        public TimeFlowMode FlowMode => m_flowMode;
        public float StartHour => m_startHour;
    }
}
