using System.Text;
using UnityEngine;
using TMPro;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 소모품 상세 팝업 패널. 분류·장착 효과·보유 수량을 표시하고, 제작 레시피가 있으면 제작 구역을 켠다.
    /// </summary>
    public class ConsumableTooltipPanel : ItemTooltipPanelBase
    {
        [Header("소모품")]
        [SerializeField] private TMP_Text m_categoryText;
        [SerializeField] private TMP_Text m_quantityText;
        [SerializeField] private TMP_Text m_modifierText;

        [Header("제작")]
        [Tooltip("제작 레시피가 없으면 통째로 꺼지는 구역")]
        [SerializeField] private GameObject m_craftSection;
        [SerializeField] private TMP_Text m_craftText;

        public override TooltipItemKind Kind => TooltipItemKind.Consumables;

        protected override void ApplyBody(ItemTooltipData data)
        {
            ItemTooltipData.ConsumableSection consumable = data.Consumable;
            if (consumable == null)
            {
                SetSection(m_craftSection, false);
                return;
            }

            SetText(m_categoryText, CategoryLabel(consumable.Category));
            SetText(m_quantityText, consumable.Quantity > 0 ? $"{consumable.Quantity}개 보유" : string.Empty);
            SetText(m_modifierText, FormatModifiers(consumable.Modifiers));

            string craft = FormatCraft(consumable);
            SetSection(m_craftSection, string.IsNullOrEmpty(craft) == false);
            SetText(m_craftText, craft);
        }

        private static string FormatCraft(ItemTooltipData.ConsumableSection consumable)
        {
            var builder = new StringBuilder();
            if (consumable.CraftGoldCost > 0) builder.Append($"골드 {consumable.CraftGoldCost:N0}");

            if (consumable.CraftMaterials != null)
            {
                foreach (MaterialCost cost in consumable.CraftMaterials)
                {
                    if (cost?.Material == null) continue;
                    if (builder.Length > 0) builder.Append(", ");
                    builder.Append($"{cost.Material.Name} {cost.Count}");
                }
            }
            return builder.ToString();
        }

        private static string CategoryLabel(EquipmentMountingArea area) => area switch
        {
            EquipmentMountingArea.Bait => "미끼",
            EquipmentMountingArea.Groundbait => "떡밥",
            _ => area.ToString(),
        };
    }
}
