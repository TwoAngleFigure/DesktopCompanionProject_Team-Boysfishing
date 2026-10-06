using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 툴팁 팝업 패널의 베이스. 아이템 종류별로 상속해 각자의 레이아웃을 갖는다.
    /// 공통 헤더(아이콘·이름·티어 뱃지)를 처리하고, 종류별 본문은 <see cref="ApplyBody"/>가 채운다.
    /// </summary>
    public abstract class ItemTooltipPanelBase : MonoBehaviour
    {
        [Header("공통 헤더")]
        [SerializeField] private Image m_icon;
        [SerializeField] private TMP_Text m_nameText;
        [SerializeField] private TMP_Text m_tierText;
        [Tooltip("티어 색으로 칠할 뱃지 배경(선택). 슬롯 프리팹과 같은 ItemGradeStyle 에셋을 쓰면 색이 일치한다")]
        [SerializeField] private Image m_tierBadge;

        [Tooltip("ItemData.Description을 표시한다(선택). 설명이 비어 있으면 \"이름 입니다\"로 대체한다")]
        [SerializeField] private TMP_Text m_descriptionText;

        [Header("등급 색")]
        [Tooltip("슬롯 프리팹에 넣은 것과 같은 에셋을 할당할 것 — 테두리와 뱃지 색이 어긋나지 않게")]
        [SerializeField] private ItemGradeStyle m_style;
        [Tooltip("뱃지 배경의 불투명도. 텍스트는 진한 원색, 배경은 옅은 같은 색으로 칠한다")]
        [SerializeField, Range(0f, 1f)] private float m_badgeBackgroundAlpha = 0.2f;

        /// <summary>이 패널이 담당하는 아이템 종류. 컨트롤러가 이 값으로 패널을 선택한다.</summary>
        public abstract TooltipItemKind Kind { get; }

        /// <summary>등급 색 공급 에셋. 파생 패널이 레어도 뱃지 등에 쓴다. 미할당이면 null이다.</summary>
        protected ItemGradeStyle Style => m_style;

        /// <summary>하위 아이콘(생산 재료 등) 조회에 쓰는 에셋 공급자. Apply 시점에 주입된다.</summary>
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

            // 설명이 없는 아이템이 대부분이다. 줄을 감추면 툴팁 높이가 아이템마다 들쭉날쭉해지므로
            // 이름으로 기본 문장을 만들어 채운다. 이름까지 없을 때만 줄을 감춘다.
            if (m_descriptionText != null)
            {
                string description = data.Description;
                if (string.IsNullOrEmpty(description))
                {
                    description = string.IsNullOrEmpty(data.Name) ? string.Empty : $"{data.Name} 입니다";
                }

                bool hasDescription = string.IsNullOrEmpty(description) == false;
                m_descriptionText.gameObject.SetActive(hasDescription);
                if (hasDescription) m_descriptionText.text = description;
            }

            ApplyBody(data);
        }

        protected abstract void ApplyBody(ItemTooltipData data);

        /// <summary>등급 뱃지를 색칠한다. 텍스트는 원색, 배경은 같은 색을 지정 불투명도로 칠한다.</summary>
        protected void ApplyBadge(TMP_Text text, Image badge, Color color)
        {
            if (text != null) text.color = color;
            if (badge != null)
            {
                badge.color = new Color(color.r, color.g, color.b, m_badgeBackgroundAlpha);
            }
        }

        /// <summary>IconKey를 Sprite로 해석한다. 키가 없거나 에셋을 찾지 못하면 null을 반환한다.</summary>
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

        /// <summary>구역 하나를 통째로 켜고 끈다. 꺼진 구역은 레이아웃 자리도 차지하지 않는다.</summary>
        protected static void SetSection(GameObject section, bool visible)
        {
            if (section != null) section.SetActive(visible);
        }

        /// <summary>스탯 변경 목록을 줄바꿈으로 이어 붙인다. 항목이 없으면 "효과 없음"을 반환한다.</summary>
        protected static string FormatModifiers(StatModifier[] modifiers) => ItemLabels.Modifiers(modifiers);
    }
}
