using DesktopCompanion.Entities;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 아이템 핸들과 처리 수량을 담는 공용 구조체
    /// </summary>
    public readonly struct ItemQuantity
    {
        public EntityHandle Handle { get; }
        public int Amount { get; }

        public ItemQuantity(EntityHandle handle, int amount)
        {
            Handle = handle;
            Amount = amount;
        }
    }
}