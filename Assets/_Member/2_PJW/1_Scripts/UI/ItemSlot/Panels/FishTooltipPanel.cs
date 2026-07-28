using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>물고기 상세 팝업. 종 정보(티어·레어도) + 개체 롤값(성급·크기) + 아쿠아리움 생산 정보.</summary>
    public class FishTooltipPanel : ItemTooltipPanelBase
    {
        private static readonly string[] s_rarityLabels = { "일반", "고급", "희귀", "영웅", "전설" };

        [Header("개체")]
        [SerializeField] private TMP_Text m_rarityText;
        [Tooltip("레어도 색으로 칠할 뱃지 배경(선택). 슬롯 글로우와 같은 색을 쓴다")]
        [SerializeField] private Image m_rarityBadge;
        [SerializeField] private TMP_Text m_starText;
        [SerializeField] private TMP_Text m_sizeText;

        [Header("아쿠아리움 생산")]
        [Tooltip("생산 정보가 없으면(아쿠아리움 밖에서 열리면) 통째로 꺼지는 구역")]
        [SerializeField] private GameObject m_produceSection;
        [SerializeField] private Image m_materialIcon;
        [SerializeField] private TMP_Text m_materialNameText;
        [SerializeField] private TMP_Text m_cycleText;
        [SerializeField] private TMP_Text m_pointsText;
        [SerializeField] private TMP_Text m_capacityText;

        public override TooltipItemKind Kind => TooltipItemKind.Fish;

        protected override void ApplyBody(ItemTooltipData data)
        {
            ItemTooltipData.FishSection fish = data.Fish;
            if (fish == null)
            {
                SetSection(m_produceSection, false);
                return;
            }

            SetText(m_rarityText, RarityLabel(fish.Rarity));

            // 슬롯 글로우와 같은 레어도 색 — 물고기에만 있는 축이라 이 패널에서만 칠한다.
            if (Style != null) ApplyBadge(m_rarityText, m_rarityBadge, Style.RarityColor(fish.Rarity));

            SetText(m_starText, fish.Star > 0 ? $"{fish.Star}성" : "-");
            SetText(m_sizeText, $"{fish.Size:0.##}cm");

            SetText(m_capacityText, $"수용량 {fish.Capacity}");
            SetText(m_materialNameText, fish.MaterialName);

            if (m_materialIcon != null)
            {
                Sprite icon = ResolveIcon(fish.MaterialIconKey);
                m_materialIcon.enabled = icon != null;
                m_materialIcon.sprite = icon;
            }

            // 개체 성급이 반영된 주기·분당 포인트는 AquariumSystem이 있어야 계산된다.
            SetSection(m_produceSection, fish.HasAquariumInfo);
            if (fish.HasAquariumInfo)
            {
                SetText(m_cycleText, $"{fish.CycleSeconds:0.#}초마다");
                SetText(m_pointsText, $"{fish.PointsPerMinute:0.#}pt/분");
            }
        }

        private static string RarityLabel(ItemRarity rarity)
        {
            int index = (int)rarity;
            return index >= 0 && index < s_rarityLabels.Length ? s_rarityLabels[index] : rarity.ToString();
        }
    }
}
