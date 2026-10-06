using System.Text;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 스탯·장착 부위 한글 표기의 단일 출처. 툴팁 팝업과 강화창처럼 같은 값을 보여주는 화면들이
    /// 각자 표를 들고 있으면 같은 스탯이 서로 다른 이름으로 보이므로 여기로 모은다.
    /// </summary>
    public static class ItemLabels
    {
        public static string Stat(PlayerStat stat) => stat switch
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

        public static string MountingArea(EquipmentMountingArea area) => area switch
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

        /// <summary>
        /// 스탯 값 1개를 표기한다. 곱연산은 배율임이 드러나야 하므로 '×'를 붙인다.
        /// </summary>
        public static string Value(ModifierOperation operation, float value)
            => operation == ModifierOperation.Multiply ? $"×{value:0.##}" : $"{value:0.##}";

        /// <summary>
        /// 스탯 변경 목록을 줄바꿈으로 이어 붙인다. 항목이 없으면 "효과 없음"을 반환한다.
        /// 합연산은 부호를 붙여 증감이 드러나게 한다.
        /// </summary>
        public static string Modifiers(StatModifier[] modifiers)
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

                builder.Append(Stat(modifier.Stat));
                builder.Append(' ');
                builder.Append(modifier.Operation == ModifierOperation.Multiply
                    ? $"×{modifier.Value:0.##}"
                    : $"{(modifier.Value >= 0f ? "+" : string.Empty)}{modifier.Value:0.##}");
            }
            return builder.Length > 0 ? builder.ToString() : "효과 없음";
        }
    }
}
