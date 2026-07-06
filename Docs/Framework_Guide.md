# Desktop Companion 프레임워크 사용 가이드 (팀원용)

> 대상: 게임플레이 코드를 작성하는 팀원
> 범위: 현재까지 구현된 **GameManager / DataManager / EntityManager / SystemManager** 구조의 사용법
> 설계 근거·결정(D1~D11)은 [`Framework_Plan.md`](Framework_Plan.md) 참조. 이 문서는 "어떻게 쓰는가"에 집중한다.
> 각 System(인벤토리·전투 등)의 실제 구현은 **팀원이 담당**한다. 프레임워크는 그 뼈대와 규칙을 제공한다.

---

## 1. 한눈에 보는 구조

```
GameManager (composition root, MonoBehaviour)
    ├─ DataManager      : 콘텐츠(정의) 로드/조회 — JSON(StreamingAssets) 우선, 없으면 SO(Resources)
    ├─ EntityManager    : Entity 생성·조회·소멸 (생명주기 중앙화)
    ├─ SystemManager    : System 보유·초기화·조회
    │       └─ (팀원이 만드는) ItemInventorySystem, BattleSystem, ...
    ├─ SaveManager      : 세이브 저장/복원 (ISaveable 자동 수집, 종료 시 자동 저장)
    └─ AssetProvider    : 비주얼 에셋 (Addressables 프리로드·조회) — View 전용 (§12)
```

**의존성은 항상 위로만 흐른다(하위 → 상위). 상위는 하위를 모른다.**
- System → EntityManager (Entity 다룸) ✅
- System → 다른 System (SystemManager.GetSystem 경유) ✅
- EntityManager → System ❌ (EntityManager는 도메인을 모른다)
- System → UI ❌ (UI가 System을 구독한다. §7)

---

## 2. 핵심 개념

| 개념 | 정체 | 예 |
|------|------|-----|
| **Data** (`GameData` 상속 SO) | 변하지 않는 정의값(청사진) | `ItemData_Fish`(이름·가격) |
| **Entity** (`Entity` 상속) | Data로 만든 런타임 인스턴스, 가변 상태 보유 | `Entity_Fish`, `Entity_Materials`(수량) |
| **EntityHandle** | Entity를 가리키는 GUID 핸들(값 타입) | `EntityManager.Get(handle)` |
| **System** (`SystemBase` 상속) | 도메인 기능 + 도메인 상태(소속) | `ItemInventorySystem` |
| **Manager** | 프레임워크 골격 | Game/Data/Entity/System |

핵심 원칙 2가지:
1. **생명주기(존재)는 EntityManager, 도메인 소속은 System.** "인벤토리에 뭐가 있나"는 System이, "그 Entity가 존재하나"는 EntityManager가 안다. (예: 아이템을 버려도 존재는 유지, 인벤토리 소속만 빠짐)
2. **Entity는 얇다.** 로직은 System에, 정적값은 Data에. Entity는 핸들+가변상태만.

---

## 3. 초기화 흐름 (자동)

씬의 GameManager가 2단계로 조립한다. 팀원이 건드릴 곳은 **`RegisterSystems()` 한 곳**뿐이다.

```
[Awake — 동기]
Data 로드(JSON→SO 폴백) → EntityManager 생성+팩토리 등록 → SystemManager 생성
  → System 등록 → InitializeAll()  ← 기본 상태 구성
  → SaveManager 생성 → ISaveable 자동 등록 → 세이브 로드  ← 저장 상태 덮어쓰기

[Start — 비동기]
AssetProvider 프리로드(preload 라벨 전량) → OnBootCompleted()  ← 이후 UI/World 초기화 지점
```
- 초기화 후 **Entity는 0개**다 — Entity 생성은 각 System이 필요할 때 `Create`로 한다(예: StageSystem은 Initialize에서 전 스테이지 생성, 인벤토리는 획득 시마다).
- 뷰(UI/World)는 `OnBootCompleted` 이후에만 안전 — 그 전엔 에셋이 준비되지 않았다.

