using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Views;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// 에셋 키 검증 툴(D18·§16.6): 전 GameData를 순회하며 타입별 필수 용도의
    /// 파생/오버라이드 키가 Addressables 카탈로그(주소)에 실존하는지 검사한다.
    /// 누락/오타를 빌드 전에 발견하는 것이 목적.
    /// </summary>
    public static class AssetKeyValidator
    {
        // 타입별 필수 에셋 용도 선언표 — 게임 디자인 확정에 따라 팀이 갱신할 것.
        private static readonly Dictionary<Type, string[]> s_requiredUsages = new()
        {
            { typeof(ItemData_Fish),        new[] { AssetUsage.Icon, AssetUsage.Model } },
            { typeof(ItemData_Equipment),   new[] { AssetUsage.Icon } },
            { typeof(ItemData_Materials),   new[] { AssetUsage.Icon } },
            { typeof(ItemData_Consumables), new[] { AssetUsage.Icon } },
            { typeof(BattleFishData),       new[] { AssetUsage.Model } },
            { typeof(StageData),            new[] { AssetUsage.Background } },
            // PlayerData/LicenseData: 현재 비주얼 에셋 불요 → 미등록(검사 제외)
        };

        [MenuItem("Tools/DesktopCompanion/에셋 키 검증")]
        public static void Validate()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("에셋 키 검증",
                    "Addressables 설정이 없습니다.\n" +
                    "Window → Asset Management → Addressables → Groups를 처음 열면 생성됩니다.", "확인");
                return;
            }

            // 카탈로그의 모든 주소 수집 (주의: 스프라이트 서브에셋 주소 'addr[name]' 형태는 미열거)
            var addresses = new HashSet<string>();
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    addresses.Add(entry.address);
                }
            }

            // 런타임과 동일한 소스 우선순위로 데이터 로드(StreamingAssets JSON → Resources SO)
            var dataManager = new DataManager();
            dataManager.Load();

            int checkedCount = 0;
            var missing = new List<string>();
            foreach (GameData data in dataManager.GetAllLoaded())
            {
                if (!s_requiredUsages.TryGetValue(data.GetType(), out string[] usages))
                {
                    continue;
                }
                foreach (string usage in usages)
                {
                    checkedCount++;
                    string key = AssetKeys.Of(data, usage);
                    if (!addresses.Contains(key))
                    {
                        missing.Add($"{data.GetType().Name}#{data.ID} '{data.Name}' → {key}");
                    }
                }
            }

            if (missing.Count == 0)
            {
                Debug.Log($"[AssetKeyValidator] ✅ 검사 {checkedCount}건 — 누락 없음");
                EditorUtility.DisplayDialog("에셋 키 검증", $"검사 {checkedCount}건 — 누락 없음 ✅", "확인");
            }
            else
            {
                Debug.LogError($"[AssetKeyValidator] ❌ 누락 {missing.Count}/{checkedCount}건:\n" +
                               string.Join("\n", missing));
                EditorUtility.DisplayDialog("에셋 키 검증",
                    $"검사 {checkedCount}건 중 누락 {missing.Count}건 ❌\n\n" +
                    string.Join("\n", missing.Take(15)) +
                    (missing.Count > 15 ? $"\n… 외 {missing.Count - 15}건 (Console 참조)" : ""), "확인");
            }
        }
    }
}
