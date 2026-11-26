using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace DesktopCompanion.EditorTools
{
    /// <summary>실제 ScriptableObject/JSON 로더를 사용하는 재현 가능한 장비 규칙 회귀 검사.</summary>
    public static class EquipmentRuleChecks
    {
        [MenuItem("Tools/DesktopCompanion/장비 규칙 테스트")]
        public static void Run()
        {
            int failures = RunAll();
            if (failures != 0) throw new InvalidOperationException($"장비 규칙 테스트 {failures}건 실패");
        }

        public static void RunBatch() => EditorApplication.Exit(RunAll() == 0 ? 0 : 1);

        private static int RunAll()
        {
            int passed = 0, failed = 0;
            void Test(string name, Action<Fixture> action)
            {
                using var fixture = new Fixture();
                try { action(fixture); passed++; Debug.Log($"[EquipmentRuleChecks] PASS {name}"); }
                catch (Exception e) { failed++; Debug.LogError($"[EquipmentRuleChecks] FAIL {name}: {e}"); }
            }
            Test("고정 상점 값 / RNG 미사용 / 미리보기 일치", f =>
            {
                Check(EquipmentRule.TryCreateState(f.Data, new EquipmentCreateRequest(200001, EquipmentSource.Shop, 31),
                    new ThrowRandom(), out var state, out var error) && error == EquipmentError.None, "고정 생산");
                Check(state.Rarity == ItemRarity.Rare && state.Main.Value.Value == 14 && state.Options.Count == 2, "고정 값");
                Check(state.Options[0].Value == 2 && state.Options[1].Value == 3 && state.AttackMainStat == MainStatKind.Tec, "고정 줄/공격 기준");
                Check(EquipmentRule.TryGetFixedProfile(f.Data, 200001, 31, out var preview, out _), "미리보기");
                Check(Json(state) == Json(preview), "미리보기와 생산 결과 일치");
            });
            Test("주스탯 경계 포함/제외 / 정상 0줄 / 중복 2줄", f =>
            {
                var low = f.Roll(new SequenceRandom(0));
                Check(low.Main.Value.Value == 10 && low.Rarity == ItemRarity.Normal && low.Options.Count == 0, "하한 포함");
                var high = f.Roll(new SequenceRandom(2, 0, 1));
                Check(high.Main.Value.Value == 12 && high.Rarity == ItemRarity.Rare, "상한 미포함, 다음 구간 하한 포함");
                Check(high.Options.Count == 2 && high.Options.All(x => x.Stat == PlayerStat_New.AttackPoint), "중복 후보 허용");
                Check(high.Options[0].Value == 2 && high.Options[1].Value == 3, "줄마다 별도 값");
                Check(f.Roll(new SequenceRandom(4)).Main.Value.Value == 14, "균등 분포 최댓값 도달");
            });
            Test("티어마다 다른 레어도 경계", f =>
            {
                Set(f.Rod, "m_tier", 2); Set(f.MainRule, "m_min", 30f); Set(f.MainRule, "m_max", 34f);
                f.Add<EquipmentRarityRange>(3, "m_tier", 2, "m_rarity", ItemRarity.Rare, "m_minInclusive", 30f, "m_maxExclusive", 35f);
                f.Add<EquipmentOptionPool>(2, "m_groupId", 21, "m_tier", 2, "m_rarity", ItemRarity.Rare,
                    "m_stat", PlayerStat_New.AttackPoint, "m_weight", 1, "m_valueRuleId", 12);
                f.Load(); Check(f.Roll(new SequenceRandom(0)).Rarity == ItemRarity.Rare, "티어 2의 30은 레어");
            });
            Test("주스탯 없는 부위 / 0 가중치 제외", f =>
            {
                Check(EquipmentRule.TryCreateState(f.Data, new EquipmentCreateRequest(200002, EquipmentSource.Reward),
                    new SequenceRandom(), out var state, out _), "모자 생산");
                Check(state.Main == null && state.AttackMainStat == null && state.Rarity == ItemRarity.Rare, "별도 레어도 표");
            });
            Test("값별 가중치 / 0 가중치 제외 / float 격자", f =>
            {
                Set(f.MainRule, "m_distribution", EquipmentValueDistribution.WeightedValues);
                f.Add<EquipmentValueWeight>(1, "m_ruleId", 11, "m_value", 10f, "m_weight", 0);
                f.Add<EquipmentValueWeight>(2, "m_ruleId", 11, "m_value", 14f, "m_weight", 7);
                f.Load(); Check(f.Roll(new SequenceRandom()).Main.Value.Value == 14f, "가중치 0 제외");
                Set(f.OptionRule, "m_min", 0.1f); Set(f.OptionRule, "m_max", 0.3f); Set(f.OptionRule, "m_step", 0.1f);
                var state = f.Roll(new SequenceRandom(2, 0));
                Check(state.Options[0].Value == 0.3f, "소수 격자 최대값");
            });
            Test("레어도 구간 누락/겹침 / 잘못된 간격 / 후보 누락 거부", f =>
            {
                Set(f.RareRange, "m_minInclusive", 13f); f.RejectRandom();
                Set(f.RareRange, "m_minInclusive", 11f); f.RejectRandom();
                Set(f.RareRange, "m_minInclusive", 12f);
                Set(f.MainRule, "m_step", 3f); f.RejectRandom(); Set(f.MainRule, "m_step", 1f);
                Set(f.Rod, "m_optionGroupId", 999); f.RejectRandom();
            });
            Test("음수/모두 0 가중치 / Multiply / Boss 거부", f =>
            {
                Set(f.Pool, "m_weight", -1); f.RejectRandom();
                Set(f.Pool, "m_weight", 0); f.RejectRandom(); Set(f.Pool, "m_weight", 1);
                Set(f.Pool, "m_operation", ModifierOperation_New.Multiply); f.RejectRandom();
                Set(f.Pool, "m_operation", ModifierOperation_New.Add);
                Set(f.RareRange, "m_rarity", ItemRarity.Boss); f.RejectRandom();
            });
            Test("고정 프로필 잘못된 장비/옵션 순번 거부", f =>
            {
                Check(!EquipmentRule.TryGetFixedProfile(f.Data, 200002, 31, out _, out _), "장비 ID 불일치");
                Set(f.FixedSecond, "m_index", 0);
                Check(!EquipmentRule.TryGetFixedProfile(f.Data, 200001, 31, out _, out _), "중복 순번 거부");
            });
            Test("저장 JSON 왕복 / 밸런스 변경 후 확정값 유지 / 사본 격리", f =>
            {
                var state = f.Roll(new SequenceRandom(4, 0, 1));
                EquipmentRule.TryCaptureState(state, out var save, out _);
                var restoredSave = JsonConvert.DeserializeObject<EquipmentStateSave>(JsonConvert.SerializeObject(save));
                Set(f.MainRule, "m_min", 999f); Set(f.RareRange, "m_rarity", ItemRarity.Legendary);
                Set(f.Rod, "m_mainStat", MainStatKind.Luk); Set(f.Rod, "m_attackMainStat", MainStatKind.Str);
                Check(EquipmentRule.TryRestoreState(f.Rod, restoredSave, out var restored, out _), "현 시트와 다른 과거 값 복원");
                Check(Json(state) == Json(restored), "순서/중복/값/공격 기준/레어도 보존");
                restoredSave.Options[0].Value = 500;
                Check(restored.Options[0].Value == 2 && state.Options[0].Value == 2, "DTO 사본 격리");
                Check(EquipmentRule.TryGetEffects(restored, out var effects, out _)
                    && effects.Modifiers.Count == 2 && effects.Modifiers.Sum(x => x.Value) == 5, "효과 변환");
            });
            Test("손상된 저장값 거부", f =>
            {
                var state = f.Roll(new SequenceRandom(4));
                Action<EquipmentStateSave>[] corrupt = {
                    x => x.Version = 2, x => x.DataId = 200002, x => x.Rarity = "Boss", x => x.Rarity = "2",
                    x => x.Main = null, x => x.Main.Value = null, x => x.Main.Value = float.NaN,
                    x => x.AttackMainStat = "None", x => x.Options = null, x => x.Options.RemoveAt(0),
                    x => x.Options[0].Stat = "999", x => x.Options[0].Value = float.PositiveInfinity,
                    x => x.Options[0].Value = null, x => x.Options[0].Operation = "Multiply"
                };
                foreach (var change in corrupt)
                {
                    EquipmentRule.TryCaptureState(state, out var save, out _); change(save);
                    Check(!EquipmentRule.TryRestoreState(f.Rod, save, out var rejected, out var error)
                        && rejected == null && error == EquipmentError.InvalidSave, "손상 데이터 실패 계약");
                }
            });
            Test("장비 개체 1회 주입 / 구형 효과 중복 방지", f =>
            {
                // 테스트 전용 핸들. 실제 핸들 발급은 EntityManager가 담당한다.
                var entity = new Entity_Equipment_New(default, f.Rod);
                var state = f.Roll(new SequenceRandom(4));
                Check(entity.State == null && entity.TryInitializeState(state) && !entity.TryInitializeState(state), "1회 주입");
                Check(typeof(Entity_Equipment_New).GetProperty("CurrentModifiers") == null, "신규 장비에 구형 효과 경로 없음");
                Check(!new Entity_Equipment_New(default, f.Hat).TryInitializeState(state), "장비 ID 일치 검사");
            });
            Test("요청 오류 / 미설정 신규 정의 / 고정 프로필 미지정", f =>
            {
                Check(!EquipmentRule.TryCreateState(f.Data, new EquipmentCreateRequest(200001, EquipmentSource.Shop), null, out _, out _), "Shop 프로필 필수");
                Check(!EquipmentRule.TryCreateState(f.Data, new EquipmentCreateRequest(200001, EquipmentSource.Craft, 31), new Random(1), out _, out _), "Craft 프로필 금지");
                Check(!EquipmentRule.TryCreateState(f.Data, new EquipmentCreateRequest(200001, EquipmentSource.Craft), null, out _, out _), "Random 필수");
                Set(f.Rod, "m_mainStat", MainStatKind.None); f.RejectRandom();
            });
            Test("시트 전수 검증 / 필수 값 누락 / 변환 전 중복 ID", f =>
            {
                Check(EquipmentRule.ValidateData(f.Rows).Count == 0, "기본 표 전수 검증");
                var missing = JArray.Parse("[{\"type\":\"EquipmentOptionPool\",\"m_id\":1}]");
                Check(EquipmentSheetValidator.ValidateItems(missing).Any(x => x.Contains("m_stat")), "누락 enum을 기본값으로 수용하지 않음");
                var rows = new SortedDictionary<int, Dictionary<int, object>> {
                    [1] = new() { [1] = "m_id", [2] = "m_ruleId" },
                    [3] = new() { [1] = 1, [2] = 11 }, [4] = new() { [1] = 1, [2] = 11 }
                };
                Check(EquipmentSheetValidator.ValidateRows(new() { ["EquipmentValueWeight"] = rows }).Count == 1, "ID 병합 전 중복 탐지");
            });
            Test("구형과 신규 타입의 데이터·팩토리 격리", f =>
            {
                var legacy = f.Add<ItemData_Equipment>(200001, "m_tier", 1,
                    "m_mountingArea", EquipmentMountingArea.FishingRod);
                var oldProduct = f.Add<ShopProducts>(100001, "m_baseId", 200001, "m_itemType", ItemType.Equipment);
                var newProduct = f.Add<ShopProducts_New>(100001, "m_baseId", 200001,
                    "m_itemType", ItemType.Equipment, "m_equipmentProfileId", 31);
                f.Load();
                Check(ReferenceEquals(f.Data.GetData<ItemData_Equipment>(200001), legacy), "구형 조회는 원본 데이터 유지");
                Check(ReferenceEquals(f.Data.GetData<ItemData_Equipment_New>(200001), f.Rod), "신규 조회는 별도 정의");
                Check(ReferenceEquals(f.Data.GetData<ShopProducts>(100001), oldProduct)
                    && ReferenceEquals(f.Data.GetData<ShopProducts_New>(100001), newProduct), "동일 상품 ID도 타입별 격리");
                Check(!typeof(ItemData_Equipment).IsAssignableFrom(typeof(ItemData_Equipment_New))
                    && !typeof(Entity_Equipment).IsAssignableFrom(typeof(Entity_Equipment_New))
                    && typeof(ItemData_Equipment).GetProperty("MainStat") == null
                    && typeof(Entity_Equipment).GetProperty("State") == null
                    && typeof(ShopProducts).GetProperty("EquipmentProfileId") == null, "원본 클래스 계약 유지");
                var entities = new EntityManager(f.Data);
                entities.RegisterFactory(typeof(ItemData_Equipment), (id, data) => new Entity_Equipment(id, (ItemData_Equipment)data));
                entities.RegisterFactory(typeof(ItemData_Equipment_New), (id, data) => new Entity_Equipment_New(id, (ItemData_Equipment_New)data));
                var oldHandle = entities.Create<ItemData_Equipment>(200001);
                var newHandle = entities.Create<ItemData_Equipment_New>(200001);
                Check(entities.Get<Entity_Equipment>(oldHandle) != null && entities.Get<Entity_Equipment>(newHandle) == null, "구형 개체 판정은 신규 장비를 받지 않음");
                Check(entities.Get<Entity_Equipment_New>(newHandle).TryInitializeState(f.Roll(new SequenceRandom(4))), "신규 팩토리와 상태 주입");
                var legacyOnly = new DataManager(); legacyOnly.Load(new GameData[] { legacy });
                Check(!EquipmentRule.TryCreateState(legacyOnly, new EquipmentCreateRequest(200001, EquipmentSource.Craft),
                    new ThrowRandom(), out _, out var error) && error == EquipmentError.InvalidDataId, "구형 데이터로 자동 폴백하지 않음");
            });
            Test("신규 시트 이름 정규화와 JSON 로드", f =>
            {
                var external = JObject.Parse(@"{
                    'ItemData_Equipment': { '200001': { 'm_tier': 1, 'm_mountingArea': 'FishingRod' } },
                    'ItemData_Equipment_New': { '200001': { 'm_tier': 1, 'm_mountingArea': 'FishingRod',
                        'm_mainStat': 'Str', 'm_attackMainStat': 'Tec' } },
                    'ShopProducts': { '100001': { 'm_baseId': 200001, 'm_itemType': 'Equipment' } },
                    'ShopProducts_New': { '100001': { 'm_baseId': 200001, 'm_itemType': 'Equipment' } }
                }");
                var normalized = ExternalSheetNormalizer.Normalize(external, GameDataTypes.BuildTypeMap());
                Check(normalized.Count == 4 && EquipmentSheetValidator.ValidateItems(normalized).Count == 0, "신구 테이블 별도 정규화");
                string directory = Path.Combine(Path.GetTempPath(), "equipment-new-test-" + Guid.NewGuid().ToString("N"));
                var loaded = new List<GameData>();
                Directory.CreateDirectory(directory);
                try
                {
                    File.WriteAllText(Path.Combine(directory, "data.json"), normalized.ToString());
                    Check(JsonDataLoader.TryLoadAll(directory, out loaded), "실제 JSON 로더 호출");
                    var data = new DataManager(); data.Load(loaded);
                    Check(data.GetData<ItemData_Equipment>(200001) != null
                        && data.GetData<ItemData_Equipment_New>(200001).MainStat == MainStatKind.Str
                        && data.GetData<ShopProducts>(100001) != null
                        && data.GetData<ShopProducts_New>(100001) != null, "같은 ID의 신구 데이터를 각각 로드");
                }
                finally
                {
                    foreach (var row in loaded) UnityEngine.Object.DestroyImmediate(row);
                    File.Delete(Path.Combine(directory, "data.json"));
                    Directory.Delete(directory);
                }
            });
            Test("신규 정의·상품의 정수 필드 소수 거부", f =>
            {
                var cases = new[] {
                    ("ItemData_Equipment_New", "m_tier", 1.4),
                    ("ItemData_Equipment_New", "m_mainValueRuleId", 0.4),
                    ("ItemData_Equipment_New", "m_optionGroupId", 0.4),
                    ("ItemData_Equipment_New", "m_rarityGroupId", 0.4),
                    ("ShopProducts_New", "m_baseId", 200001.4),
                    ("ShopProducts_New", "m_equipmentProfileId", 0.4)
                };
                foreach (var (typeName, column, value) in cases)
                {
                    var row = new JObject { ["type"] = typeName, ["m_id"] = 1, [column] = value };
                    Check(EquipmentSheetValidator.ValidateItems(new JArray(row)).Any(x => x.Contains(column)),
                        typeName + "." + column + " 소수를 정수로 반올림하지 않고 거부");
                }
            });
            Test("모든 레어도 옵션 개수 / 가중치 구간 선택", f =>
            {
                foreach (ItemRarity rarity in new[] { ItemRarity.Normal, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary })
                {
                    Set(f.RareRange, "m_rarity", rarity);
                    Set(f.Pool, "m_rarity", rarity);
                    var state = f.Roll(new SequenceRandom(4));
                    Check(state.Rarity == rarity && state.Options.Count == (int)rarity, "레어도별 0~4줄");
                }
                Set(f.RareRange, "m_rarity", ItemRarity.Rare); Set(f.Pool, "m_rarity", ItemRarity.Rare);
                Set(f.MainRule, "m_distribution", EquipmentValueDistribution.WeightedValues);
                f.Add<EquipmentValueWeight>(1, "m_ruleId", 11, "m_value", 10f, "m_weight", 1);
                f.Add<EquipmentValueWeight>(2, "m_ruleId", 11, "m_value", 14f, "m_weight", 3);
                f.Load();
                Check(f.Roll(new FractionRandom(0.249)).Main.Value.Value == 10f, "첫 가중치 구간 내부");
                Check(f.Roll(new FractionRandom(0.25)).Main.Value.Value == 14f, "경계는 다음 가중치 구간");
                Check(f.Roll(new FractionRandom(0.999)).Main.Value.Value == 14f, "마지막 가중치 구간");
            });
            Test("실제 개체 저장·삭제·동일 핸들 복원", f =>
            {
                var entities = new EntityManager(f.Data);
                entities.RegisterFactory(typeof(ItemData_Equipment_New), (id, data) => new Entity_Equipment_New(id, (ItemData_Equipment_New)data));
                var handle = entities.Create<ItemData_Equipment_New>(200001);
                var entity = entities.Get<Entity_Equipment_New>(handle);
                Check(entity.TryInitializeState(f.Roll(new SequenceRandom(4, 0, 1))), "생산 상태 주입");
                entity.SetUpgradeLevel(2);
                Check(EquipmentRule.TryCaptureState(entity.State, out var save, out _), "저장 상태 복사");
                var settings = new JsonSerializerSettings { ContractResolver = new SerializeFieldContractResolver() };
                var entry = new EquipmentSaveEntry { Handle = handle.ToString(), State = save };
                var loaded = JsonConvert.DeserializeObject<EquipmentSaveEntry>(JsonConvert.SerializeObject(entry, settings), settings);
                int level = entity.UpgradeLevel;
                entities.Destroy(handle);
                Check(EntityHandle.TryParse(loaded.Handle, out var restoredHandle) && restoredHandle.Value != Guid.Empty
                    && !entities.TryGet(restoredHandle, out _), "저장 핸들 형식·중복 검사");
                Check(EquipmentRule.TryRestoreState(f.Rod, loaded.State, out var state, out _), "저장값 검증");
                var restored = entities.Get<Entity_Equipment_New>(entities.Restore<ItemData_Equipment_New>(restoredHandle, loaded.State.DataId));
                Check(restored != null && restored.TryInitializeState(state), "동일 핸들 복원·상태 주입");
                restored.SetUpgradeLevel(level);
                Check(restored.Id.Equals(handle) && restored.UpgradeLevel == 2 && Json(restored.State) == Json(entity.State), "핸들·강화·중복 옵션 보존");
            });
            Test("현재 XLSX 장비 헤더 / 기존 JSON과의 로드 호환", f =>
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/GameData_NewNeoUltraSuper.xlsx"));
                var sheets = XlsxSheetReader.Read(path);
                Check(sheets.ContainsKey("EquipmentOptionPool") && sheets.ContainsKey("EquipmentFixedProfile") && sheets.ContainsKey("ShopProducts_New") && sheets.ContainsKey("ItemData_Equipment_New"), "신규 탭");
                Check(EquipmentSheetValidator.ValidateRows(sheets).Count == 0, "장비 행 유효성");
                var build = typeof(GameDataConverter_New).GetMethod("BuildExternalShape", BindingFlags.NonPublic | BindingFlags.Static);
                var external = (JObject)build.Invoke(null, new object[] { sheets });
                var normalized = ExternalSheetNormalizer.Normalize(external, GameDataTypes.BuildTypeMap());
                Check(EquipmentSheetValidator.ValidateItems(normalized).Count == 0, "작성한 시트 변환 검증");
                var current = JArray.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Data/GameData.json")));
                Check(EquipmentSheetValidator.ValidateItems(current).Count == 0, "기존 JSON 검증 호환");
            });
            Debug.Log($"[EquipmentRuleChecks] RESULT passed={passed} failed={failed}");
            return failed;
        }

        private static string Json(EquipmentState state)
        {
            EquipmentRule.TryCaptureState(state, out var save, out _);
            return JsonConvert.SerializeObject(save);
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Set(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                field.SetValue(target, value); return;
            }
            throw new MissingFieldException(name);
        }
        private sealed class SequenceRandom : Random
        {
            private readonly Queue<int> values;
            internal SequenceRandom(params int[] values) => this.values = new Queue<int>(values);
            public override int Next(int maxValue)
            {
                int value = values.Count > 0 ? values.Dequeue() : 0;
                Check(value >= 0 && value < maxValue, "테스트 난수 범위"); return value;
            }
            public override double NextDouble() => 0;
        }
        private sealed class FractionRandom : Random
        {
            private readonly double fraction;
            internal FractionRandom(double fraction) => this.fraction = fraction;
            public override double NextDouble() => fraction;
            public override int Next(int maxValue) => 0;
        }
        private sealed class ThrowRandom : Random
        {
            public override int Next(int maxValue) => throw new InvalidOperationException("난수 소비 금지");
            public override double NextDouble() => throw new InvalidOperationException("난수 소비 금지");
        }
        private sealed class Fixture : IDisposable
        {
            internal readonly List<GameData> Rows = new();
            internal readonly DataManager Data = new();
            internal ItemData_Equipment_New Rod, Hat;
            internal EquipmentValueRule MainRule, OptionRule;
            internal EquipmentRarityRange RareRange;
            internal EquipmentOptionPool Pool;
            internal EquipmentFixedOption FixedSecond;
            internal Fixture()
            {
                Rod = Add<ItemData_Equipment_New>(200001, "m_tier", 1, "m_mountingArea", EquipmentMountingArea.FishingRod,
                    "m_mainStat", MainStatKind.Str, "m_attackMainStat", MainStatKind.Tec, "m_mainValueRuleId", 11, "m_optionGroupId", 21);
                Hat = Add<ItemData_Equipment_New>(200002, "m_tier", 1, "m_mountingArea", EquipmentMountingArea.Hat, "m_rarityGroupId", 41, "m_optionGroupId", 21);
                MainRule = Add<EquipmentValueRule>(11, "m_min", 10f, "m_max", 14f, "m_step", 1f);
                OptionRule = Add<EquipmentValueRule>(12, "m_min", 2f, "m_max", 3f, "m_step", 1f);
                Add<EquipmentRarityRange>(1, "m_tier", 1, "m_rarity", ItemRarity.Normal, "m_minInclusive", 10f, "m_maxExclusive", 12f);
                RareRange = Add<EquipmentRarityRange>(2, "m_tier", 1, "m_rarity", ItemRarity.Rare, "m_minInclusive", 12f, "m_maxExclusive", 15f);
                Pool = Add<EquipmentOptionPool>(1, "m_groupId", 21, "m_tier", 1, "m_rarity", ItemRarity.Rare, "m_stat", PlayerStat_New.AttackPoint, "m_weight", 1, "m_valueRuleId", 12);
                Add<EquipmentRarityWeight>(1, "m_groupId", 41, "m_tier", 1, "m_rarity", ItemRarity.Normal, "m_weight", 0);
                Add<EquipmentRarityWeight>(2, "m_groupId", 41, "m_tier", 1, "m_rarity", ItemRarity.Rare, "m_weight", 100);
                Add<EquipmentFixedProfile>(31, "m_equipmentId", 200001, "m_rarity", ItemRarity.Rare, "m_mainValue", (float?)14f);
                Add<EquipmentFixedOption>(1, "m_profileId", 31, "m_index", 0, "m_stat", PlayerStat_New.AttackPoint, "m_value", 2f);
                FixedSecond = Add<EquipmentFixedOption>(2, "m_profileId", 31, "m_index", 1, "m_stat", PlayerStat_New.AttackPoint, "m_value", 3f);
                Load();
            }
            internal T Add<T>(int id, params object[] fields) where T : GameData
            {
                var row = ScriptableObject.CreateInstance<T>(); Set(row, "m_id", id);
                for (int i = 0; i < fields.Length; i += 2) Set(row, (string)fields[i], fields[i + 1]);
                Rows.Add(row); return row;
            }
            internal void Load() => Data.Load(Rows);
            internal EquipmentState Roll(Random random)
            {
                Check(EquipmentRule.TryCreateState(Data, new EquipmentCreateRequest(200001, EquipmentSource.Craft), random, out var state, out _), "랜덤 생산 성공");
                return state;
            }
            internal void RejectRandom()
            {
                Check(!EquipmentRule.TryCreateState(Data, new EquipmentCreateRequest(200001, EquipmentSource.Craft),
                    new ThrowRandom(), out var state, out var error) && state == null && error == EquipmentError.InvalidRule, "잘못된 규칙은 난수 소비 전에 거부");
            }
            public void Dispose() { foreach (var row in Rows) UnityEngine.Object.DestroyImmediate(row); }
        }
    }
}