---

## 4. ★ 팀원이 할 일 — System 만들기

### 4-1. SystemBase 상속

```csharp
using DesktopCompanion.Systems;
using DesktopCompanion.Entities;
using DesktopCompanion.Data;
using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    public class ItemInventorySystem : SystemBase
    {
        // 도메인 상태(소속): 어떤 Entity가 인벤토리에 있는지 = 핸들만 보관
        private readonly List<EntityHandle> m_slots = new();

        // UI가 구독할 이벤트 (System은 UI를 모른다 — "쏘고 잊음")
        public event Action<EntityHandle> OnItemAdded;
        public event Action<EntityHandle> OnItemRemoved;

        // 다른 요소 준비 후 초기화 (여기서 구독)
        public override void Initialize()
        {
            // 소멸된 Entity를 소속에서 자동 정리 (dangling 방지)
            EntityManager.OnEntityDestroyed += HandleEntityDestroyed;
        }

        // ── 도메인 기능 ──
        public void Add(EntityHandle handle)
        {
            if (m_slots.Contains(handle)) return;
            m_slots.Add(handle);
            OnItemAdded?.Invoke(handle);        // UI 통지
        }

        public void Remove(EntityHandle handle)
        {
            if (m_slots.Remove(handle))
                OnItemRemoved?.Invoke(handle);
        }

        // 새 물고기를 만들어 인벤토리에 넣는 예 (Entity 생성은 EntityManager에 요청)
        public EntityHandle AddNewFish(int dataId)
        {
            var handle = EntityManager.Create<ItemData_Fish>(dataId);
            Add(handle);
            return handle;
        }

        // ── UI/뷰용 읽기 전용 API (ViewModel이 조회) ──
        public IReadOnlyList<EntityHandle> Slots => m_slots;

        private void HandleEntityDestroyed(EntityHandle handle) => Remove(handle);
    }
}
```

### 4-2. EntityManager 사용법 (SystemBase의 `EntityManager` 프로퍼티)

| 하고 싶은 것 | 호출 |
|--------------|------|
| Entity 생성 | `EntityManager.Create<ItemData_Fish>(dataId)` → `EntityHandle` |
| 실체 조회 | `EntityManager.Get(handle)` / `EntityManager.Get<Entity_Fish>(handle)` |
| 안전 조회 | `EntityManager.TryGet(handle, out var e)` |
| 소멸 | `EntityManager.Destroy(handle)` (→ `OnEntityDestroyed` 발행) |

> **핸들만 보관하라.** System은 Entity 객체가 아니라 `EntityHandle`을 들고, 필요할 때 `Get`으로 실체를 얻는다. 소멸된 핸들은 `Get`이 `null`을 준다.

### 4-3. 다른 System 참조 (D11)

필드로 직접 물지 말고, 필요할 때 `SystemManager`에서 얻는다.

```csharp
var battle = SystemManager.GetSystem<BattleSystem>();
battle?.DoSomething();
```
> **순환 의존 주의**: A→B, B→A로 서로 물면 곤란. 잦은 상호작용은 이벤트로 푸는 걸 고려.

### 4-4. GameManager에 등록

작성한 System을 [`GameManager.RegisterSystems()`](../Assets/_Main/1_Scripts/0_Core/GameManager.cs)에 한 줄 추가한다. (의존성 주입은 SystemManager가 알아서 함)

```csharp
private void RegisterSystems()
{
    m_systemManager.Register(new ItemInventorySystem());
    // m_systemManager.Register(new BattleSystem());
}
```

이게 전부다. 이후 어디서든 `SystemManager.GetSystem<ItemInventorySystem>()`로 접근 가능.

---

## 5. Data 추가하기

