using System.Text;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 물고기 1개체의 행 표시 데이터. 인벤토리·수족관 목록에 공용으로 쓴다.
    /// 성급·크기는 개체 롤값이고 티어·레어리티는 종 고정값이며, 액션 가능 여부와 차단 사유를 함께 담는다.
    /// </summary>
    public class AquariumFishVD
    {
        public EntityHandle Handle;      // 배치/회수 지목 키(정렬과 무관하게 개체를 특정)
        public int DataId;
        public string Name;
        public string IconKey;

        public int Tier;
        public ItemRarity Rarity;
        public int Star;                 // 개체 성급(1~5)
        public float Size;               // 개체 크기

        public int MaterialId;
        public string MaterialName;
        public string MaterialIconKey;
        public float PointsPerMinute;    // 분당 생산 포인트

        public float RemainingSeconds;   // 다음 생산까지(배치 개체만 유효)
        public int Capacity;             // 이 개체가 차지하는 수용량

        public bool CanAct = true;       // 액션 버튼(넣기/빼기) 활성 여부
        public string BlockReason = string.Empty;   // 비활성 사유(표시용)

        public static AquariumFishVD From(AquariumSystem.AquariumFishInfo info, AquariumSystem aquarium)
        {
            ItemData_Fish fishData = aquarium != null ? aquarium.GetFishData(info.DataId) : null;
            ItemData_Materials matData = aquarium != null ? aquarium.GetMaterialData(info.MaterialId) : null;

            return new AquariumFishVD
            {
                Handle = info.Handle,
                DataId = info.DataId,
                Name = info.Name,
                IconKey = fishData != null ? AssetKeys.Of(fishData, AssetUsage.Icon) : null,
                Tier = info.Tier,
                Rarity = info.Rarity,
                Star = (int)info.Star,
                Size = info.Size,
                MaterialId = info.MaterialId,
                MaterialName = info.MaterialName,
                MaterialIconKey = matData != null ? AssetKeys.Of(matData, AssetUsage.Icon) : null,
                PointsPerMinute = info.PointsPerMinute,
                RemainingSeconds = info.RemainingSeconds,
                Capacity = info.Capacity,
            };
        }
    }

    /// <summary>재료 1종의 생산 경과 행 표시 데이터(누적/요구 포인트, 보류 개수, 시간당 생산 개수).</summary>
    public class AquariumMaterialVD
    {
        public int MaterialId;
        public string MaterialName;
        public string IconKey;
        public int Points;
        public int Required;
        public int Pending;
        public float PerHour;   // 시간당 생산 개수

        /// <summary>게이지 채움 비율(0~1).</summary>
        public float Progress => Required > 0 ? Mathf.Clamp01((float)Points / Required) : 0f;

        public static AquariumMaterialVD From(AquariumSystem.MaterialStatus status, AquariumSystem aquarium)
        {
            ItemData_Materials data = aquarium != null ? aquarium.GetMaterialData(status.MaterialId) : null;
            return new AquariumMaterialVD
            {
                MaterialId = status.MaterialId,
                MaterialName = status.MaterialName,
                IconKey = data != null ? AssetKeys.Of(data, AssetUsage.Icon) : null,
                Points = status.Points,
                Required = status.Required,
                Pending = status.Pending,
                PerHour = status.PerHour,
            };
        }
    }

    /// <summary>
    /// 수족관 정보 패널의 표시 데이터. 현재 등급과 다음 등급의 수용 한도, 강화 비용·보유량·차단 사유를 담는다.
    /// 등급별 수용 한도만 다루며, '사용/최대' 수용량은 <see cref="AquariumFishListViewModel.CapacityText"/>가 담당한다.
    /// </summary>
    public class AquariumInfoVD
    {
        public int Level;
        public string LevelName = string.Empty;   // 현재 등급 이름(AquariumUpgradeData.Name)
        public int MaxCapacity;                   // 현재 등급의 수용 한도

        public bool HasNext;
        public bool CanUpgrade;
        public int NextLevel;
        public string NextLevelName = string.Empty;
        public int NextMaxCapacity;
        public int GoldCost;
        public int GoldOwned;
        public string MaterialSummary = string.Empty;   // "새우살 20/20, 조개 5/3"
        public string BlockReason = string.Empty;

        public static AquariumInfoVD From(AquariumSystem aquarium)
        {
            if (aquarium == null)
            {
                return new AquariumInfoVD();
            }

            AquariumSystem.AquariumUpgradeInfo upgrade = aquarium.GetUpgradeInfo();
            return new AquariumInfoVD
            {
                Level = aquarium.UpgradeLevel,
                LevelName = GradeName(aquarium.CurrentUpgrade != null ? aquarium.CurrentUpgrade.Name : null, aquarium.UpgradeLevel),
                MaxCapacity = aquarium.MaxCapacity,
                HasNext = upgrade.HasNext,
                CanUpgrade = upgrade.CanUpgrade,
                NextLevel = upgrade.NextLevel,
                NextLevelName = GradeName(upgrade.NextName, upgrade.NextLevel),
                NextMaxCapacity = upgrade.NextMaxCapacity,
                GoldCost = upgrade.GoldCost,
                GoldOwned = upgrade.GoldOwned,
                MaterialSummary = SummarizeMaterials(upgrade),
                BlockReason = upgrade.BlockReason,
            };
        }

        /// <summary>등급 이름을 반환한다. 이름이 비어 있으면 `Lv.N`으로 폴백한다.</summary>
        private static string GradeName(string name, int level)
            => string.IsNullOrWhiteSpace(name) ? $"Lv.{level}" : name;

        private static string SummarizeMaterials(AquariumSystem.AquariumUpgradeInfo upgrade)
        {
            if (upgrade.Materials == null || upgrade.Materials.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < upgrade.Materials.Count; i++)
            {
                AquariumSystem.MaterialRequirement req = upgrade.Materials[i];
                if (i > 0) builder.Append(", ");
                builder.Append($"{req.MaterialName} {req.Owned}/{req.Required}");
            }
            return builder.ToString();
        }
    }
}
