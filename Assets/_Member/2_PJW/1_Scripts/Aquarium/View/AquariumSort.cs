using System.Collections.Generic;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>물고기 목록 정렬 기준(계획 27 B절).</summary>
    public enum AquariumFishSortKey
    {
        Name,
        Tier,
        Rarity,
        Star,
        Size,
        PointsPerMinute,
    }

    /// <summary>재료 생산 경과 목록 정렬 기준.</summary>
    public enum AquariumMaterialSortKey
    {
        Name,
        Progress,
        CurrentPoints,
        RequiredPoints,
        PerHour,
    }

    /// <summary>정렬 방향(유저가 지정).</summary>
    public enum SortDirection
    {
        Ascending,
        Descending,
    }

    /// <summary>
    /// 아쿠아리움 목록 정렬. 정렬은 '표시 정책'이라 System이 아닌 View 계층이 소유한다.
    /// 키 1개 + 방향 토글이며, 동률은 항상 (이름 → dataId → 핸들)로 안정화한다 —
    /// 같은 종 개체가 여럿 섞여도 갱신마다 순서가 뒤바뀌지 않게 하기 위함이다.
    /// </summary>
    public static class AquariumSort
    {
        public static void Apply(List<AquariumFishVD> list, AquariumFishSortKey key, SortDirection direction)
        {
            if (list == null || list.Count < 2)
            {
                return;
            }

            int sign = direction == SortDirection.Ascending ? 1 : -1;
            list.Sort((a, b) =>
            {
                int compared = key switch
                {
                    AquariumFishSortKey.Tier => a.Tier.CompareTo(b.Tier),
                    AquariumFishSortKey.Rarity => ((int)a.Rarity).CompareTo((int)b.Rarity),
                    AquariumFishSortKey.Star => a.Star.CompareTo(b.Star),
                    AquariumFishSortKey.Size => a.Size.CompareTo(b.Size),
                    AquariumFishSortKey.PointsPerMinute => a.PointsPerMinute.CompareTo(b.PointsPerMinute),
                    _ => string.CompareOrdinal(a.Name, b.Name),
                };
                return compared != 0 ? compared * sign : Tiebreak(a, b);
            });
        }

        public static void Apply(List<AquariumMaterialVD> list, AquariumMaterialSortKey key, SortDirection direction)
        {
            if (list == null || list.Count < 2)
            {
                return;
            }

            int sign = direction == SortDirection.Ascending ? 1 : -1;
            list.Sort((a, b) =>
            {
                int compared = key switch
                {
                    AquariumMaterialSortKey.Progress => a.Progress.CompareTo(b.Progress),
                    AquariumMaterialSortKey.CurrentPoints => a.Points.CompareTo(b.Points),
                    AquariumMaterialSortKey.RequiredPoints => a.Required.CompareTo(b.Required),
                    AquariumMaterialSortKey.PerHour => a.PerHour.CompareTo(b.PerHour),
                    _ => string.CompareOrdinal(a.MaterialName, b.MaterialName),
                };
                return compared != 0 ? compared * sign : a.MaterialId.CompareTo(b.MaterialId);
            });
        }

        // 동률 안정화 — 방향(sign)을 곱하지 않는다. 오름/내림을 뒤집어도 동률 그룹 내부 순서는 고정된다.
        private static int Tiebreak(AquariumFishVD a, AquariumFishVD b)
        {
            int compared = string.CompareOrdinal(a.Name, b.Name);
            if (compared != 0) return compared;

            compared = a.DataId.CompareTo(b.DataId);
            return compared != 0 ? compared : a.Handle.Value.CompareTo(b.Handle.Value);
        }

        // ── 정렬 기준 값 표시 ──

        /// <summary>
        /// 현재 정렬 기준의 값만 문자열로(계획 28 P3). 상세를 툴팁으로 옮긴 뒤에도
        /// 정렬 결과를 행에서 눈으로 확인할 수 있게 하는 한 칸이다.
        /// 이름 정렬이면 빈 문자열 — 이름은 이미 행에 보인다.
        /// </summary>
        public static string FormatValue(AquariumFishVD vd, AquariumFishSortKey key)
        {
            if (vd == null)
            {
                return string.Empty;
            }

            return key switch
            {
                AquariumFishSortKey.Tier => $"T{vd.Tier}",
                AquariumFishSortKey.Rarity => RarityLabel(vd.Rarity),
                AquariumFishSortKey.Star => $"{vd.Star}성",
                AquariumFishSortKey.Size => $"{vd.Size:0.##}",
                AquariumFishSortKey.PointsPerMinute => $"{vd.PointsPerMinute:0.#}pt/분",
                _ => string.Empty,
            };
        }

        public static string FormatValue(AquariumMaterialVD vd, AquariumMaterialSortKey key)
        {
            if (vd == null)
            {
                return string.Empty;
            }

            return key switch
            {
                AquariumMaterialSortKey.Progress => $"{vd.Progress * 100f:0}%",
                AquariumMaterialSortKey.CurrentPoints => $"{vd.Points}pt",
                AquariumMaterialSortKey.RequiredPoints => $"{vd.Required}pt",
                AquariumMaterialSortKey.PerHour => $"{vd.PerHour:0.#}개/h",
                _ => string.Empty,
            };
        }

        private static readonly string[] s_rarityLabels = { "일반", "고급", "희귀", "영웅", "전설" };

        private static string RarityLabel(ItemRarity rarity)
        {
            int index = (int)rarity;
            return index >= 0 && index < s_rarityLabels.Length ? s_rarityLabels[index] : rarity.ToString();
        }

        // ── 드롭다운 표시용 라벨 ──

        public static readonly string[] FishSortLabels =
        {
            "이름", "티어", "레어리티", "성급", "크기", "분당 포인트",
        };

        public static readonly string[] MaterialSortLabels =
        {
            "이름", "진행률", "현재 포인트", "필요 포인트", "시간당 개수",
        };
    }
}