1. `GameData`(또는 `ItemData`) 상속 SO 작성 + `[CreateAssetMenu]`.
   **⚠️ 파일명 = 클래스명 필수** (예: `BattleFishData`는 `BattleFishData.cs`에). 다르면 에셋 생성 시 "No script asset for ..." 경고와 함께 스크립트 연결이 깨진다. (`[Serializable]` 일반 클래스·enum은 해당 없음 — SO만)
   ```csharp
   [CreateAssetMenu(menuName = "DesktopCompanion/Battle/Fish")]
   public class BattleFishData : GameData { /* 필드 */ }
   ```
2. 에셋 생성: Project 창 우클릭 → Create → 지정한 메뉴 → 값 입력.
3. 배치: `Assets/_Main/Resources/Data/` 하위(계열별 폴더 자유)에 두면 `DataManager.Load()`가 자동 수집.
4. 조회: `DataManager.GetData<BattleFishData>(id)` — **구체 타입으로** 질의.

> `ID`는 **타입별로** 유니크하면 된다(ItemData의 1과 StageData의 1은 안 겹침).

---

## 6. Entity 추가하기 + 팩토리 등록

Data가 런타임 인스턴스로 필요하면(= Entity화):
1. `Entity` 상속 클래스 작성.
   ```csharp
   public class Entity_BattleFish : Entity
   {
       public int Hp { get; private set; }   // 가변 상태
       public Entity_BattleFish(EntityHandle id, BattleFishData data) : base(id, data) { }
   }
   ```
2. [`GameManager.RegisterEntityFactories()`](../Assets/_Main/1_Scripts/0_Core/GameManager.cs)에 매핑 한 줄:
   ```csharp
   m_entityManager.RegisterFactory(typeof(BattleFishData),
       (id, data) => new Entity_BattleFish(id, (BattleFishData)data));
   ```
그러면 `EntityManager.Create<BattleFishData>(id)`가 동작. **EntityManager/DataManager 본체는 손대지 않는다.**

> **모든 Data가 Entity가 되는 건 아니다.** 런타임 **가변 상태가 없는** 순수 설정/상수 데이터는 팩토리를 등록하지 않고 System이 `DataManager.GetData<T>(id)`/`GetAll<T>()`로 직접 읽어 쓴다. 반대로 **가변 상태가 있으면 Entity로** 만든다(예: 스테이지가 "보스 리젠 타이머"를 가지면 `StageData`+`Entity_Stage`로, StageSystem이 초기화 때 전 스테이지를 `GetAll<StageData>()`로 생성·제어). **판단 기준 = 가변 상태 유무.**

---

## 7. UI 연결 (MVVM) — 나중 단계지만 규칙만 기억

- **UI의 창구는 System 하나.** UI(ViewModel)가 System의 이벤트를 구독하고 읽기 API로 조회한다.
- **System은 UI(ViewModel/View)를 모른다.** 이벤트로만 통지("쏘고 잊음"). 결합은 `ViewModel → System` 단방향.
- EntityManager를 UI가 직접 구독하지 않는다(System 창구만).

```
InventorySystem (Model: event + 읽기 API)
   ▲ 구독/조회
InventoryViewModel  ─바인딩→  View(UI 슬롯)
```

---

## 8. 테스트 (수동 Play 검증)

자동화 테스트 없이 Play로 검증한다.
1. 테스트용 Data 에셋을 만든다(가능하면 `Assets/_Main/_TestData/` 등 Resources 밖).
2. 씬의 **GameManager** 인스펙터에서 `Use Test Data` 체크 + `Test Data` 리스트에 그 에셋들을 등록.
3. Play → `Create`/`Get`/`Destroy`와 System 동작을 로그/화면으로 확인.

> 이렇게 하면 실데이터(Resources)를 오염시키지 않고 원하는 데이터로 검증할 수 있다.

---

## 9. 규칙 요약 (Do / Don't)

**Do**
- System은 `SystemBase` 상속, `EntityManager`/`SystemManager`는 protected 프로퍼티로 사용.
- Entity 참조는 `EntityHandle`로 보관, 실체는 그때그때 `Get`.
- 도메인 상태(소속·규칙)는 System이 소유, 상태 변화는 **이벤트로 통지**.
- 새 계열은 Data + Entity + 팩토리 한 줄 + (System은) Register 한 줄로 확장.

