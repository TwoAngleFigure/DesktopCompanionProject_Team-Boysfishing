using DesktopCompanion.Data;
using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    [Serializable]
    public class InventorySave
    {
        public List<SlotSave> slots = new List<SlotSave>();

        public bool autoSellEnabled;
        public ItemQuality maxAutoSellQuality = ItemQuality.OneStar;
        public ItemRarity maxAutoSellRarity = ItemRarity.Normal;

        [Serializable]
        public class SlotSave
        {
            public ItemType itemType;
            public int slotIndex;

            public string handle;
            public int dataId;

            public float size;
            public ItemQuality quality;
            public int upgradeLevel;
            public int quantity;
        }
    }
}