using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 소모품 상세 팝업 패널. 분류·장착 효과·보유 수량을 표시하고, 제작 레시피가 있으면 제작 구역을 켠다.
    /// 제작 재료는 공용 <see cref="MaterialCostRow"/>로 그려 인벤토리·조합창과 같은 모양이 되게 한다.
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
        [Tooltip("제작 골드 비용. 0이면 빈 문자열")]
        [SerializeField] private TMP_Text m_craftGoldText;
        [Tooltip("제작 재료 칸이 생성될 부모")]
        [SerializeField] private Transform m_craftMaterialRoot;
        [SerializeField] private MaterialCostRow m_craftMaterialRowPrefab;

        private readonly List<MaterialCostRow> m_craftRows = new();

        public override TooltipItemKind Kind => TooltipItemKind.Consumables;

        protected override void ApplyBody(ItemTooltipData data)
        {
            ItemTooltipData.ConsumableSection consumable = data.Consumable;
            if (consumable == null)
            {
                SetSection(m_craftSection, false);
                RefreshCraftMaterials(null);
                return;
            }

            SetText(m_categoryText, ItemLabels.MountingArea(consumable.Category));
            SetText(m_quantityText, consumable.Quantity > 0 ? $"{consumable.Quantity}개 보유" : string.Empty);
            SetText(m_modifierText, FormatModifiers(consumable.Modifiers));

            bool hasCraft = consumable.CraftGoldCost > 0 || HasMaterial(consumable.CraftMaterials);
            SetSection(m_craftSection, hasCraft);

            SetText(m_craftGoldText, consumable.CraftGoldCost > 0
                ? consumable.CraftGoldCost.ToString("N0")
                : string.Empty);

            // 구역을 껐더라도 행은 비워 둔다 — 다음 아이템에서 지난 재료가 남아 보이지 않게.
            RefreshCraftMaterials(hasCraft ? consumable.CraftMaterials : null);
        }

        /// <summary>
        /// 제작 재료 칸을 채운다. 팝업 안에서는 hover가 통하지 않으므로(팝업이 레이캐스트를 받지 않는다)
        /// 툴팁 공급자를 넘기지 않고, 보유 수량도 알 수 없어 필요량만 표시한다.
        /// 정의를 찾지 못한 재료도 칸은 만든다 — 조용히 사라지면 데이터 누락을 눈치챌 수 없다.
        /// </summary>
        private void RefreshCraftMaterials(ItemTooltipData.CraftCost[] costs)
        {
            if (m_craftMaterialRowPrefab == null || m_craftMaterialRoot == null)
            {
                return;
            }

            int shown = 0;
            if (costs != null)
            {
                foreach (ItemTooltipData.CraftCost cost in costs)
                {
                    if (cost == null) continue;

                    while (m_craftRows.Count <= shown)
                    {
                        m_craftRows.Add(Instantiate(m_craftMaterialRowPrefab, m_craftMaterialRoot));
                    }

                    ItemSlotVD slotVD = ItemSlotVD.FromData(cost.Item);

                    m_craftRows[shown].gameObject.SetActive(true);
                    m_craftRows[shown].Set(slotVD, ResolveIcon(slotVD != null ? slotVD.IconKey : null), null, cost.Count);
                    shown++;
                }
            }

            for (int i = shown; i < m_craftRows.Count; i++)
            {
                m_craftRows[i].gameObject.SetActive(false);
            }
        }

        private static bool HasMaterial(ItemTooltipData.CraftCost[] costs)
        {
            return costs != null && costs.Length > 0;
        }
    }
}
