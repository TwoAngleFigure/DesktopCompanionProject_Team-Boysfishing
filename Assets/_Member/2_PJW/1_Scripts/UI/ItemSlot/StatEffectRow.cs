using UnityEngine;
using TMPro;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 능력치 1행. 스탯 이름과 현재 적용값, 그리고 증감분을 각각의 칸에 표시한다.
    /// 강화창처럼 증감이 있는 곳과 조합창처럼 값만 있는 곳에 공용으로 쓴다 — 증감이 0이면 그 칸은 꺼진다.
    /// 이름 표기는 <see cref="ItemLabels"/>를 쓰므로 툴팁 팝업과 같은 이름으로 보인다.
    /// </summary>
    public class StatEffectRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_nameText;

        [Tooltip("현재 적용 중인 값")]
        [SerializeField] private TMP_Text m_currentText;

        [Tooltip("증감분. 0이면 꺼진다(증감 개념이 없는 창에서는 항상 꺼진 상태가 된다)")]
        [SerializeField] private TMP_Text m_deltaText;

        [Header("증감 색")]
        [SerializeField] private Color m_increaseColor = new Color(0.35f, 0.8f, 0.4f);
        [SerializeField] private Color m_decreaseColor = new Color(0.85f, 0.25f, 0.25f);

        /// <summary>스탯 변경 정의 하나를 그대로 표시한다. 증감 칸은 쓰지 않는다.</summary>
        public void Set(StatModifier modifier)
        {
            if (modifier == null)
            {
                return;
            }
            Set(modifier.Stat, modifier.Operation, modifier.Value, 0f);
        }

        public void Set(PlayerStat stat, ModifierOperation operation, float current, float delta)
        {
            if (m_nameText != null) m_nameText.text = ItemLabels.Stat(stat);
            if (m_currentText != null) m_currentText.text = ItemLabels.Value(operation, current);

            if (m_deltaText != null)
            {
                // 안 오르는 스탯(최대 강화·증감 개념이 없는 창)은 증감 칸을 통째로 비운다.
                bool hasDelta = Mathf.Approximately(delta, 0f) == false;
                m_deltaText.gameObject.SetActive(hasDelta);

                if (hasDelta)
                {
                    bool isIncrease = delta > 0f;
                    m_deltaText.text = $"{(isIncrease ? "+" : "-")} {ItemLabels.Value(operation, Mathf.Abs(delta))}";
                    m_deltaText.color = isIncrease ? m_increaseColor : m_decreaseColor;
                }
            }
        }
    }
}
