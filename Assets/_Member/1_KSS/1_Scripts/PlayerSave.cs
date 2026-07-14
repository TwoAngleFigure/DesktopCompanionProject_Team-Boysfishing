using DesktopCompanion.Data;
using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    [Serializable]
    public class PlayerSave
    {
        public int currentLicense;
        public int gold;

        public int bonusInventorySize;
        public int currentStorageUpgradeCost;

        public List<EquippedItemSave> equippedItems = new List<EquippedItemSave>();

        [Serializable]
        public class EquippedItemSave
        {
            public EquipmentMountingArea area;
            public string handle;
            public int dataId;
            public int upgradeLevel;
        }
    }
}
