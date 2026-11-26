using DesktopCompanion.Data;
using UnityEngine;

public class ShopProducts_New : GameData
{
    [SerializeField] private int m_baseId;
    [SerializeField] private ItemType m_itemType;
    [SerializeField] private bool m_isSummon;
    [SerializeField] private int m_tier;
    [SerializeField] private int m_price;

    [SerializeField] private int m_equipmentProfileId;
    public int EquipmentProfileId => m_equipmentProfileId;

    public int BaseId => m_baseId;
    public ItemType ItemType => m_itemType;
    public bool IsSummon => m_isSummon;
    public int Tier => m_tier;
    public int Price => m_price;
}