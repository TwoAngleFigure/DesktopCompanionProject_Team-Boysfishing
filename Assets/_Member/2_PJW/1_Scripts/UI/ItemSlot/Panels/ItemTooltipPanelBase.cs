using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 툴팁 팝업 패널의 베이스(계획 28 C절). 종류별로 상속해 각자의 레이아웃을 갖는다.
    /// 공통 헤더(아이콘·이름·티어)는 여기서 처리하고, 종류별 본문만 <see cref="ApplyBody"/>에 구현한다.
    /// </summary>
    public abstract class ItemTooltipPanelBase : MonoBehaviour
    {
        [Header("공통 헤더")]
        [SerializeField] private Image m_icon;
        [SerializeField] private TMP_Text m_nameText;
        [SerializeField] private TMP_Text m_tierText;
        [Tooltip("티어 색으로 칠할 뱃지 배경(선택). 슬롯 프리팹과 같은 ItemGradeStyle 에셋을 쓰면 색이 일치한다")]
        [SerializeField] private Image m_tierBadge;

        [Header("등급 색")]
        [Tooltip("슬롯 프리팹에 넣은 것과 같은 에셋을 할당할 것 — 테두리와 뱃지 색이 어긋나지 않게")]
        [SerializeField] private ItemGradeStyle m_style;
        [Tooltip("뱃지 배경의 불투명도. 텍스트는 진한 원색, 배경은 옅은 같은 색으로 칠한다")]
        [SerializeField, Range(0f, 1f)] private float m_badgeBackgroundAlpha = 0.2f;

        /// <summary>이 패널이 담당하는 아이템 종류. 컨트롤러가 이 값으로 패널을 고른다.</summary>
        public abstract TooltipItemKind Kind { get; }

        /// <summary>등급 색 소스(파생 패널이 레어도 뱃지 등에 쓴다). 미할당이면 null.</summary>
        protected ItemGradeStyle Style => m_style;

        /// <summary>에셋 조회가 필요한 하위 아이콘(생산 재료 등)을 위해 패널에 넘긴다.</summary>
        protected AssetProvider AssetProvider { get; private set; }

        public void Apply(ItemTooltipData data, Sprite icon, AssetProvider assets)
        {
            if (data == null)
            {
                return;
            }
            AssetProvider = assets;

            if (m_icon != null)
            {
                m_icon.enabled = icon != null;
                m_icon.sprite = icon;
            }
            if (m_nameText != null) m_nameText.text = data.Name;
            if (m_tierText != null) m_tierText.text = $"T{data.Tier}";

            // 슬롯 테두리와 같은 티어 색 — 슬롯에서 팝업으로 시선이 옮겨가도 같은 색이라 연결이 읽힌다.
            if (m_style != null) ApplyBadge(m_tierText, m_tierBadge, m_style.TierColor(data.Tier));

            ApplyBody(data);
        }

        protected abstract void ApplyBody(ItemTooltipData data);

        /// <summary>등급 뱃지 색칠. 텍스트는 원색, 배경은 같은 색을 옅게(가독성 확보).</summary>
        protected void ApplyBadge(TMP_Text text, Image badge, Color color)
        {
            if (text != null) text.color = color;
            if (badge != null)
            {
                badge.color = new Color(color.r, color.g, color.b, m_badgeBackgroundAlpha);
            }
        }

        /// <summary>IconKey → Sprite. 키가 없거나 에셋이 없으면 null.</summary>
        protected Sprite ResolveIcon(string iconKey)
        {
            if (AssetProvider == null || string.IsNullOrEmpty(iconKey))
            {
                return null;
            }
            AssetProvider.TryGet(iconKey, out Sprite sprite);
            return sprite;
        }

        protected static void SetText(TMP_Text target, string text)
        {
            if (target != null) target.text = text;
        }

        /// <summary>구역 하나를 통째로 켜고 끈다(정보가 없는 구역은 자리도 차지하지 않게).</summary>
        protected static void SetSection(GameObject section, bool visible)
        {
            if (section != null) section.SetActive(visible);
        }

        /// <summary>스탯 변경 목록을 줄바꿈으로 이어 붙인다(장비·소모품 공용).</summary>
        protected static string FormatModifiers(StatModifier[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0)
            {
                return "효과 없음";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < modifiers.Length; i++)
            {
                StatModifier modifier = modifiers[i];
                if (modifier == null) continue;
                if (builder.Length > 0) builder.Append('\n');

                builder.Append(StatLabel(modifier.Stat));
                builder.Append(' ');
                builder.Append(modifier.Operation == ModifierOperation.Multiply
                    ? $"×{modifier.Value:0.##}"
                    : $"{(modifier.Value >= 0f ? "+" : string.Empty)}{modifier.Value:0.##}");
            }
            return builder.Length > 0 ? builder.ToString() : "효과 없음";
        }

        private static string StatLabel(PlayerStat stat) => stat switch
        {
            PlayerStat.DamagePerClick => "공격력",
            PlayerStat.ManualDamagePerHitMultiply => "수동 데미지 배율",
            PlayerStat.BattleTimeVariable => "전투 시간",
            PlayerStat.CriticalChance => "치명타 확률",
            PlayerStat.CriticalMultiply => "치명타 배율",
            PlayerStat.AutoBattleCooltime => "자동 낚시 간격",
            PlayerStat.AutoSpeedPerTime => "자동 공격 속도",
            PlayerStat.AutoDamagePerHitMultiply => "자동 데미지 배율",
            PlayerStat.MapMovementSpeedPerTime => "이동 속도",
            PlayerStat.InventorySize => "인벤토리 칸",
            PlayerStat.ProbabilityAtFishSize => "높은 성급 확률",
            PlayerStat.ProbabilityAtFishRarity => "높은 등급 확률",
            PlayerStat.GoldGettingMultiply => "판매 골드 배율",
            _ => stat.ToString(),
        };
    }
}
