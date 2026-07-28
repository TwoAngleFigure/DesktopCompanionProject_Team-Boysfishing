using DesktopCompanion.Data;
using System;
using System.Collections.Generic;

namespace DesktopCompanion.Save
{
    [Serializable]
    public class FishCollectionSave
    {
        public List<Entry> entries = new();

        [Serializable]
        public class Entry
        {
            public int fishDataId;
            public ItemQuality bestQuality;
            public float bestSize;
        }
    }

}
