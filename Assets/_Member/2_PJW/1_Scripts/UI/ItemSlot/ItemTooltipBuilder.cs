using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 개체/정의 → <see cref="ItemTooltipData"/> 조립(계획 28 B절).
    /// <paramref name="aquarium"/>은 <b>선택</b>이다 — 넘기면 아쿠아리움 지표(생산 주기·분당 포인트·
    /// 재료 진행)가 채워지고, 없으면 해당 섹션의 HasAquariumInfo가 false가 되어 패널이 그 구역을 감춘다.
    /// 덕분에 같은 슬롯·팝업을 아쿠아리움 밖(상점·제작 등)에서도 그대로 쓸 수 있다.
    /// </summary>
    public static class ItemTooltipBuilder
    {
        /// <summary>개체 기반. 물고기의 성급·크기·레어도는 개체 롤값을 쓴다.</summary>
        public static ItemTooltipData FromEntity(EntityHandle handle, EntityManager entities, AquariumSystem aquarium = null)
        {
            Entity entity = entities != null ? entities.Get(handle) : null;
            if (entity == null)
            {
                return null;
            }

            switch (entity)
            {
                case Entity_Fish fish: return BuildFish(fish, handle, aquarium);
                case Entity_Materials materials: return BuildMaterial(materials.ItemData);
                case Entity_Equipment equipment: return BuildEquipment(equipment);
                case Entity_Consumables consumables: return BuildConsumable(consumables);
                default: return null;
            }
        }

        /// <summary>
        /// 정의 기반. 개체가 없는 표시(아쿠아리움 재료 생산 풀, 상점 진열 등)용.
        /// 개체 롤값이 없으므로 물고기라도 성급·크기·생산 지표는 채워지지 않는다.
        /// </summary>
        public static ItemTooltipData FromData(ItemData data)
        {
            switch (data)
            {
                case ItemData_Fish fish: return BuildFish(null, default, null, fish);
                case ItemData_Materials materials: return BuildMaterial(materials);
                case ItemData_Equipment equipment: return BuildEquipment(null, equipment);
                case ItemData_Consumables consumables: return BuildConsumable(null, consumables);
                default: return null;
            }
        }

        // ── 종류별 조립 ──

        private static ItemTooltipData BuildFish(Entity_Fish fish, EntityHandle handle,
            AquariumSystem aquarium, ItemData_Fish fallbackData = null)
        {
            ItemData_Fish data = fish != null ? fish.ItemData : fallbackData;
            if (data == null)
            {
                return null;
            }

            var section = new ItemTooltipData.FishSection
            {
                Capacity = data.AquariumCapacity,
                MaterialName = data.AquariumMaterial != null ? data.AquariumMaterial.Name : "-",
                MaterialIconKey = data.AquariumMaterial != null
                    ? AssetKeys.Of(data.AquariumMaterial, AssetUsage.Icon)
                    : null,
            };

            if (fish != null)
            {
                section.Rarity = fish.Rarity;
                section.Star = (int)fish.Quality;
                section.Size = fish.Size;

                // 생산 주기·분당 포인트는 개체 성급에 따라 달라지므로 개체가 있을 때만 의미가 있다.
                if (aquarium != null)
                {
                    AquariumSystem.AquariumFishInfo info = aquarium.BuildFishInfo(handle);
                    if (info.IsValid)
                    {
                        section.CycleSeconds = info.CycleSeconds;
                        section.PointsPerMinute = info.PointsPerMinute;
                        section.HasAquariumInfo = true;
                    }
                }
            }

            ItemTooltipData tooltip = Head(data, TooltipItemKind.Fish);
            tooltip.Fish = section;
            return tooltip;
        }

        // 재료는 헤더(아이콘·이름·티어)만 표시한다 — 아쿠아리움 진행 상황은 재료 게이지 행이 이미 보여준다.
        private static ItemTooltipData BuildMaterial(ItemData_Materials data)
            => data != null ? Head(data, TooltipItemKind.Materials) : null;

        private static ItemTooltipData BuildEquipment(Entity_Equipment equipment, ItemData_Equipment fallbackData = null)
        {
            ItemData_Equipment data = equipment != null ? equipment.ItemData : fallbackData;
            if (data == null)
            {
                return null;
            }

            int level = equipment != null ? equipment.UpgradeLevel : 0;
            var section = new ItemTooltipData.EquipmentSection
            {
                MountingArea = data.MountingArea,
                UpgradeLevel = level,
                MaxUpgradeLevel = data.MaxUpgradeLevel,
                Modifiers = data.GetModifiers(level),
                NextStep = data.GetNextUpgradeStep(level),
            };

            ItemTooltipData tooltip = Head(data, TooltipItemKind.Equipment);
            tooltip.Equipment = section;
            return tooltip;
        }

        private static ItemTooltipData BuildConsumable(Entity_Consumables consumables, ItemData_Consumables fallbackData = null)
        {
            ItemData_Consumables data = consumables != null ? consumables.ItemData : fallbackData;
            if (data == null)
            {
                return null;
            }

            var section = new ItemTooltipData.ConsumableSection
            {
                Category = data.MountingArea,
                Modifiers = data.Modifiers,
                Quantity = consumables != null ? consumables.Quantity : 0,
                CraftMaterials = data.CraftMaterials,
                CraftGoldCost = data.CraftGoldCost,
            };

            ItemTooltipData tooltip = Head(data, TooltipItemKind.Consumables);
            tooltip.Consumable = section;
            return tooltip;
        }

        /// <summary>공통 헤더(이름·아이콘·티어)를 채운 빈 툴팁을 만든다. 섹션은 호출자가 붙인다.</summary>
        private static ItemTooltipData Head(ItemData data, TooltipItemKind kind) => new ItemTooltipData
        {
            Kind = kind,
            DataId = data.ID,
            Name = data.Name,
            IconKey = AssetKeys.Of(data, AssetUsage.Icon),
            Tier = data.Tier,
        };
    }
}
