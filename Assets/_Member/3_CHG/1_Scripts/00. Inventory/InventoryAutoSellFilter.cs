using DesktopCompanion.Data;
using DesktopCompanion.Entities;

public enum SetFilterResult
{
    Invalid,
    UnChanged,
    Changed
}

internal sealed class InventoryAutoSellFilter 
{
    private bool m_autoSellEnabled = false;
    private ItemQuality m_maxAutoSellQuality = ItemQuality.OneStar;
    private ItemRarity m_maxAutoSellRarity = ItemRarity.Normal;

    public bool AutoSellEnabled => m_autoSellEnabled;
    public ItemQuality MaxAutoSellQuality => m_maxAutoSellQuality;
    public ItemRarity MaxAutoSellRarity => m_maxAutoSellRarity;

    public SetFilterResult Set(bool enabled, ItemQuality maxQuality, ItemRarity maxRarity)
    {
        if ((int)maxQuality < (int)ItemQuality.OneStar || (int)maxQuality > (int)ItemQuality.FiveStar
            || (int)maxRarity < (int)ItemRarity.Normal || (int)maxRarity > (int)ItemRarity.Legendary)
        {
            return SetFilterResult.Invalid;
        }

        if (m_autoSellEnabled == enabled && m_maxAutoSellQuality == maxQuality && m_maxAutoSellRarity == maxRarity)
            return SetFilterResult.UnChanged;

        ApplyAutoSellFilter(enabled, maxQuality, maxRarity);
        return SetFilterResult.Changed;
    }

    private void ApplyAutoSellFilter(bool enabled, ItemQuality maxQuality, ItemRarity maxRarity)
    {
        if (m_autoSellEnabled == enabled && m_maxAutoSellQuality == maxQuality && m_maxAutoSellRarity == maxRarity)
        {
            return;
        }

        m_autoSellEnabled = enabled;
        m_maxAutoSellQuality = maxQuality;
        m_maxAutoSellRarity = maxRarity;
    }

    public bool IsAutoSellTarget(Entity_Fish fish)
    {
        if (!m_autoSellEnabled)
        {
            return false;
        }

        return fish.Quality <= m_maxAutoSellQuality
            && fish.Rarity <= m_maxAutoSellRarity;
    }
}
