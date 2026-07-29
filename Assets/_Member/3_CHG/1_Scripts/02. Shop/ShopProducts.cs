using DesktopCompanion.Data;
using UnityEngine;

public class ShopProducts : GameData
{
    [SerializeField] private int m_baseId;
    [SerializeField] private ItemType m_itemType;
    [SerializeField] private bool m_isSummon;
    [SerializeField] private int m_tier;
    [SerializeField] private int m_price;

    public int BaseId => m_baseId;
    public ItemType ItemType => m_itemType;
    public bool IsSummon => m_isSummon;
    public int Tier => m_tier;
    public int Price => m_price;

    public ShopProducts(int baseId, ItemType itemType, bool isSummon, int tier, int price)
    {
        m_baseId = baseId;
        m_itemType = itemType;
        m_isSummon = isSummon;
        m_tier = tier;
        m_price = price;
    }
}
