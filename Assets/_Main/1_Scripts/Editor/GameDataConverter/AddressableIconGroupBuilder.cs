using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Views;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// GameData.json의 ID/이름을 기준으로 Assets/Resource 아이콘을 찾아
    /// Addressables 그룹(Icon_Consumable, Icon_Material 등)에 AssetKeys 규약 주소로 등록한다(D18 연계).
    /// 신규 아이콘 폴더/타입이 늘어나면 매핑표와 메뉴 항목을 추가할 것.
    /// </summary>
    public static class AddressableIconGroupBuilder
    {
        private const string SchemaTemplateGroupName = "Icon_Fish";
        private const string PreloadLabel = "preload";

        // ItemData_Consumables#ID → Assets/Resource/Icon_Consumable 내 파일명(확장자 제외).
        // 한글이름영어로.md 변환표 기준. 아이콘 파일이 없는 ID는 매핑에서 제외(로그로 안내).
        private static readonly Dictionary<int, string> s_consumableIconFiles = new()
        {
            { 400002, "golden_bait" },
            { 400003, "iridescent_jade_pearl" },
            { 400004, "ancient_obsidian_lure" },
            { 400005, "venomous_amethyst" },
            { 400006, "golden_coral" },
            { 400007, "sturdy_coastal_ore" },
            { 400008, "frozen_glacier_crystal" },
            { 400009, "abyssal_blue_pearl" },
            { 400010, "silver_spearhead_lure" },
            { 400011, "sea_giants_conch" },
        };

        // ItemData_Materials#ID → Assets/Resource/Icon_Material 내 파일명(확장자 제외).
        // 300016~300024는 1~9티어 장비 조각으로, 티어별 전용 아이콘을 각각 쓴다.
        private static readonly Dictionary<int, string> s_materialIconFiles = new()
        {
            { 300001, "lustrous_scale" },
            { 300002, "hard_seashell" },
            { 300003, "rare_coral" },
            { 300004, "abyssal_quartz" },
            { 300005, "legendary_volcanic_stone" },
            { 300006, "golden_bait_fragment" },
            { 300007, "iridescent_jade_pearl_fragment" },
            { 300008, "ancient_obsidian_lure_fragment" },
            { 300009, "venomous_amethyst_fragment" },
            { 300010, "golden_coral_fragment" },
            { 300011, "sturdy_coastal_ore_fragment" },
            { 300012, "frozen_glacier_crystal_fragment" },
            { 300013, "abyssal_blue_pearl_fragment" },
            { 300014, "silver_spearhead_lure_fragment" },
            { 300015, "sea_giants_conch_fragment" },
            { 300016, "tier_1_equipment_fragment" },
            { 300017, "tier_2_equipment_fragment" },
            { 300018, "tier_3_equipment_fragment" },
            { 300019, "tier_4_equipment_fragment" },
            { 300020, "tier_5_equipment_fragment" },
            { 300021, "tier_6_equipment_fragment" },
            { 300022, "tier_7_equipment_fragment" },
            { 300023, "tier_8_equipment_fragment" },
            { 300024, "tier_9_equipment_fragment" },
        };

        [MenuItem("Tools/DesktopCompanion/Addressables - Consumable 아이콘 그룹 등록")]
        public static void BuildConsumableIconGroup()
        {
            var dataManager = new DataManager();
            dataManager.Load();
            BuildIconGroup(
                "Icon_Consumable",
                "Assets/Resource/Icon_Consumable",
                s_consumableIconFiles,
                dataManager.GetAll<ItemData_Consumables>());
        }

        [MenuItem("Tools/DesktopCompanion/Addressables - Material 아이콘 그룹 등록")]
        public static void BuildMaterialIconGroup()
        {
            var dataManager = new DataManager();
            dataManager.Load();
            BuildIconGroup(
                "Icon_Material",
                "Assets/Resource/Icon_Material",
                s_materialIconFiles,
                dataManager.GetAll<ItemData_Materials>());
        }

        [MenuItem("Tools/DesktopCompanion/Addressables - Fish 아이콘 그룹 등록")]
        public static void BuildFishIconGroup()
        {
            var dataManager = new DataManager();
            dataManager.Load();

            // Assets/Resource/Icon_Fish/ItemData_Fish_{ID}_Icon.png — 파일명이 곧 주소라 매핑표 불필요.
            // AssetKeys로 만들어 두면 m_assetKey 오버라이드 시 주소와 기대 파일명이 함께 움직인다.
            var iconFiles = new Dictionary<int, string>();
            foreach (ItemData_Fish data in dataManager.GetAll<ItemData_Fish>())
            {
                iconFiles[data.ID] = AssetKeys.Of(data, AssetUsage.Icon);
            }

            BuildIconGroup(
                "Icon_Fish",
                "Assets/Resource/Icon_Fish",
                iconFiles,
                dataManager.GetAll<ItemData_Fish>());
        }

        [MenuItem("Tools/DesktopCompanion/Addressables - Fish 모델 그룹 등록")]
        public static void BuildFishModelGroup()
        {
            var dataManager = new DataManager();
            dataManager.Load();

            // Assets/Resource/Model_Fish/Prefab_Fish_{ID}/Fish_{ID}.prefab — ID 기준 폴더 규약이라 매핑표 불필요.
            var modelFiles = new Dictionary<int, string>();
            foreach (ItemData_Fish data in dataManager.GetAll<ItemData_Fish>())
            {
                modelFiles[data.ID] = $"Prefab_Fish_{data.ID}/Fish_{data.ID}";
            }

            BuildAssetGroup(
                "Model_Fish",
                "Assets/Resource/Model_Fish",
                ".prefab",
                AssetUsage.Model,
                modelFiles,
                dataManager.GetAll<ItemData_Fish>());
        }

        private static void BuildIconGroup<T>(
            string groupName,
            string iconFolder,
            IReadOnlyDictionary<int, string> iconFiles,
            IEnumerable<T> dataList) where T : GameData
            => BuildAssetGroup(groupName, iconFolder, ".png", AssetUsage.Icon, iconFiles, dataList);

        private static void BuildAssetGroup<T>(
            string groupName,
            string assetFolder,
            string extension,
            string usage,
            IReadOnlyDictionary<int, string> assetFiles,
            IEnumerable<T> dataList) where T : GameData
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Addressables 그룹 등록",
                    "Addressables 설정이 없습니다.\n" +
                    "Window → Asset Management → Addressables → Groups를 처음 열면 생성됩니다.", "확인");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                AddressableAssetGroup template = settings.FindGroup(SchemaTemplateGroupName);
                List<AddressableAssetGroupSchema> schemasToCopy = template != null
                    ? new List<AddressableAssetGroupSchema>(template.Schemas)
                    : null;
                group = schemasToCopy != null
                    ? settings.CreateGroup(groupName, false, false, false, schemasToCopy)
                    : settings.CreateGroup(groupName, false, false, false, null,
                        typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }

            int registered = 0;
            var missing = new List<string>();
            foreach (T data in dataList)
            {
                if (!assetFiles.TryGetValue(data.ID, out string relativePath))
                {
                    missing.Add($"{typeof(T).Name}#{data.ID} '{data.Name}' → 파일 매핑 없음");
                    continue;
                }

                string assetPath = $"{assetFolder}/{relativePath}{extension}";
                if (AssetImporter.GetAtPath(assetPath) == null)
                {
                    missing.Add($"{typeof(T).Name}#{data.ID} '{data.Name}' → {assetPath} 없음");
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                entry.address = AssetKeys.Of(data, usage);
                entry.SetLabel(PreloadLabel, true, force: true, postEvent: false);
                registered++;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, group, true, true);
            AssetDatabase.SaveAssets();

            string message = $"등록 {registered}건" + (missing.Count > 0 ? $", 누락 {missing.Count}건" : "");
            if (missing.Count > 0)
            {
                Debug.LogWarning($"[AddressableIconGroupBuilder] 누락 {missing.Count}건:\n" + string.Join("\n", missing));
            }
            Debug.Log($"[AddressableIconGroupBuilder] {groupName} 그룹 {message}");
            EditorUtility.DisplayDialog("Addressables 그룹 등록",
                message + (missing.Count > 0 ? "\n\n" + string.Join("\n", missing) : ""), "확인");
        }
    }
}
