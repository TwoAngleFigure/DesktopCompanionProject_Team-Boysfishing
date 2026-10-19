using DesktopCompanion.Data;
using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    [Serializable]
    public class PlayerSave_New
    {
        // null은 해당 항목이 없는 이전 저장을 뜻한다. 최종 계산 결과는 저장하지 않는다.
        public int? dataId;
        public float? str;
        public float? tec;
        public float? luk;

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
            public int quantity; // 장착 소모품 수량
        }
    }
}
