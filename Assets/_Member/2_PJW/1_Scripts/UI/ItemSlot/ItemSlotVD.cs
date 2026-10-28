using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Views
{
    /// <summary>아이템 종류. 슬롯의 표시 규칙과 툴팁 패널 선택에 쓰인다.</summary>
    public enum TooltipItemKind
    {
        Fish,
        Materials,
        Equipment,
        Consumables,
        Unknown,
    }

    /// <summary>
    /// 슬롯 1칸의 표시 데이터. ItemData 계열 전 종류에 공용으로 쓴다.
    /// <see cref="Tier"/>는 전 종류의 테두리 색, <see cref="Rarity"/>는 물고기 전용 글로우에 쓰이며
    /// 레어도 보유 여부는 <see cref="HasRarity"/>로 판별한다.
    /// </summary>
    public class ItemSlotVD
    {
        public TooltipItemKind Kind = TooltipItemKind.Unknown;
        public EntityHandle Handle;      // 개체가 없으면 default(정의만 있는 재료 등)
        public int DataId;
        public string IconKey;

        public int Tier = 1;             // 전 종류 — 테두리 색
        public bool HasRarity;           // 물고기만 true → 글로우 On
        public ItemRarity Rarity;        // HasRarity일 때만 유효
        public int Star;                 // 물고기 개체 성급(1~5). 0 = 성급 표시 없음
        public int Quantity;             // 스택. 1 이하면 미표시

        /// <summary>개체(Entity)에서 만들어졌는지. false면 정의(ItemData)만으로 만든 표시다.</summary>
        public bool HasEntity => Handle.Value != System.Guid.Empty;

        /// <summary>개체(Entity)로부터 만든다. 물고기의 성급·레어도는 개체 롤값을 쓴다.</summary>
        public static ItemSlotVD FromEntity(EntityHandle handle, EntityManager entities)
        {
            Entity entity = entities != null ? entities.Get(handle) : null;
            if (entity == null)
            {
                return null;
            }

            switch (entity)
            {
                case Entity_Fish fish:
                {
                    var vd = FromData(fish.ItemData);
                    if (vd == null) return null;
                    vd.Handle = handle;
                    vd.HasRarity = true;
                    vd.Rarity = fish.Rarity;
                    vd.Star = (int)fish.Quality;
                    return vd;
                }
                case Entity_Materials materials:
                {
                    var vd = FromData(materials.ItemData, materials.Quantity);
                    if (vd != null) vd.Handle = handle;
                    return vd;
                }
                case Entity_Equipment equipment:
                {
                    var vd = FromData(equipment.ItemData);
                    if (vd != null) vd.Handle = handle;
                    return vd;
                }
                case Entity_Consumables consumables:
                {
                    var vd = FromData(consumables.ItemData, consumables.Quantity);
                    if (vd != null) vd.Handle = handle;
                    return vd;
                }
                default:
                    return null;
            }
        }

        /// <summary>
        /// 정의(ItemData)만으로 만든다. 개체가 없는 표시에 쓴다.
        /// 개체 롤값이 없으므로 물고기라도 성급·레어도는 채워지지 않는다.
        /// </summary>
        public static ItemSlotVD FromData(ItemData data, int quantity = 0)
        {
            if (data == null)
            {
                return null;
            }

            return new ItemSlotVD
            {
                Kind = KindOf(data),
                DataId = data.ID,
                IconKey = AssetKeys.Of(data, AssetUsage.Icon),
                Tier = data.Tier,
                Quantity = quantity,
            };
        }

        private static TooltipItemKind KindOf(ItemData data) => data switch
        {
            ItemData_Fish => TooltipItemKind.Fish,
            ItemData_Materials => TooltipItemKind.Materials,
            ItemData_Equipment => TooltipItemKind.Equipment,
            ItemData_Consumables => TooltipItemKind.Consumables,
            _ => TooltipItemKind.Unknown,
        };
    }
}
