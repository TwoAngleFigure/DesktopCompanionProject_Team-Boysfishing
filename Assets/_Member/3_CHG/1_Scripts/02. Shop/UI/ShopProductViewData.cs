using DesktopCompanion.Data;
using UnityEngine;

public class ShopProductViewData
{
    public int DataId;
    public string Name;

    public ItemType ItemType;
    public string IconKey;

    public int Tier;
    public int Price;

    public bool IsStackable;

    public ShopProductViewData(int dataId, string name, ItemType itemType, string iconKey, int tier, int price, bool isStackable = true)
    {
        DataId = dataId;
        Name = name;
        ItemType = itemType;
        IconKey = iconKey;
        Tier = tier;
        Price = price;
        IsStackable = isStackable;
    }
}
