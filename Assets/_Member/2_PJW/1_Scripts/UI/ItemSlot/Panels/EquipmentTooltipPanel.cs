using System.Text;
using UnityEngine;
using TMPro;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 장비 상세 팝업. 부위·강화 단계·현재 효과·다음 강화 비용.
    /// ※ 계획 28 R-5 — 아쿠아리움에서는 쓰이지 않는다. 장비 창/상점이 생길 때 표현을 다듬는다.
    /// </summary>
    public class EquipmentTooltipPanel : ItemTooltipPanelBase
    {
        [Header("장비")]
        [SerializeField] private TMP_Text m_mountingAreaText;
        [SerializeField] private TMP_Text m_upgradeText;
        [SerializeField] private TMP_Text m_modifierText;

        [Header("다음 강화")]
        [Tooltip("최대 강화면 통째로 꺼지는 구역")]
        [SerializeField] private GameObject m_nextStepSection;
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

            SetText(m_mountingAreaText, MountingAreaLabel(equipment.MountingArea));
            SetText(m_upgradeText, equipment.UpgradeLevel > 0
                ? $"+{equipment.UpgradeLevel} / {equipment.MaxUpgradeLevel}"
                : "무강화");
            SetText(m_modifierText, FormatModifiers(equipment.Modifiers));

            bool hasNext = equipment.NextStep != null;
            SetSection(m_nextStepSection, hasNext);
            if (hasNext)
            {
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

        private static string MountingAreaLabel(EquipmentMountingArea area) => area switch
        {
            EquipmentMountingArea.FishingRod => "낚싯대",
            EquipmentMountingArea.FishingLine => "낚싯줄",
            EquipmentMountingArea.Reel => "릴",
            EquipmentMountingArea.Lure => "루어",
            EquipmentMountingArea.Hat => "모자",
            EquipmentMountingArea.Uniform => "한벌옷",
            EquipmentMountingArea.Gloves => "장갑",
            EquipmentMountingArea.Engine => "배 엔진",
            EquipmentMountingArea.Storage => "물고기 창고",
            EquipmentMountingArea.GPS => "GPS",
            EquipmentMountingArea.Bait => "미끼",
            EquipmentMountingArea.Groundbait => "떡밥",
            _ => area.ToString(),
        };
    }
}
