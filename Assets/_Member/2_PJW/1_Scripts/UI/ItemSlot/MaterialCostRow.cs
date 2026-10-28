using UnityEngine;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 비용으로 소모되는 재료 1칸. 강화·조합처럼 "무엇이 얼마나 필요한가"를 보여주는 곳에 공용으로 쓴다.
    /// 아이콘·티어 테두리·hover 팝업은 <see cref="ItemSlotView"/>가 맡고,
    /// 이 위젯은 "보유/필요" 수량과 부족 여부 색만 얹는다.
    /// </summary>
    public class MaterialCostRow : MonoBehaviour
    {
        [SerializeField] private ItemSlotView m_slot;

        [Tooltip("\"보유/필요\" 수량")]
        [SerializeField] private TMP_Text m_amountText;

        [Tooltip("충분할 때의 수량 색. 툴팁처럼 보유 수량을 모르는 곳에서도 이 색을 쓴다")]
        [SerializeField] private Color m_enoughColor = Color.black;
        [SerializeField] private Color m_lackColor = new Color(0.85f, 0.25f, 0.25f);

        /// <summary>
        /// 필요량만 표시한다. 보유 수량을 알 수 없는 곳(툴팁 팝업 등)에서 쓴다.
        /// </summary>
        public void Set(ItemSlotVD slotVD, Sprite icon, IItemTooltipSource tooltipSource, int required)
        {
            if (m_slot != null) m_slot.Set(slotVD, icon, tooltipSource);

            if (m_amountText != null)
            {
                m_amountText.text = required.ToString();
                m_amountText.color = m_enoughColor;
            }
        }

        /// <summary>
        /// 재료 1칸을 채운다. <paramref name="tooltipSource"/>는 hover 팝업 상세를 공급할 창이다.
        /// </summary>
        public void Set(ItemSlotVD slotVD, Sprite icon, IItemTooltipSource tooltipSource, int owned, int required)
        {
            if (m_slot != null) m_slot.Set(slotVD, icon, tooltipSource);

            if (m_amountText != null)
            {
                m_amountText.text = $"{owned}/{required}";
                m_amountText.color = owned >= required ? m_enoughColor : m_lackColor;
            }
        }
    }
}
