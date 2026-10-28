using System;
using DesktopCompanion.Data;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 기록의 출처. 사건이 일어나는 시스템이 RecordSystem의 해당 기록 함수를 호출한다.
    /// 시트 조건 칸과 세이브에는 이름 문자열로 저장되므로 이름을 바꾸지 않는다.
    /// </summary>
    public enum RecordSource
    {
        None = 0,
        FishCaught,       // 물고기 포획. 대상 = 어종 ID
        FishingFailed,    // 포획 실패. 대상 = 어종 ID
        ItemDropped,      // 낚시 추가 드랍. 대상 = 아이템 ID
        ItemCrafted,      // 제작. 대상 = 결과 아이템 ID
        ItemBought,       // 상점 구매. 대상 = 아이템 ID
        ItemSold,         // 상점 판매. 대상 = 아이템 ID
        GoldEarned,       // 골드 증가량. 대상 없음
        GoldSpent,        // 골드 감소량. 대상 없음
        StageArrived,     // 지역 도착. 대상 = 지역 ID
    }

    /// <summary>집계 키. TargetId가 0이면 출처 전체 키다.</summary>
    public readonly struct RecordKey : IEquatable<RecordKey>
    {
        public readonly RecordSource Source;
        public readonly int TargetId;

        public RecordKey(RecordSource source, int targetId = 0)
        {
            Source = source;
            TargetId = targetId;
        }

        public bool Equals(RecordKey other) => Source == other.Source && TargetId == other.TargetId;

        public override bool Equals(object obj) => obj is RecordKey other && Equals(other);

        public override int GetHashCode() => ((int)Source * 397) ^ TargetId;

        /// <summary>시트 조건 칸과 같은 표기. 예: FishCaught, FishCaught:100001</summary>
        public override string ToString() => TargetId == 0 ? Source.ToString() : $"{Source}:{TargetId}";
    }

    /// <summary>키 하나의 변경 내역. RecordSystem.OnRecordsChanged가 목록으로 전달한다.</summary>
    public readonly struct RecordChange
    {
        public readonly RecordKey Key;
        public readonly long OldValue;
        public readonly long NewValue;

        public RecordChange(RecordKey key, long oldValue, long newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    // ── 사건 정보. ID와 값만 담는다 ──
    // EntityHandle은 담지 않는다. 포획 개체처럼 이벤트 직후 파기될 수 있는 개체는 구독자가 조회할 수 없다.

    /// <summary>물고기 포획 사건.</summary>
    public readonly struct FishCaughtInfo
    {
        public readonly int FishId;
        public readonly ItemQuality Quality;
        public readonly float Size;

        public FishCaughtInfo(int fishId, ItemQuality quality, float size)
        {
            FishId = fishId;
            Quality = quality;
            Size = size;
        }
    }

    /// <summary>아이템 획득 계열 사건(낚시 추가 드랍).</summary>
    public readonly struct ItemRecordInfo
    {
        public readonly ItemType Type;
        public readonly int ItemId;
        public readonly int Count;

        public ItemRecordInfo(ItemType type, int itemId, int count)
        {
            Type = type;
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>제작 사건. 결과 아이템 기준으로 집계한다.</summary>
    public readonly struct ItemCraftInfo
    {
        public readonly int RecipeId;
        public readonly ItemType Type;
        public readonly int ItemId;
        public readonly int Count;

        public ItemCraftInfo(int recipeId, ItemType type, int itemId, int count)
        {
            RecipeId = recipeId;
            Type = type;
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>상점 거래 사건(구매·판매). Gold는 지불하거나 받은 골드다.</summary>
    public readonly struct ItemTradeInfo
    {
        public readonly ItemType Type;
        public readonly int ItemId;
        public readonly int Count;
        public readonly int Gold;

        public ItemTradeInfo(ItemType type, int itemId, int count, int gold)
        {
            Type = type;
            ItemId = itemId;
            Count = count;
            Gold = gold;
        }
    }
}
