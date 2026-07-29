using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아이템 1칸의 공용 표시 위젯. ItemData 계열 전 종류에 쓴다.
    /// 아이콘·티어 테두리·레어도 글로우(물고기)·성급(물고기)·스택 수량을 표시하고,
    /// 자체 hover 감지로 <see cref="ItemTooltipController"/>에 상세 팝업을 요청·해제한다.
    ///
    /// 프리팹 구성 순서(뒤 → 앞): 글로우 → 배경 → 아이콘 → 테두리 → 성급.
    /// </summary>
    public class ItemSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image m_icon;

        [Header("티어 테두리 — 전 종류")]
        [Tooltip("테두리 프레임 이미지(9-slice). 색만 티어에 따라 바뀐다")]
        [SerializeField] private Image m_tierBorder;

        [Header("레어도 글로우 — 물고기 전용")]
        [Tooltip("소프트 글로우 이미지. 레어도가 없는 종류에서는 꺼진다")]
        [SerializeField] private Image m_rarityGlow;

        [Header("성급 — 물고기 전용")]
        [Tooltip("물고기일 때만 켜지는 성급 이미지. 숫자 텍스트는 이 오브젝트의 하위")]
        [SerializeField] private GameObject m_starRoot;
        [SerializeField] private TMP_Text m_starText;

        [Header("공통")]
        [SerializeField] private ItemGradeStyle m_style;
        [Tooltip("스택 수량(재료·소모품). 1 이하면 빈 문자열")]
        [SerializeField] private TMP_Text m_quantityText;

        private ItemSlotVD m_vd;
        private IItemTooltipSource m_tooltipSource;
        private Sprite m_iconSprite;          // 팝업 헤더에 같은 아이콘을 쓰기 위해 보관
        private bool m_isHovered;

        public ItemSlotVD Data => m_vd;

        /// <summary>
        /// 슬롯 내용을 채운다. <paramref name="tooltipSource"/>가 null이면 hover해도 팝업이 뜨지 않는다.
        /// 이미 hover 중인 상태에서 호출되면 팝업도 새 내용으로 갱신한다.
        /// </summary>
        public void Set(ItemSlotVD vd, Sprite icon, IItemTooltipSource tooltipSource)
        {
            m_vd = vd;
            m_iconSprite = icon;
            m_tooltipSource = tooltipSource;

            if (m_icon != null)
            {
                m_icon.enabled = icon != null;
                m_icon.sprite = icon;
            }

            ApplyTierBorder(vd);
            ApplyRarityGlow(vd);
            ApplyStar(vd);

            if (m_quantityText != null)
            {
                m_quantityText.text = vd != null && vd.Quantity > 1 ? vd.Quantity.ToString() : string.Empty;
            }

            // 행 풀링으로 내용만 바뀐 경우 — 마우스가 그대로 올라가 있으면 팝업도 새 아이템으로 갱신한다.
            if (m_isHovered)
            {
                RequestTooltip();
            }
        }

        private void ApplyTierBorder(ItemSlotVD vd)
        {
            if (m_tierBorder == null || m_style == null)
            {
                return;
            }
            m_tierBorder.color = m_style.TierColor(vd != null ? vd.Tier : 1);
        }

        private void ApplyRarityGlow(ItemSlotVD vd)
        {
            if (m_rarityGlow == null)
            {
                return;
            }

            m_rarityGlow.enabled = false;
            if (vd == null || vd.HasRarity == false || m_style == null)
            {
                return;   // 레어도가 없는 종류(재료·장비·소모품)는 글로우를 꺼 둔다
            }

            ItemGradeStyle.GlowEntry glow = m_style.RarityGlow(vd.Rarity);
            if (glow.Strength <= 0f)
            {
                return;   // Normal 등 강도 0
            }

            Color color = glow.Color;
            color.a = glow.Strength;
            m_rarityGlow.color = color;
            m_rarityGlow.enabled = true;
        }

        private void ApplyStar(ItemSlotVD vd)
        {
            bool showStar = vd != null && vd.Kind == TooltipItemKind.Fish && vd.Star > 0;

            if (m_starRoot != null) m_starRoot.SetActive(showStar);
            if (m_starText != null && showStar) m_starText.text = vd.Star.ToString();
        }

        // ── hover ──

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_isHovered = true;
            RequestTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_isHovered = false;
            ItemTooltipController.Dismiss(this);
        }

        /// <summary>비활성화 시 hover 상태를 해제하고 팝업을 닫는다.</summary>
        private void OnDisable()
        {
            m_isHovered = false;
            ItemTooltipController.Dismiss(this);
        }

        private void RequestTooltip()
        {
            // 배선 누락은 조용히 무시하면 원인을 찾기 어렵다 — hover 시점에만 발생하므로 로그가 넘치지 않는다.
            if (m_vd == null)
            {
                Debug.LogWarning($"[ItemSlotView] 슬롯 데이터가 없습니다 — Set()이 호출되지 않았거나 " +
                                 $"ItemSlotVD 생성이 실패했습니다(행 프리팹의 슬롯 참조 확인).", this);
                return;
            }
            if (m_tooltipSource == null)
            {
                Debug.LogWarning("[ItemSlotView] 툴팁 공급자가 없습니다 — 창이 Set()에 자기 자신을 넘겼는지 확인.", this);
                return;
            }

            // 상세는 마우스가 올라온 지금 1건만 조립한다(목록 갱신 때 전 행을 미리 만들지 않는다).
            ItemTooltipData data = m_tooltipSource.BuildTooltip(m_vd);
            if (data == null)
            {
                Debug.LogWarning($"[ItemSlotView] 툴팁 데이터 조립 실패 — kind={m_vd.Kind}, dataId={m_vd.DataId}", this);
                return;
            }

            ItemTooltipController.Request(this, data, m_iconSprite, transform as RectTransform);
        }
    }
}
