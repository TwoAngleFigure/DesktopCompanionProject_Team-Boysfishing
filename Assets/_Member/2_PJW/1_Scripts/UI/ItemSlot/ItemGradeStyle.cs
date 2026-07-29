using UnityEngine;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아이템 등급 표현 색을 공급하는 설정 에셋.
    /// - 티어 테두리 색: 시작 색상각에서 <see cref="m_stepDegrees"/>씩 색상환을 돌려 계산한다.
    ///   톤(<see cref="m_saturation"/>·<see cref="m_baseValue"/>)은 전 티어에 공통 적용되며,
    ///   색상환을 한 바퀴 돌 때마다 밝기 상한에 <see cref="m_cycleValueFalloff"/>가 곱해진다.
    /// - 레어도 글로우: 물고기 전용. 레어도 5단계의 색·강도를 인스펙터로 지정한다.
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

        [SerializeField, Range(0f, 1f)] private float m_saturation = 1f;
        [SerializeField, Range(0f, 1f)] private float m_baseValue = 1f;

        [Tooltip("색상환을 한 바퀴 돌 때마다 곱할 명도 배수. 스텝이 커서 색이 되돌아올 때 구분용(안전망)")]
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

        /// <summary>티어 테두리 색을 반환한다. 시작 색상각에서 (tier-1) × Step 만큼 돌린 값이다.</summary>
        public Color TierColor(int tier)
        {
            int steps = Mathf.Max(1, tier) - 1;                        // 티어는 1부터
            float rotated = steps * m_stepDegrees;                     // 시작점에서 돌린 총 각도
            float hue = Mathf.Repeat(m_startHue + rotated, 360f) / 360f;

            int cycle = Mathf.FloorToInt(rotated / 360f);              // 몇 바퀴째인지(보통 0)
            float value = m_baseValue * Mathf.Pow(m_cycleValueFalloff, cycle);

            return Color.HSVToRGB(hue, m_saturation, value);
        }

        /// <summary>레어도 글로우의 색·강도를 반환한다. 정의가 없으면 강도 0을 반환한다.</summary>
        public GlowEntry RarityGlow(ItemRarity rarity)
        {
            int index = (int)rarity;
            return m_rarityGlows != null && index >= 0 && index < m_rarityGlows.Length && m_rarityGlows[index] != null
                ? m_rarityGlows[index]
                : s_noGlow;
        }

        /// <summary>
        /// 레어도 식별 색을 반환한다. 글로우 강도와 무관하므로 강도 0인 레어도도 자기 색을 반환한다.
        /// </summary>
        public Color RarityColor(ItemRarity rarity) => RarityGlow(rarity).Color;
    }
}
