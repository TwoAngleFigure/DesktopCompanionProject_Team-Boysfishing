# 셰이더 API (DesktopCompanion)

이 폴더의 셰이더는 두 갈래다.

| 갈래 | 셰이더 | 문서 |
|---|---|---|
| 스폰/디스폰 **페이드** 3종 | `DitherFadeLit` / `DitherFadeLitSmooth` / `FadeLitAlpha` | 아래 1~6절 |
| 아쿠아리움 **물** | `AquariumWater` | [7절](#7-aquariumwater-아쿠아리움-물) |

---

# 페이드 셰이더

데스크톱 컴패니언은 씬을 **투명 RenderTexture(알파=데스크톱 투명도)** 로 렌더한다.
일반 Transparent 셰이더는 알파가 그대로 데스크톱을 뚫고(관통), Tripo 메시의 자기 겹침
정렬이 깨진다. 그래서 "완전 투명(0) → 불투명(1)" **전환(페이드)** 은 아래 3종의
전용 셰이더 + `DitherFade` 드라이버로 처리한다.

> 목적은 **반투명 유지**나 수중 표현이 아니라 **스폰/디스폰 시 페이드 전환**이다.

---

## 1. 셰이더 3종

| 셰이더 이름(`Shader.Find`) | 방식 | 큐 | 특징 | 권장 상황 |
|---|---|---|---|---|
| `DesktopCompanion/DitherFadeLit` | Bayer 4×4 디더 | Opaque / Geometry | 스크린도어, 그림자 페이드 지원 | 정렬 안정 최우선, 격자 패턴 허용 |
| `DesktopCompanion/DitherFadeLitSmooth` | IGN 디더 | Opaque / Geometry | 격자 덜 보임, 순수 ALU라 가장 경량 | **기본 권장** (매끄럽고 가벼움) |
| `DesktopCompanion/FadeLitAlpha` | 진짜 알파(프리멀티플라이드) | Transparent | 완전 매끄러운 알파, `ZWrite On`으로 자기 겹침 방지 | 픽셀 디더가 거슬릴 때 |

세 셰이더 모두 URP Lit 기반이며 **동일한 프로퍼티 세트**를 노출하므로,
`DitherFade` 드라이버 하나로 어느 것이든 구동할 수 있다.

### 공통: 데스크톱 무관통 원리
- 디더 2종: `clip(_Fade - threshold)`로 픽셀을 버리고, 그려지는 픽셀은 **알파=1**로 출력 → Opaque 유지.
- `FadeLitAlpha`: 프리멀티플라이드(`Blend One OneMinusSrcAlpha`) → 결과 알파 `= _Fade + dstA*(1-_Fade)`,
  배경이 불투명(알파 1)이면 결과 알파도 1 → 관통 없음.

---

## 2. 셰이더 프로퍼티

| 프로퍼티 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `_BaseMap` | 2D | white | 베이스(알베도) 텍스처 |
| `_BaseColor` | Color | (1,1,1,1) | 틴트 색 |
| **`_Fade`** | Range(0,1) | **1** | **0 = 완전 투명, 1 = 완전 불투명.** 페이드 구동 대상 |
| `_Metallic` | Range(0,1) | 0 | 메탈릭 |
| `_Smoothness` | Range(0,1) | 0.5 | 스무스니스 |
| `_Cull` | Enum(CullMode) | 2(Back) | 컬 모드 |

> ⚠️ `_Fade`는 **인스턴스 머티리얼**(`renderer.material.SetFloat("_Fade", v)`)에 직접 써야 한다.
> `MaterialPropertyBlock`은 SRP Batcher 호환 셰이더에서 나머지 `UnityPerMaterial` 프로퍼티
> (`_BaseColor` 등)를 0으로 만들어 모델이 검게 렌더된다.

---

## 3. `DitherFade` 컴포넌트

`DesktopCompanion.Views.DitherFade` — 세 셰이더 공통 `_Fade`를 **DOTween**으로 전환한다.
`Assets/_Member/2_PJW/1_Scripts/Display/DitherFade.cs`

### 인스펙터 필드
| 필드 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| Renderers | `Renderer[]` | (비움) | 대상 렌더러. 비우면 자식에서 자동 수집 |
| Duration | `float` | 0.4 | 인자 없는 `DOFade*`/자동 페이드의 기본 시간(초) |
| Ease | `Ease` | Linear | 페이드 이징 |
| Fade In On Enable | `bool` | true | 활성화 시 자동 0→1 페이드 인 |

### 프로퍼티 (읽기)
| 멤버 | 타입 | 설명 |
|---|---|---|
| `Fade` | `float` | 현재 `_Fade` 값(0~1) |
| `Duration` | `float` | 기본 페이드 시간 |

### 메서드
| 시그니처 | 반환 | 설명 |
|---|---|---|
| `DOFade(float end, float dur)` | `Tween` | `_Fade`를 end(0~1)로 전환. **Sequence 조합용** |
| `DOFade(float end)` | `Tween` | 기본 시간으로 전환 |
| `DOFadeIn(float dur)` / `DOFadeIn()` | `Tween` | 0→1 페이드 인 |
| `DOFadeOut(float dur)` / `DOFadeOut()` | `Tween` | 1→0 페이드 아웃 |
| `SetFade(float v)` | `void` | 전환 없이 즉시 적용(내부 setter 겸용) |
| `SetFadeImmediate(float v)` | `void` | 진행 중 트윈 Kill 후 즉시 적용 |

모든 `DOFade*` 트윈은 `.SetTarget(this)`(→ `DOTween.Kill(fade)`로 일괄 제어)와
`.SetLink(gameObject)`(→ 오브젝트 파괴 시 자동 Kill)가 적용되어 있다.

---

## 4. 사용 예

### 4-1. 즉시 값 설정
```csharp
using DesktopCompanion.Views;

var fade = obj.GetComponent<DitherFade>();
fade.SetFade(0.5f);          // 즉시 반투명(디더/알파에 따라 시각화)
fade.SetFadeImmediate(0f);   // 진행 중 페이드 취소하고 즉시 투명
```

### 4-2. 단발 페이드
```csharp
fade.DOFadeIn(0.4f);   // 0.4초 동안 페이드 인 (자동 재생)
fade.DOFadeOut();      // 기본 시간으로 페이드 아웃
```

### 4-3. DOTween Sequence 조합
```csharp
using DG.Tweening;

// 스폰: 페이드 인 → 1초 유지 → 페이드 아웃 → 파괴
DOTween.Sequence()
    .Append(fade.DOFadeIn(0.4f))
    .AppendInterval(1f)
    .Append(fade.DOFadeOut(0.4f))
    .OnComplete(() => Destroy(obj));

// 여러 오브젝트 동시 페이드(Join)
DOTween.Sequence()
    .Append(fishFade.DOFadeIn())
    .Join(boatFade.DOFadeIn());
```

### 4-4. 콜백/체이닝
```csharp
fade.DOFadeOut(0.3f)
    .SetEase(Ease.InQuad)      // 인스펙터 Ease를 개별 덮어쓰기
    .OnComplete(() => pool.Release(obj));
```

---

## 5. 주의점

1. **겹치는 페이드**: `DOFade*`는 내부에서 `Kill`하지 않는다(Sequence에 Append된 트윈을
   깨지 않기 위함). 즉시 중단이 필요하면 `SetFadeImmediate()` 또는 `DOTween.Kill(fade)`.
   단, 자동 페이드 인(OnEnable)은 재활성화 누적을 막으려 시작 전 `DOTween.Kill(this)`를 호출한다.
2. **DOTween 초기화**: 첫 사용 시 경고가 뜨면 `Tools > Demigiant > DOTween Utility Panel`에서
   Setup 하거나 부팅 시 `DOTween.Init()`를 한 번 호출한다.
3. **테스트**: `FadeSliderStub` 컴포넌트로 uGUI 슬라이더를 통해 `_Fade`를 실시간 확인할 수 있다
   (오버레이 빌드에서 만지려면 전체화면 UI 캔버스 하위의 uGUI Slider여야 함 — 클릭관통이
   GraphicRaycaster로 감지).

---

## 6. 관련 파일
- 셰이더: `4_Shader/DitherFadeLit.shader`, `DitherFadeLitSmooth.shader`, `FadeLitAlpha.shader`
- 드라이버: `1_Scripts/Display/DitherFade.cs`
- 테스트 스텁: `1_Scripts/Display/FadeSliderStub.cs`
- 설계 배경: `Docs/Plan/07_OpaqueTransparencyShader.md` (개인 폴더, gitignore)

---

# 7. AquariumWater (아쿠아리움 물)

`DesktopCompanion/AquariumWater` — 아쿠아리움의 SW3(Stylized Water 3)를 대체하는 자체 물 셰이더.
설계 배경: `Docs/Plan/32_AquariumWaterShader.md` (개인 폴더, gitignore)

## 7-1. 무엇이 다른가

- **URP 라이트를 전혀 읽지 않는다.** `Lighting.hlsl`도, 라이팅 pragma도 없다. 조명은 `AquariumWaterLight`가
  주입하는 전역 2개(`_AQW_LightDir` / `_AQW_LightColor`)뿐이다. → **태양(SunSource) 회전·데이나이트와 무관**하게
  같은 뷰를 유지한다. SW3는 메인 라이트만 계산해서 이게 불가능했고, 그걸 우회하려 탱크를 회전시킨 것이
  노말맵 늘어남의 원인이었다.
- **노말 UV가 오브젝트 공간 삼평면(triplanar)** 이다. 월드 XZ 투영이 아니므로 오브젝트가 기울어도 무늬가
  늘어나지 않고, **구·임의 볼록 메시**에도 그대로 쓸 수 있다.
- **물속 색이 "유리면 → 뒤 물체 거리"** 로 결정된다. 가까울수록 원래 색, 멀수록 `Deep Color`.

## 7-2. 사용 규칙 (중요)

| 규칙 | 이유 |
|---|---|
| 6면체는 **평면 6장**에 같은 머티리얼을 붙인다 | 면마다 머티리얼을 나눌 필요가 없다. 수면/유리는 셰이더가 법선으로 자동 판별한다 |
| 평면 법선은 **탱크 바깥**을 향해야 한다 | `Cull Back`이라 카메라를 향한 근면만 그려진다. 뒤집히면 면이 통째로 사라진다 |
| **볼록 형상만** 지원한다 | 오목 형상은 근면이 겹쳐 이중 합성된다 |
| 씬에 `AquariumWaterLight`가 **1개** 있어야 한다 | 없으면 `Fallback Light Dir`로 폴백해 그림은 나오지만 각도 조절이 안 된다 |
| URP Asset의 **Depth Texture / Opaque Texture가 켜져 있어야** 한다 | 둘 다 샘플한다. 프로젝트는 `PC_RPAsset`에서 이미 ON |
| 렌더러의 **SSAO를 끄면 안 된다** | 깊이 프리패스가 사라지면 `_CameraDepthTexture`가 컬러 포맷이 되어 감쇠가 깨진다([[BF Pixelizer]]와 공유하는 제약) |

## 7-3. 머티리얼 프로퍼티

**Underwater**

| 프로퍼티 | 기본값 | 설명 |
|---|---|---|
| `Deep Color` | (0.05, 0.35, 0.55) | 완전히 감쇠했을 때의 색 |
| `Max Distance` | 6 | **완전히 심해색이 되는 거리**(월드 단위). 뒤에 물체가 없으면 이 값으로 클램프된다 |
| `Density` | 1 | 감쇠 곡선의 휨. 클수록 초반에 빨리 어두워진다 |

**Water Lighting** — 심해색이 조명에 반응하게 한다

| 프로퍼티 | 기본값 | 설명 |
|---|---|---|
| `Diffuse` | **off** | 끄면 심해색이 상수(라이트를 돌려도 물 톤 불변). 켜면 아래 두 값으로 변조된다 |
| `Ambient` | 0.3 | 라이트가 하나도 없을 때의 바닥 밝기. 물이 완전히 검어지는 것을 막는다 |
| `Diffuse Strength` | 0.7 | 라이트 기여분의 배율 |

기본값 `0.3 + 0.7 = 1.0`은 **머리 위 흰색 디렉셔널(강도 1) 기준으로 토글 off일 때와 같은 밝기**가
되도록 맞춘 것이다. 토글을 켜도 갑자기 밝아지지 않고, 라이트를 수평선 쪽으로 눕히면 0.3까지 어두워진다.

> **기준이 면 법선이 아니라 월드 up이다.** 면 법선을 쓰면 위에서 비추는 라이트에 대해 옆면이
> `dot ≈ 0`이라 새까매지고 6면이 따로 논다. 물은 면이 아니라 부피이므로 "빛이 수면을 통해 얼마나
> 내려오는가"로 보는 것이 맞고, 그래야 6면이 같은 톤을 유지한다.
>
> 조명은 **물 자신의 색에만** 곱한다. `sceneColor`(물고기·바닥)는 이미 URP로 조명된 결과라
> 여기서 또 곱하면 이중 조명이 된다.

**Normals** — SW3 Normals 탭과 1:1 대응

| 프로퍼티 | 기본값 | 설명 |
|---|---|---|
| `Enable` | on | 끄면 기하 법선만 쓴다(굴절·스펙큘러가 평평해짐) |
| `Normal Map` | bump | |
| `Tiling` | (0.5, 0.5) | **월드 1m당 반복 수.** 면마다 localScale이 달라도 밀도가 같아진다 |
| `  Sub-layer (multiplier)` | 0.5 | 2번째 샘플의 타일링 배수 |
| `Speed` | 0.4 | 음수면 역방향 |
| `  Sub-layer (multiplier)` | -0.25 | 2번째 샘플의 속도 배수 |
| `Strength` | 0.5 | 기하 법선과의 lerp |
| `Animation Speed` | 1 | 전체 흐름 속도 배수 |
| `Direction` | (1, 0) | 투영 평면 내 흐름 방향 |
| `Triplanar Sharpness` | 8 | 투영 전환의 날카로움 |
| `Distance Normals` | off | 멀리서 타일링 반복이 보일 때 켠다 |
| `  Large Normal Map` / `  Tiling` / `  Blend Range` | bump / 0.05 / (5, 30) | 대형 노말과 블렌드할 카메라 거리 범위 |

**Surface / Refraction / Caustics**

| 프로퍼티 | 기본값 | 설명 |
|---|---|---|
| `Specular` | on | 수면 하이라이트 |
| `Surface Angle Threshold` | 0.5 | 이 이상 위를 향한 면을 수면으로 본다(`dot(N, up)`) |
| `Surface Blend Band` | 0.2 | 전환 폭. 구형에서 위쪽 캡이 부드럽게 수면이 된다 |
| `Highlight Size` | 64 | 하이라이트 로브의 좁기. **모든 광원 공통.** 값이 크면 매우 좁아 각도가 안 맞으면 안 보인다 |
| `Highlight Strength (메인 디렉셔널)` | 1 | |
| `Highlight Strength (Point / Spot)` | 1 | 추가 라이트 전용 배율 |
| `Fallback Light Dir` | (-0.3, -0.8, -0.5) | `AquariumWaterLight`가 없을 때만 쓰는 폴백 |
| `Refraction` / `Strength` | on / 1 | 노말로 뒤 화면 UV를 민다 |
| `Caustics` / `Map` / `Tiling` / `Speed` / `Brightness` / `Chromance` | on / black / 0.1 / 0.1 / 2 / 1 | 뒤 물체 위에 투사. `Chromance = 0`이면 샘플이 6탭 → 2탭으로 줄어든다 |

## 7-4. `AquariumWaterLight` 컴포넌트

`DesktopCompanion.Views.AquariumWaterLight` — `1_Scripts/Aquarium/World/AquariumWaterLight.cs`

> **물에 영향을 주는 라이트는 오직 이 컴포넌트에 연결된 것뿐이다.** 씬에 디렉셔널 라이트를 아무리
> 추가해도 여기 연결하지 않으면 물은 반응하지 않는다. 셰이더가 URP 라이팅을 아예 읽지 않기 때문이며,
> 이것이 태양 독립의 대가다. **"라이트를 만들었는데 반응이 없다"의 원인은 거의 항상 이것이다.**

| 필드 | 설명 |
|---|---|
| **메인 (디렉셔널)** | |
| `Light` | 기준 디렉셔널 라이트. **비워두면 이 오브젝트의 Transform 방향**을 쓴다. **방향만** 사용하므로 Point/Spot을 넣으면 경고가 뜬다 |
| `Override Color` | 라이트의 색·강도를 무시하고 아래 값을 쓴다 |
| `Color` / `Intensity` | 위가 켜졌거나 `Light`가 비었을 때 쓰이는 값 |
| **추가 (Point / Spot / Directional)** | |
| `Additional Lights` | 수면 하이라이트에 참여할 라이트 배열. **최대 4개**. Point·Spot·Directional 전부 가능 |

- `[ExecuteAlways]`라 **에디트 모드에서도** 라이트를 돌리면 물이 즉시 반응한다.
- 라이트가 움직이거나 밝기가 바뀔 수 있으므로 **매 프레임 주입**한다(전부 CPU 상수 갱신이라 저렴).
- 최대 개수는 셰이더의 `AQW_MAX_ADD_LIGHTS`와 C#의 `MaxAdditionalLights` **둘 다** 고쳐야 늘어난다.

### 라이트 켜고 끄기 — `Light.enabled`가 아니라 GameObject로

| 상태 | 물 | 물고기·바닥(URP 수광) |
|---|---|---|
| GameObject 활성 + `Light` 컴포넌트 **켬** | 영향 O | 영향 O |
| GameObject 활성 + `Light` 컴포넌트 **끔** | **영향 O** | 영향 X |
| GameObject **비활성** | 영향 X | 영향 X |

이 컴포넌트는 `Light.enabled`를 보지 않고 Transform·색만 읽는다. 따라서 **`Light` 컴포넌트를 꺼두면
"물에만 영향을 주는 라이트"** 가 된다(URP 라이팅에 참여하지 않으므로 바다·일반 오브젝트에도 무영향).
완전히 끄려면 **GameObject를 비활성**한다.

### Point / Spot 감쇠

거리·콘 감쇠는 URP의 `DistanceAttenuation`/`AngleAttenuation`과 **같은 식**을 쓴다(`Range`,
`Spot Angle`, `Inner Spot Angle` 그대로 반영). 물고기 쪽 조명과 감이 맞게 하기 위함이다.

모든 라이트(메인·추가)는 **두 항**에 기여한다.

| 항 | 토글 | 적용 면 | 효과 |
|---|---|---|---|
| 수면 스펙큘러 | `Specular` (기본 on) | 윗면(`surfaceMask > 0`) | 램프가 수면에 맺히는 반짝임 |
| 확산 | `Diffuse` (기본 **off**) | 전 면 | 물 전체의 톤이 밝아지고 어두워짐 |

`Diffuse`를 끈 상태에서는 라이트를 돌려도 **물 전체의 밝기가 원리적으로 변하지 않는다.** 윗면의
좁은 하이라이트만 움직이므로 "라이트가 안 먹는다"고 느끼기 쉽다. 물 톤을 조명에 연동하려면
`Diffuse`를 켜야 한다.

### 하이라이트가 안 보일 때

이 프로젝트는 **LDR**이다 — URP Asset이 `m_SupportsHDR: 0`이고 아쿠아리움 RT도 `ARGB32`(8비트).
가산 스펙큘러는 흰색에서 클램프되므로 **`Highlight Strength`를 올려도 어느 지점부터는 변화가 없다.**
색 프로퍼티에 `[HDR]`을 붙여도 같은 이유로 클램프되어 의미가 없다.

더 눈에 띄게 하려면 강도가 아니라 **`Highlight Size`를 낮춰 로브를 넓힌다**(64 → 16 정도).

> ⚠️ 셰이더 쪽에서 `_AQW_LightDir` / `_AQW_LightColor`는 **`UnityPerMaterial` CBUFFER 밖**에 선언되어 있다.
> 안으로 옮기면 SRP Batcher가 머티리얼 상수로 덮어써 전역 주입이 조용히 무시된다.

## 7-5. 관련 파일
- 셰이더: `4_Shader/AquariumWater.shader`, `4_Shader/AquariumWaterCommon.hlsl`
- 조명 주입: `1_Scripts/Aquarium/World/AquariumWaterLight.cs`
