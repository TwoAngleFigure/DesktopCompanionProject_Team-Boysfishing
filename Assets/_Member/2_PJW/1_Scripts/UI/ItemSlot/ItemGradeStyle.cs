using UnityEngine;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 슬롯의 등급 표현(계획 28 P6·P7).
    /// - 티어 테두리 색: 색상환(Hue)을 <see cref="m_stepDegrees"/>씩 돌려 <b>계산</b>한다. 색을 나열하지 않으므로
    ///   티어가 늘어도 에셋을 손볼 필요가 없다. 시작점은 완전한 초록(Hue 120°), 회전 방향은 Hue 증가.
    /// - 레어도 글로우: 물고기 전용. 5단계를 인스펙터로 지정한다.
    /// ※ 티어당 회전량은 티어 상한이 확정돼야 정할 수 있다. 티어 수 N에 대해 360/N이 기준점이며,
    ///   Step × (N-1)이 360을 넘으면 색이 되돌아온다(그때는 바퀴별 명도 감쇠가 구분을 돕는다).
    /// </summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/UI/Item Grade Style")]
    public class ItemGradeStyle : ScriptableObject
    {
        [Header("티어 테두리 — 색상환 회전")]
        [Tooltip("티어 1의 색상각. 120 = 완전한 초록 (0,255,0)")]
        [SerializeField, Range(0f, 360f)] private float m_startHue = 120f;

        [Tooltip("티어가 1 오를 때 색상환을 돌릴 각도. 티어 수 N에 대해 360/N이 기준점 " +
                 "(작으면 인접 티어가 비슷해지고, 크면 한 바퀴를 일찍 돌아 색이 겹친다)")]
        [SerializeField, Range(1f, 360f)] private float m_stepDegrees = 60f;

        [Header("티어 테두리 — 톤(전 티어 공통)")]
        [Tooltip("선명도. 1 = 원색, 낮출수록 흰빛이 섞여 탁해진다. 예: 밝기 255 + 선명도 0.5 → (255,255,128)")]
        [SerializeField, Range(0f, 1f)] private float m_saturation = 1f;
        [Tooltip("밝기 상한 — 색에서 가장 밝은 채널이 가질 값. 1 = 255(완전한 원색), 0.737 = 188. " +
                 "인스펙터에서는 0~255 채널값으로 표기된다")]
        [SerializeField, Range(0f, 1f)] private float m_baseValue = 1f;

        [Tooltip("색상환을 한 바퀴 돌 때마다 밝기 상한에 곱할 배수. 스텝이 커서 색이 되돌아올 때 구분용(안전망)")]
        [SerializeField, Range(0.1f, 1f)] private float m_cycleValueFalloff = 0.65f;

        [Header("레어도 글로우 — 물고기 전용")]
        [Tooltip("index = ItemRarity (0=Normal … 4=Legendary). 비우면 글로우 없음")]
        [SerializeField] private GlowEntry[] m_rarityGlows;

        [System.Serializable]
        public class GlowEntry
        {
            public Color Color = Color.white;

            [Tooltip("0 = 글로우 없음. 글로우 이미지의 알파로 적용된다")]
            [Range(0f, 1f)] public float Strength;
        }

        private static readonly GlowEntry s_noGlow = new GlowEntry { Strength = 0f };

        /// <summary>티어 테두리 색. 시작 색상각에서 (tier-1) × Step 만큼 돌린 값.</summary>
        public Color TierColor(int tier)
        {
            int steps = Mathf.Max(1, tier) - 1;                        // 티어는 1부터
            float rotated = steps * m_stepDegrees;                     // 시작점에서 돌린 총 각도
            float hue = Mathf.Repeat(m_startHue + rotated, 360f) / 360f;

            int cycle = Mathf.FloorToInt(rotated / 360f);              // 몇 바퀴째인지(보통 0)
            float value = m_baseValue * Mathf.Pow(m_cycleValueFalloff, cycle);

            return Color.HSVToRGB(hue, m_saturation, value);
        }

        /// <summary>레어도 글로우. 정의가 없으면 강도 0(글로우 꺼짐).</summary>
        public GlowEntry RarityGlow(ItemRarity rarity)
        {
            int index = (int)rarity;
            return m_rarityGlows != null && index >= 0 && index < m_rarityGlows.Length && m_rarityGlows[index] != null
                ? m_rarityGlows[index]
                : s_noGlow;
        }

        /// <summary>
        /// 레어도 식별 색(글로우 강도와 무관). 슬롯 글로우와 툴팁 뱃지가 같은 색을 쓰도록 하는 통로 —
        /// 강도 0인 Normal도 뱃지에는 자기 색으로 표시된다.
        /// </summary>
        public Color RarityColor(ItemRarity rarity) => RarityGlow(rarity).Color;
    }
}
