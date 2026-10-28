using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 개체(Entity) 또는 정의(ItemData)로부터 <see cref="ItemTooltipData"/>를 조립한다.
    /// AquariumSystem·MixtureSystem은 선택 인자다. 넘기면 아쿠아리움 생산 지표와
    /// 조합 레시피 기준의 제작 비용이 채워지고, 넘기지 않으면 해당 구역을 패널이 감추거나
    /// 아이템 정의에 적힌 제작 비용으로 대체한다.
    /// </summary>
    public static class ItemTooltipBuilder
    {
        /// <summary>개체로부터 조립한다. 물고기의 성급·크기·레어도는 개체 롤값을 쓴다.</summary>
        public static ItemTooltipData FromEntity(EntityHandle handle, EntityManager entities,
            AquariumSystem aquarium = null, MixtureSystem mixture = null)
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
                case Entity_Consumables consumables: return BuildConsumable(consumables, null, mixture);
                default: return null;
            }
        }

        /// <summary>
        /// 정의로부터 조립한다. 개체 롤값이 없으므로 물고기라도 성급·크기·생산 지표는 채워지지 않는다.
        /// </summary>
        public static ItemTooltipData FromData(ItemData data, MixtureSystem mixture = null)
        {
            switch (data)
            {
                case ItemData_Fish fish: return BuildFish(null, default, null, fish);
                case ItemData_Materials materials: return BuildMaterial(materials);
                case ItemData_Equipment equipment: return BuildEquipment(null, equipment);
                case ItemData_Consumables consumables: return BuildConsumable(null, consumables, mixture);
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

        private static ItemTooltipData BuildConsumable(Entity_Consumables consumables,
            ItemData_Consumables fallbackData = null, MixtureSystem mixture = null)
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
            };

            FillCraftCost(section, data, mixture);

            ItemTooltipData tooltip = Head(data, TooltipItemKind.Consumables);
            tooltip.Consumable = section;
            return tooltip;
        }

        /// <summary>
        /// 제작 비용을 채운다. 조합 레시피를 찾으면 그쪽이 기준이고(비용의 진짜 출처),
        /// MixtureSystem을 못 받았거나 레시피가 없으면 아이템 정의에 적힌 값으로 대체한다.
        /// </summary>
        private static void FillCraftCost(ItemTooltipData.ConsumableSection section, ItemData_Consumables data,
            MixtureSystem mixture)
        {
            RecipeData_Mixture recipe = mixture != null
                ? mixture.GetRecipeByResult(ItemType.Consumables, data.ID)
                : null;

            if (recipe != null)
            {
                var parsed = mixture.ParseIngredients(recipe.m_ingredients);
                var costs = new ItemTooltipData.CraftCost[parsed.Count];

                for (int i = 0; i < parsed.Count; i++)
                {
                    costs[i] = new ItemTooltipData.CraftCost
                    {
                        Item = mixture.GetItemData(parsed[i].itemType, parsed[i].dataId),
                        Count = parsed[i].amount,
                    };
                }

                section.CraftMaterials = costs;
                section.CraftGoldCost = recipe.m_goldCost;
                return;
            }

            MaterialCost[] defined = data.CraftMaterials;
            if (defined != null)
            {
                var costs = new ItemTooltipData.CraftCost[defined.Length];
                for (int i = 0; i < defined.Length; i++)
                {
                    costs[i] = new ItemTooltipData.CraftCost
                    {
                        Item = defined[i] != null ? defined[i].Material : null,
                        Count = defined[i] != null ? defined[i].Count : 0,
                    };
                }
                section.CraftMaterials = costs;
            }
            section.CraftGoldCost = data.CraftGoldCost;
        }

        /// <summary>공통 헤더(이름·아이콘·티어)만 채운 툴팁을 만든다. 섹션은 호출자가 붙인다.</summary>
        private static ItemTooltipData Head(ItemData data, TooltipItemKind kind) => new ItemTooltipData
        {
            Kind = kind,
            DataId = data.ID,
            Name = data.Name,
            IconKey = AssetKeys.Of(data, AssetUsage.Icon),
            Tier = data.Tier,
            Description = data.Description,
        };
    }
}
