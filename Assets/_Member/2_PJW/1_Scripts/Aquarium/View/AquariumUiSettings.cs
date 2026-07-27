using System;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 UI의 사용자 설정(정렬 기준/방향)을 PlayerPrefs에 저장·로드한다.
    /// 머신별 사용자 설정이라 게임 세이브와 무관하다(<see cref="DisplaySettings"/>와 동형).
    /// listId로 목록별 설정을 분리한다: "fish"(인벤토리·수족관 물고기 공용) / "material".
    /// ※ 물고기 두 목록은 같은 항목을 비교하므로 정렬 설정을 하나로 공유한다.
    /// </summary>
    public static class AquariumUiSettings
    {
        public const string ListFish = "fish";
        public const string ListMaterial = "material";

        private const string KeyPrefix = "aquarium.sort.";

        public static (AquariumFishSortKey key, SortDirection direction) LoadFishSort(string listId)
            => (ReadEnum(SortKeyPref(listId), AquariumFishSortKey.Name),
                ReadEnum(DirectionPref(listId), SortDirection.Ascending));

        public static void SaveFishSort(string listId, AquariumFishSortKey key, SortDirection direction)
            => Save(listId, (int)key, (int)direction);

        public static (AquariumMaterialSortKey key, SortDirection direction) LoadMaterialSort(string listId)
            => (ReadEnum(SortKeyPref(listId), AquariumMaterialSortKey.Name),
                ReadEnum(DirectionPref(listId), SortDirection.Ascending));

        public static void SaveMaterialSort(string listId, AquariumMaterialSortKey key, SortDirection direction)
            => Save(listId, (int)key, (int)direction);

        private static void Save(string listId, int key, int direction)
        {
            PlayerPrefs.SetInt(SortKeyPref(listId), key);
            PlayerPrefs.SetInt(DirectionPref(listId), direction);
            PlayerPrefs.Save();
        }

        private static string SortKeyPref(string listId) => $"{KeyPrefix}{listId}.key";
        private static string DirectionPref(string listId) => $"{KeyPrefix}{listId}.dir";

        /// <summary>저장된 정수를 열거형으로 읽되 정의에 없는 값이면 기본값으로 폴백한다(항목 추가/삭제 대비).</summary>
        private static T ReadEnum<T>(string prefKey, T fallback) where T : struct, Enum
        {
            int raw = PlayerPrefs.GetInt(prefKey, (int)(object)fallback);
            foreach (T defined in Enum.GetValues(typeof(T)))
            {
                if ((int)(object)defined == raw)
                {
                    return defined;
                }
            }
            return fallback;
        }
    }
}
