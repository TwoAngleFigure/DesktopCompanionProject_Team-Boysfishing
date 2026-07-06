using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>에셋 용도 접미 상수(D18). 필요 시 확장.</summary>
    public static class AssetUsage
    {
        public const string Icon = "Icon";             // UI 아이콘(Sprite)
        public const string Model = "Model";           // 월드 프리팹(GameObject)
        public const string Background = "Background"; // 스테이지 배경
    }

    /// <summary>
    /// Data → Addressables 주소 키 파생(D18·§16.2). View 계층 전용 — Data/System은 사용하지 않는다.
    /// 규약: {클래스명}_{ID}_{용도}. m_assetKey가 있으면 그것을 키 베이스로 사용(공유/스킨 오버라이드).
    /// </summary>
    public static class AssetKeys
    {
        /// <summary>키 베이스(용도 접미 제외). 예: "ItemData_Fish_100001" 또는 오버라이드 값.</summary>
        public static string BaseOf(GameData data)
            => string.IsNullOrEmpty(data.AssetKey)
                ? $"{data.GetType().Name}_{data.ID}"
                : data.AssetKey;

        /// <summary>최종 키. 예: Of(fishData, AssetUsage.Icon) → "ItemData_Fish_100001_Icon".</summary>
        public static string Of(GameData data, string usage)
            => $"{BaseOf(data)}_{usage}";
    }
}