**Don't**
- ❌ Entity에 게임 로직 넣기(로직은 System).
- ❌ Entity가 스스로 생성/소멸/핸들 발급(EntityManager만).
- ❌ EntityManager/DataManager에 도메인(아이템/전투) 지식 하드코딩.
- ❌ System이 UI를 참조하거나 다른 System을 필드로 직접 소유(→ `GetSystem`).
- ❌ `EntityManager.Create/Get` 밖에서 Entity를 `new` 하기.
- ❌ Data에 Sprite/Prefab 직접 참조 넣기(JSON 채널 붕괴 — 에셋은 §12 키 규약으로).
- ❌ System/Entity에서 AssetProvider 사용(에셋은 View 관심사).

---

## 10. API 치트시트

```csharp
// Data
T                DataManager.GetData<T>(int id)     // T : GameData (구체 타입) — 단건
IReadOnlyList<T> DataManager.GetAll<T>()            // T : GameData — 그 타입 전체

// Entity 생명주기
EntityHandle EntityManager.Create<TData>(int dataId)   // TData : GameData
Entity       EntityManager.Get(EntityHandle h)
T            EntityManager.Get<T>(EntityHandle h)      // T : Entity
bool         EntityManager.TryGet(EntityHandle h, out Entity e)
void         EntityManager.Destroy(EntityHandle h)
event Action<EntityHandle> EntityManager.OnEntityDestroyed

// System
void SystemManager.Register<T>(T system)            // T : SystemBase  (GameManager에서만)
T    SystemManager.GetSystem<T>()                   // T : SystemBase

// SystemBase (상속 시 사용 가능)
protected EntityManager EntityManager
protected DataManager   DataManager      // 정의 직접 조회(GetData/GetAll)
protected SystemManager SystemManager
virtual void Initialize()

// Entity 생명주기 (Save/Load용 추가)
EntityHandle EntityManager.Restore<TData>(EntityHandle handle, int dataId)  // 저장된 handle로 재등록

// EntityHandle
EntityHandle.New()            // (EntityManager 내부에서만 발급)
handle.ToString("N")          // 저장/직렬화 경계용
EntityHandle.TryParse(s, out h)

// ISaveable (영구 상태 System이 구현 — §11)
string SaveId { get; }
Type   StateType { get; }
object CaptureState()
void   RestoreState(object state)

// 비주얼 에셋 (View 전용 — §12)
string AssetKeys.Of(GameData data, string usage)     // 키 파생 ("{클래스명}_{ID}_{용도}" 또는 m_assetKey 오버라이드)
AssetUsage.Icon / .Model / .Background               // 용도 상수
T    AssetProvider.Get<T>(string key)                // 프리로드된 에셋 동기 조회 (없으면 에러 로그+null)
bool AssetProvider.TryGet<T>(string key, out T a)    // 안전 조회
```

---

## 11. 저장/로드 대비 — `ISaveable` (지금부터 채워두기)

서버 저장이 최종 목표다. **영구 상태를 갖는 System은 처음부터 `ISaveable`을 구현**해 두면, 나중에 Save/Load(SaveManager)를 붙일 때 **기존 로직을 안 건드리고 배선만** 하면 된다.

### 데이터는 두 채널 — 절대 섞지 말 것
- **콘텐츠(정의)**: SO(지금)/JSON(나중). `DataManager`가 유일 창구. **저장 안 함.**
- **세이브(플레이어 상태)**: `dataId 참조 + 가변 상태`만 DTO로 → JSON. **정의(SO) 자체는 저장하지 않는다.**
- 두 채널의 유일한 접점은 `dataId`.

