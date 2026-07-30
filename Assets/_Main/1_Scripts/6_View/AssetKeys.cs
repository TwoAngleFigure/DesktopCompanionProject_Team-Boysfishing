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
        /// <summary>기본(대체) 에셋 키 접두. 파생 키가 없을 때 AssetProvider가 대신 조회한다.</summary>
        public const string DefaultPrefix = "Default";

        /// <summary>용도별 기본 에셋 키. 예: DefaultOf(AssetUsage.Icon) → "Default_Icon".</summary>
        public static string DefaultOf(string usage)
            => $"{DefaultPrefix}_{usage}";

        /// <summary>키에서 용도 접미를 추출한다. 예: "ItemData_Fish_100001_Icon" → "Icon".</summary>
        public static string UsageOf(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            int separator = key.LastIndexOf('_');
            return separator >= 0 && separator < key.Length - 1
                ? key.Substring(separator + 1)
                : null;
        }

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
