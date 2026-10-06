using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 재료 생산 경과를 표시하는 목록 행. 원형 게이지 안에 재료 슬롯을 두고
    /// "현재/필요" 포인트와 현재 정렬 기준의 값을 함께 표시한다.
    /// 1초 정산 경로에서는 <see cref="SetProgress"/>만 호출해 수치만 갱신한다.
    /// </summary>
    public class AquariumMaterialGaugeRow : MonoBehaviour
    {
        [Tooltip("Image Type=Filled, Fill Method=Radial 360")]
        [SerializeField] private Image m_gauge;

        [Tooltip("게이지 내부 재료 슬롯(아이콘 + 티어 테두리 + hover 팝업)")]
        [SerializeField] private ItemSlotView m_slot;

        [SerializeField] private TMP_Text m_nameText;

        [Tooltip("\"현재/필요\" 포인트")]
        [SerializeField] private TMP_Text m_amountText;

        [Tooltip("현재 정렬 기준의 값(진행률로 정렬 중이면 27%, 시간당 개수로 정렬 중이면 9.6개/h …)")]
        [SerializeField] private TMP_Text m_sortValueText;

        private int m_materialId;

        /// <summary>이 행이 표시 중인 재료의 dataId.</summary>
        public int MaterialId => m_materialId;

        public void Set(AquariumMaterialVD vd, ItemSlotVD slotVD, Sprite icon, IItemTooltipSource tooltipSource,
                        AquariumMaterialSortKey sortKey)
        {
            if (vd == null)
            {
                return;
            }

            m_materialId = vd.MaterialId;

            if (m_slot != null) m_slot.Set(slotVD, icon, tooltipSource);
            if (m_nameText != null) m_nameText.text = vd.MaterialName;

            SetProgress(vd, sortKey);
        }

        /// <summary>게이지·포인트·정렬 값만 갱신한다. 슬롯과 이름은 건드리지 않는다.</summary>
        public void SetProgress(AquariumMaterialVD vd, AquariumMaterialSortKey sortKey)
        {
            if (vd == null)
            {
                return;
            }

            if (m_gauge != null) m_gauge.fillAmount = vd.Progress;
            if (m_amountText != null) m_amountText.text = $"{vd.Points}/{vd.Required}";
            if (m_sortValueText != null) m_sortValueText.text = AquariumSort.FormatValue(vd, sortKey);
        }
    }
}