### System에서 구현
```csharp
using DesktopCompanion.Save;

public class ItemInventorySystem : SystemBase, ISaveable
{
    // ... 기존 Add/Remove/규칙 로직은 그대로 (변경 없음) ...

    public string SaveId    => "inventory";
    public Type   StateType => typeof(InventorySave);

    public object CaptureState()
    {
        var save = new InventorySave();
        foreach (var h in m_slots)
        {
            var e = EntityManager.Get(h);
            if (e != null) save.items.Add(new InventorySave.Slot { dataId = e.DataId /*, quantity = ... */ });
        }
        return save;
    }

    public void RestoreState(object state)
    {
        var save = (InventorySave)state;
        foreach (var slot in save.items)
        {
            // 항목의 Data 타입 식별 방법은 SaveManager 설계 시 확정(예: 타입 태그).
            var h = EntityManager.Create<ItemData_Fish>(slot.dataId);
            Add(h);
        }
    }
}

[Serializable]
public class InventorySave
{
    public List<Slot> items = new();
    [Serializable] public class Slot { public int dataId; public int quantity; }
}
```

### 규칙
- 세이브 DTO엔 **dataId + 가변상태만.** 이름/가격 등 정적값(정의)은 넣지 말 것(용량·버전 취약).
- DTO는 **public 필드 기반** POCO로 작성(직렬화가 프로퍼티가 아닌 **필드만** 읽는다). `EntityHandle`은 `ToString("N")`으로 문자열화해 담는다.
- 복원은 `EntityManager.Restore(handle, dataId)`(handle 보존) 또는 `Create` + 가변상태 setter. **기존 Add/Remove 로직은 그대로 재사용.**
- 복원 순서 원칙: `Initialize()` = 기본 상태 구성 → `RestoreState()` = 저장 상태 **덮어쓰기**.

### 자동 배선 (팀원이 할 일 없음)
- GameManager가 부팅 시 **ISaveable을 구현한 System을 자동 수집·등록**하고 세이브 파일이 있으면 로드한다.
- 저장 트리거: 종료 시 자동(`OnApplicationQuit`) + 수동 `SaveManager.Save()`.
- 파일: `persistentDataPath/save.json`.

### (참고) 콘텐츠 파이프라인 — 표준 경로
- **데이터 값 저작은 스프레드시트가 표준**: `Docs/GameData.xlsx` 편집(규칙은 그 안의 README 시트) → 메뉴 **`Tools/DesktopCompanion/GameData 변환 (xlsx → JSON)`** 1클릭 → `StreamingAssets/Data/GameData.json` 생성 → Play 시 자동 로드.
- JSON이 없으면 Resources의 SO 폴백(개발 편의). 외부 변환 툴 JSON(시트맵 포맷)도 그대로 로드 가능.
- 시트의 `#` 접두 열은 사람용 표시 전용(JSON 제외), `m_assetKey` 열은 에셋 키 오버라이드(§12).

> 이 규칙(특히 §9의 handle 기반 보관)만 지키면, Save/Load 도입은 **additive**이며 System 로직 재작성이 없다.

## 12. 비주얼 에셋 — Addressables 등록부터 화면 표시까지

모델·스프라이트 등 에셋은 Data에 직접 참조하지 않는다(JSON 채널·서버 목표 때문에 불가능). 대신 **키 규약 + Addressables**로 연결한다. 전체 흐름:

```
[아트] 에셋 임포트 → Addressable 체크 → 주소=규약 키 입력 → preload 라벨 부여
[기획] (필요 시) 시트 m_assetKey에 오버라이드 키 입력
[검증] Tools/DesktopCompanion/에셋 키 검증
[코드] View가 AssetKeys.Of(data, 용도) → AssetProvider.Get<T>(key)
```

### 12-1. 키 규약

**`{클래스명}_{ID}_{용도}`** — 예: `ItemData_Fish_100001_Icon`, `BattleFishData_500001_Model`, `StageData_600001_Background`

| 용도 접미 (`AssetUsage` 상수) | 의미 | 타입 |
|---|---|---|
| `Icon` | UI 아이콘 | Sprite |
| `Model` | 월드 프리팹 | GameObject |
| `Background` | 스테이지 배경 | Sprite 등 |

