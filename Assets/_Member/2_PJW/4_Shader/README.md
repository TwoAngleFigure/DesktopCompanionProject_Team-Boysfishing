# 페이드 셰이더 API (DesktopCompanion)

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
