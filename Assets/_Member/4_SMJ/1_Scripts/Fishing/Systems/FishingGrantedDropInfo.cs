using DesktopCompanion.Data;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 인벤토리에 지급된 낚시 추가 보상 결과.
    /// </summary>
    public readonly struct FishingGrantedDropInfo
    {
        public ItemType ItemType { get; }
        public int DataId { get; }
        public int Count { get; }

        public FishingGrantedDropInfo(
            ItemType itemType,
            int dataId,
            int count)
        {
            ItemType = itemType;
            DataId = dataId;
            Count = count;
        }

        public bool IsValid => DataId > 0 && Count > 0;
    }
}