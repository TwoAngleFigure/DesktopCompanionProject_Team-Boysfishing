using DesktopCompanion.Data;

public class ShopProductViewData
{
    public int ProductId;
    public int BaseId;
    public string Name;

    public ItemType ItemType;
    public string IconKey;

    public int Tier;
    public int Price;

    public string TypeText;
    public string Description;

    public bool IsBuyable;
    public bool IsSummon;
    public bool IsStackable;

    public ShopProductViewData(int productId, int baseId, string name, ItemType itemType, string iconKey, int tier, 
        int price, string typeText, string description, bool isBuyable, bool isSummon, bool isStackable = true)
    {
        ProductId = productId;
        BaseId = baseId;
        Name = name;
        ItemType = itemType;
        IconKey = iconKey;
        Tier = tier;
        Price = price;
        TypeText = typeText;
        Description = description;
        IsBuyable = isBuyable;
        IsSummon = isSummon;
        IsStackable = isStackable;
    }
}