**오버라이드(`m_assetKey`)**: 여러 항목이 에셋을 공유하거나 예외일 때만 시트의 `m_assetKey` 열에 **키 베이스**를 입력. 예: 멸치살·새우살 둘 다 `Material_CommonFishMeat` 입력 → 최종 키 `Material_CommonFishMeat_Icon` 하나를 공유. 빈칸(기본)이면 규약 키 사용.

### 12-2. 에셋 등록 절차 (아트/등록 담당)

1. 에셋을 프로젝트에 임포트
2. 에셋 선택 → 인스펙터 상단 **`Addressable` 체크**
3. 주소(Address)를 **규약 키 그대로** 입력 (예: `ItemData_Fish_100001_Icon`)
4. `Window → Asset Management → Addressables → Groups`에서 해당 항목에 **`preload` 라벨 부여 — 필수!**
   - ⚠️ **라벨 없으면 로드되지 않는다.** 현재는 부팅 때 `preload` 라벨만 전량 로드하는 정책이라, 라벨 누락 = Play에서 `미탑재 에셋 키` 에러.
5. Group은 용도별(Icons/Models/Stages)로 나눠 정리 권장 — 로드 시점과 무관(빌드/배포 단위), 나중 최적화·서버 배포 때 이득.

### 12-3. 검증

메뉴 **`Tools/DesktopCompanion/에셋 키 검증`** — 전 데이터를 순회하며 타입별 필수 에셋(Fish=Icon+Model, Equipment/Materials/Consumables=Icon, BattleFish=Model, Stage=Background)의 키가 Addressables 주소에 존재하는지 검사, 누락 목록 리포트. **에셋 등록/데이터 추가 후 습관적으로 실행할 것.**

### 12-4. 코드에서 사용 (View 전용)

```csharp
using DesktopCompanion.Views;

// View(UI 슬롯 / WorldManager)에서 — data는 ViewModel/System 경유로 받은 GameData
string key = AssetKeys.Of(fishData, AssetUsage.Icon);   // "ItemData_Fish_100001_Icon"
Sprite icon = m_assetProvider.Get<Sprite>(key);          // 프리로드됐으므로 동기 반환
m_iconImage.sprite = icon;

// 안전 조회(없어도 에러 로그 없이)
if (m_assetProvider.TryGet(key, out Sprite sprite)) { ... }

// 월드 모델
GameObject prefab = m_assetProvider.Get<GameObject>(AssetKeys.Of(battleFishData, AssetUsage.Model));
```
- `AssetProvider`는 GameManager가 생성해 **UIManager/WorldManager에만 주입**된다. **System/Entity에서 사용 금지** — 에셋은 표현(View)의 관심사.
- 호출 시점: `OnBootCompleted` 이후(프리로드 완료 후)만 안전.
- (후속 예정) 에셋 총량이 커지면 스테이지 단위 라벨 로드/해제로 전환된다 — API는 유지되므로 View 코드는 그대로.

## 관련 파일
- 코어: `Assets/_Main/1_Scripts/0_Core/` (GameManager/DataManager/EntityManager/SystemManager + Json/)
- Data: `Assets/_Main/1_Scripts/1_Data/` (GameData + 계열별 Data/StatModifier)
- Entity: `Assets/_Main/1_Scripts/2_Entity/` (Entity/EntityHandle)
- System 베이스: `Assets/_Main/1_Scripts/3_System/` (ISystem/SystemBase)
- 세이브: `Assets/_Main/1_Scripts/5_Save/` (ISaveable/SaveManager/SaveModel)
- 뷰 에셋: `Assets/_Main/1_Scripts/6_View/` (AssetKeys/AssetProvider)
- 에디터 툴: `Assets/_Main/1_Scripts/Editor/GameDataConverter/` (변환기/에셋 키 검증)
- 데이터 저작: `Docs/GameData.xlsx` (규칙은 내부 README 시트)
- 설계 근거: [`Framework_Plan.md`](Framework_Plan.md)
