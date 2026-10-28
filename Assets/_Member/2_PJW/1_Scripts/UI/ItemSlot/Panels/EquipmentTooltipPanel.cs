using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 장비 상세 팝업 패널. 장착 부위·강화 단계·현재 효과를 표시하고,
    /// 다음 강화 단계가 있으면 그 비용 구역을 켠다.
    /// </summary>
    public class EquipmentTooltipPanel : ItemTooltipPanelBase
    {
        [Header("장비")]
        [SerializeField] private TMP_Text m_mountingAreaText;
        [SerializeField] private TMP_Text m_upgradeText;
        [Tooltip("강화 단계 색으로 칠할 뱃지 배경(선택). 강화창의 강화 뱃지와 같은 색이 나온다")]
        [SerializeField] private Image m_upgradeBadge;
        [SerializeField] private TMP_Text m_modifierText;

        [Header("다음 강화")]
        [Tooltip("최대 강화면 통째로 꺼지는 구역")]
        [SerializeField] private GameObject m_nextStepSection;
        [SerializeField] private TMP_Text m_nextUpgradeText;
        [SerializeField] private TMP_Text m_nextCostText;

        public override TooltipItemKind Kind => TooltipItemKind.Equipment;

        protected override void ApplyBody(ItemTooltipData data)
        {
            ItemTooltipData.EquipmentSection equipment = data.Equipment;
            if (equipment == null)
            {
                SetSection(m_nextStepSection, false);
                return;
            }

            SetText(m_mountingAreaText, ItemLabels.MountingArea(equipment.MountingArea));
            SetText(m_upgradeText, equipment.UpgradeLevel > 0
                ? $"+{equipment.UpgradeLevel} / {equipment.MaxUpgradeLevel}"
                : "무강화");

            // 강화창의 강화 뱃지와 같은 규칙(2단계마다 팔레트 한 칸)으로 칠한다.
            if (Style != null) ApplyBadge(m_upgradeText, m_upgradeBadge, Style.UpgradeColor(equipment.UpgradeLevel));

            SetText(m_modifierText, FormatModifiers(equipment.Modifiers));

            bool hasNext = equipment.NextStep != null;
            SetSection(m_nextStepSection, hasNext);
            if (hasNext)
            {
                SetText(m_nextUpgradeText, $"+{equipment.UpgradeLevel + 1} / {equipment.MaxUpgradeLevel}");
                SetText(m_nextCostText, FormatCost(equipment.NextStep));
            }
        }

        private static string FormatCost(UpgradeStep step)
        {
            var builder = new StringBuilder();
            if (step.GoldCost > 0) builder.Append($"골드 {step.GoldCost:N0}");

            if (step.MaterialCosts != null)
            {
                foreach (MaterialCost cost in step.MaterialCosts)
                {
                    if (cost?.Material == null) continue;
                    if (builder.Length > 0) builder.Append(", ");
                    builder.Append($"{cost.Material.Name} {cost.Count}");
                }
            }
            return builder.Length > 0 ? builder.ToString() : "무료";
        }
    }
}
