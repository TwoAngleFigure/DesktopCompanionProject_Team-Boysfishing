// 아쿠아리움 전용 물 셰이더의 공용 선언·유틸.
// 설계: Docs/Plan/32_AquariumWaterShader.md
//
// 핵심 3가지
//  1) 깊이 복원은 ComputeWorldSpacePosition + UNITY_MATRIX_I_VP로 한다.
//     LinearEyeDepth는 원근 전용이라 직교(orthographic) 아쿠아리움 카메라에서 틀린 값을 준다.
//  2) 노말 UV는 오브젝트 공간 삼평면(triplanar). 월드 XZ 투영이 아니므로 오브젝트가 회전해도
//     투영이 무너지지 않고(SW3의 늘어남 원인), 구형·임의 볼록 메시에도 그대로 적용된다.
//  3) 전역 주입 변수는 UnityPerMaterial CBUFFER '밖'에 둔다. 안에 넣으면 SRP Batcher가
//     머티리얼 상수로 덮어써 Shader.SetGlobalVector가 무시된다.
#ifndef AQUARIUM_WATER_COMMON_INCLUDED
#define AQUARIUM_WATER_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl" // BlendNormal

// ─────────────────────────────────────────────────────────────────────────────
// 전역 주입 (AquariumWaterLight.cs) — ⚠ CBUFFER 밖
// ─────────────────────────────────────────────────────────────────────────────
#define AQW_MAX_ADD_LIGHTS 4   // ※ AquariumWaterLight.MaxAdditionalLights와 반드시 같아야 한다.

float4 _AQW_LightDir;      // 메인(디렉셔널). xyz = 월드 방향(라이트 → 표면). 영벡터면 머티리얼 폴백.
float4 _AQW_LightColor;    // rgb = 색 × 강도

// 추가 라이트(Point/Spot/추가 Directional). URP의 추가 라이트 상수 구성을 그대로 따른다.
float  _AQW_AddLightCount;
float4 _AQW_AddLightPos[AQW_MAX_ADD_LIGHTS];      // xyz = 위치(w=1) 또는 라이트를 향한 방향(w=0)
float4 _AQW_AddLightColor[AQW_MAX_ADD_LIGHTS];    // rgb = 색 × 강도
float4 _AQW_AddLightAtten[AQW_MAX_ADD_LIGHTS];    // x = 1/range², zw = 스팟 콘 (scale, offset)
float4 _AQW_AddLightSpotDir[AQW_MAX_ADD_LIGHTS];  // xyz = -light.forward

CBUFFER_START(UnityPerMaterial)
    float4 _AQW_DeepColor;
    float  _AQW_MaxDistance;
    float  _AQW_Density;

    float4 _AQW_NormalTiling;
    float  _AQW_NormalSubTiling;
    float  _AQW_NormalSpeed;
    float  _AQW_NormalSubSpeed;
    float  _AQW_NormalStrength;
    float  _AQW_AnimationSpeed;
    float4 _AQW_ScrollDir;
    float  _AQW_TriplanarSharpness;
    float  _AQW_DistanceNormalsTiling;
    float4 _AQW_DistanceNormalsFadeDist;   // xy = (min, max)

    float  _AQW_AmbientStrength;
    float  _AQW_DiffuseStrength;

    float  _AQW_SurfaceAngle;
    float  _AQW_SurfaceBand;
    float  _AQW_SunReflectionSize;
    float  _AQW_SunReflectionStrength;
    float  _AQW_PointSpotReflectionStrength;
    float4 _AQW_FallbackLightDir;

    float  _AQW_RefractionStrength;

    float  _AQW_CausticsTiling;
    float  _AQW_CausticsSpeed;
    float  _AQW_CausticsBrightness;
    float  _AQW_CausticsChromance;
CBUFFER_END

TEXTURE2D(_AQW_BumpMap);      SAMPLER(sampler_AQW_BumpMap);
TEXTURE2D(_AQW_BumpMapLarge); // 샘플러는 _AQW_BumpMap 것을 공유한다.
TEXTURE2D(_AQW_CausticsMap);  SAMPLER(sampler_AQW_CausticsMap);

// ─────────────────────────────────────────────────────────────────────────────
// 조명
// ─────────────────────────────────────────────────────────────────────────────

/// 물 조명 방향(월드, 라이트 → 표면). 전역 미주입 씬(프리뷰·테스트)에서도 그림이 나오도록 폴백한다.
float3 GetWaterLightDir()
{
    float3 g = _AQW_LightDir.xyz;
    return normalize(dot(g, g) > 1e-6 ? g : _AQW_FallbackLightDir.xyz);
}

half3 GetWaterLightColor()
{
    float3 g = _AQW_LightDir.xyz;
    return dot(g, g) > 1e-6 ? (half3)_AQW_LightColor.rgb : half3(1.0h, 1.0h, 1.0h);
}

/// 거리 감쇠. URP의 DistanceAttenuation과 같은 식이라 물고기(URP 수광)와 감이 맞는다.
half WaterDistanceAttenuation(float distanceSqr, half oneOverRangeSqr)
{
    float lightAtten   = rcp(distanceSqr);
    half  factor       = (half)(distanceSqr * oneOverRangeSqr);
    half  smoothFactor = saturate(1.0h - factor * factor);
    return (half)lightAtten * (smoothFactor * smoothFactor);
}

/// 스팟 콘 감쇠. spotDir = -light.forward, lightDir = 표면 → 라이트 방향.
half WaterAngleAttenuation(half3 spotDir, half3 lightDir, half2 spotAtten)
{
    half atten = saturate(dot(spotDir, lightDir) * spotAtten.x + spotAtten.y);
    return atten * atten;
}

/// 수면 스펙큘러 1개 광원분(Blinn-Phong). L = 표면 → 라이트 방향.
half3 WaterSpecular(float3 normalWS, float3 viewDirWS, float3 lightDirWS, half3 lightColor,
                    half attenuation, float strength)
{
    half s = (half)pow(saturate(dot(normalWS, normalize(lightDirWS + viewDirWS))), _AQW_SunReflectionSize);
    return lightColor * (s * strength * attenuation);
}

/// i번째 추가 라이트의 방향과 감쇠를 푼다. 스펙큘러·확산 루프가 공유한다.
void ResolveAdditionalLight(int i, float3 positionWS, out float3 lightDirWS, out half attenuation)
{
    float4 posType = _AQW_AddLightPos[i];

    // w=1이면 위치(Point/Spot), w=0이면 라이트를 향한 방향(Directional) — URP와 같은 규약.
    float3 toLight     = posType.xyz - positionWS * posType.w;
    float  distanceSqr = max(dot(toLight, toLight), 1e-6);
    lightDirWS = toLight * rsqrt(distanceSqr);

    attenuation = 1.0h;
    UNITY_BRANCH
    if (posType.w > 0.5)
    {
        half4 atten  = (half4)_AQW_AddLightAtten[i];
        attenuation  = WaterDistanceAttenuation(distanceSqr, atten.x);
        attenuation *= WaterAngleAttenuation((half3)_AQW_AddLightSpotDir[i].xyz, (half3)lightDirWS, atten.zw);
    }
}

/// 추가 라이트(Point/Spot/추가 Directional) 전체의 스펙큘러 합.
half3 AdditionalWaterSpecular(float3 positionWS, float3 normalWS, float3 viewDirWS)
{
    half3 sum = half3(0.0h, 0.0h, 0.0h);

    int count = min((int)_AQW_AddLightCount, AQW_MAX_ADD_LIGHTS);
    for (int i = 0; i < count; i++)
    {
        float3 lightDirWS;
        half   attenuation;
        ResolveAdditionalLight(i, positionWS, lightDirWS, attenuation);

        sum += WaterSpecular(normalWS, viewDirWS, lightDirWS, (half3)_AQW_AddLightColor[i].rgb,
                             attenuation, _AQW_PointSpotReflectionStrength);
    }

    return sum;
}

/// 물 부피에 도달하는 빛의 양(확산 + 앰비언트). 심해색에 곱해 쓴다.
///
/// ★ 면 법선이 아니라 '월드 up'을 기준으로 삼는다. 면 법선을 쓰면 위에서 비추는 라이트에 대해
///   옆면이 dot≈0이 되어 새까매지고 6면이 따로 논다. 물은 면이 아니라 부피라서, "빛이 수면을
///   통해 얼마나 내려오는가"로 보는 것이 맞고 그래야 6면이 같은 톤을 갖는다.
///
/// 앰비언트는 바닥값이라 확산 배율의 영향을 받지 않는다(라이트가 없어도 물이 완전히 검어지지 않도록).
half3 WaterLightAmount(float3 positionWS)
{
    const float3 up = float3(0.0, 1.0, 0.0);

    // 메인 디렉셔널. _AQW_LightDir은 라이트 → 표면 방향이라 뒤집는다.
    half3 lit = GetWaterLightColor() * (half)saturate(dot(up, -GetWaterLightDir()));

    int count = min((int)_AQW_AddLightCount, AQW_MAX_ADD_LIGHTS);
    for (int i = 0; i < count; i++)
    {
        float3 lightDirWS;
        half   attenuation;
        ResolveAdditionalLight(i, positionWS, lightDirWS, attenuation);

        lit += (half3)_AQW_AddLightColor[i].rgb * ((half)saturate(dot(up, lightDirWS)) * attenuation);
    }

    return (half3)_AQW_AmbientStrength + lit * (half)_AQW_DiffuseStrength;
}

// ─────────────────────────────────────────────────────────────────────────────
// 깊이·거리 (원근/직교 공통)
// ─────────────────────────────────────────────────────────────────────────────

/// 카메라 전방축에 투영한 깊이. 직교에서도 올바르다(단순 거리는 직교에서 시선축 깊이가 아니다).
float WaterEyeDepth(float3 positionWS)
{
    return dot(positionWS - GetCameraPositionWS(), GetViewForwardDir());
}

/// 화면 UV의 씬 깊이를 월드 좌표로 복원한다. 원근·직교 모두 무분기로 정확하다.
/// hasSceneObject = 그 픽셀에 실제 불투명 물체가 있는가(false면 배경/원평면).
float3 SceneWorldPos(float2 screenUV, out bool hasSceneObject)
{
    float rawDepth = SampleSceneDepth(screenUV);

    float deviceDepth = rawDepth;
#if UNITY_REVERSED_Z
    hasSceneObject = rawDepth > 1e-6;
#else
    hasSceneObject = rawDepth < 1.0 - 1e-6;
    // 비-reversed-Z 플랫폼은 클립 z가 [-1,1]인데 깊이 버퍼는 [0,1]이라 리맵이 필요하다
    // (URP ScreenSpaceShadows.shader와 같은 처리).
    deviceDepth = rawDepth * 2.0 - 1.0;
#endif

    // screenUV는 GetNormalizedScreenSpaceUV가 준 Y-up NDC이고, ComputeClipSpacePosition이
    // 기대하는 공간과 같다. 깊이 텍스처 샘플링 UV와 동일한 값을 넘겨야 한다는 URP 규약.
    return ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
}

/// 유리면 → 뒤 물체 거리 기반 지수 감쇠.
/// _AQW_MaxDistance에서 정확히 1이 되도록 정규화해, MaxDistance = "완전 심해색이 되는 거리",
/// Density = "곡선의 휨"이라는 독립적인 의미를 갖게 한다.
half WaterFog(float3 positionWS, float3 sceneWS)
{
    float d = min(max(0.0, length(sceneWS - positionWS)), _AQW_MaxDistance);
    float f = 1.0 - exp(-_AQW_Density * d);
    float n = 1.0 - exp(-_AQW_Density * _AQW_MaxDistance);
    return (half)saturate(f / max(1e-4, n));
}

// ─────────────────────────────────────────────────────────────────────────────
// 노말 — 오브젝트공간 삼평면
// ─────────────────────────────────────────────────────────────────────────────

/// 오브젝트 → 월드 스케일(lossyScale). 오브젝트 공간 좌표에 곱해 '월드 단위' 로컬 좌표를 만든다.
/// 탱크 6면은 각자 다른 localScale(예: 0.1, 5, 0.1)을 갖기 때문에, 순수 오브젝트 좌표를 쓰면
/// 면마다 타일링이 달라지고 무늬가 늘어난다. 스케일을 되돌리면 타일링이 "월드 1m당 반복 수"가 되어
/// 6면이 자동으로 같은 밀도를 갖는다.
float3 GetObjectLossyScale()
{
    float4x4 m = GetObjectToWorldMatrix();
    return float3(length(float3(m._m00, m._m10, m._m20)),
                  length(float3(m._m01, m._m11, m._m21)),
                  length(float3(m._m02, m._m12, m._m22)));
}

/// 한 투영 평면에서 2회 샘플 + 블렌드. SW3 Normals 탭의 main/sub-layer 구조에 대응한다.
half3 SampleWaterNormalPlane(float2 uv, float camDist)
{
    float2 flow    = _Time.y * _AQW_AnimationSpeed * _AQW_ScrollDir.xy;
    float2 tiling  = _AQW_NormalTiling.xy;
    float2 tiling2 = tiling * _AQW_NormalSubTiling;

    float2 uv1 = uv * tiling  + flow * _AQW_NormalSpeed;
    float2 uv2 = uv * tiling2 + flow * (_AQW_NormalSpeed * _AQW_NormalSubSpeed);

    half3 n = BlendNormal(
        UnpackNormal(SAMPLE_TEXTURE2D(_AQW_BumpMap, sampler_AQW_BumpMap, uv1)),
        UnpackNormal(SAMPLE_TEXTURE2D(_AQW_BumpMap, sampler_AQW_BumpMap, uv2)));

#if defined(_AQW_DISTANCE_NORMALS)
    // 카메라가 멀수록 대형 노말로 넘겨 타일링 반복 아티팩트를 줄인다.
    float2 lt   = uv * _AQW_DistanceNormalsTiling;
    float2 luv1 = lt       + flow * (_AQW_NormalSpeed * 2.0);
    float2 luv2 = lt * 2.0 + flow * (_AQW_NormalSpeed * -0.9);

    half3 ln = BlendNormal(
        UnpackNormal(SAMPLE_TEXTURE2D(_AQW_BumpMapLarge, sampler_AQW_BumpMap, luv1)),
        UnpackNormal(SAMPLE_TEXTURE2D(_AQW_BumpMapLarge, sampler_AQW_BumpMap, luv2)));

    half fade = (half)saturate((_AQW_DistanceNormalsFadeDist.y - camDist)
              / max(1e-4, _AQW_DistanceNormalsFadeDist.y - _AQW_DistanceNormalsFadeDist.x));
    n = lerp(ln, n, fade);
#endif

    return n;
}

/// 오브젝트공간 삼평면 노말(whiteout 블렌드). 결과는 오브젝트 공간 노말이다.
/// 평면에서는 가중치가 one-hot이라 실제 샘플이 1투영(2탭)뿐이고, 구형에서만 최대 3투영으로 늘어난다.
/// (가중치 ~0인 투영을 건너뛰므로 경계에서 밉 선택이 부정확할 수 있으나, 기여도가 0에 수렴해 무시 가능)
half3 TriplanarNormalOS(float3 posOS, float3 nOS, float camDist)
{
    float3 p = posOS * GetObjectLossyScale();

    float3 w = pow(abs(nOS), _AQW_TriplanarSharpness);
    w /= max(1e-4, w.x + w.y + w.z);

    half3 nX = half3(0.0h, 0.0h, 1.0h);
    half3 nY = nX;
    half3 nZ = nX;

    UNITY_BRANCH if (w.x > 0.001) nX = SampleWaterNormalPlane(p.zy, camDist);
    UNITY_BRANCH if (w.y > 0.001) nY = SampleWaterNormalPlane(p.xz, camDist);
    UNITY_BRANCH if (w.z > 0.001) nZ = SampleWaterNormalPlane(p.xy, camDist);

    // whiteout 블렌드: 투영별 탄젠트 노말을 오브젝트 공간 축으로 재배치해 합성한다.
    half3 blended = normalize(
          half3(nX.xy + nOS.zy, abs(nX.z) * nOS.x).zyx * w.x
        + half3(nY.xy + nOS.xz, abs(nY.z) * nOS.y).xzy * w.y
        + half3(nZ.xy + nOS.xy, abs(nZ.z) * nOS.z).xyz * w.z);

    return normalize(lerp(nOS, blended, _AQW_NormalStrength));
}

// ─────────────────────────────────────────────────────────────────────────────
// 코스틱
// ─────────────────────────────────────────────────────────────────────────────

/// 반대 방향으로 흐르는 2개 레이어의 min → 망 무늬. 단일 채널.
half CausticsLayerMin(float2 uv1, float2 uv2)
{
    half a = SAMPLE_TEXTURE2D(_AQW_CausticsMap, sampler_AQW_CausticsMap, uv1).r;
    half b = SAMPLE_TEXTURE2D(_AQW_CausticsMap, sampler_AQW_CausticsMap, uv2).r;
    return min(a, b);
}

/// 뒤 물체(sceneWS)의 월드 XZ에 투영한 코스틱.
/// 월드 공간을 쓰는 이유: 6면이 각자 다른 오브젝트 트랜스폼을 갖기 때문에 오브젝트 공간으로 투영하면
/// 같은 지점의 무늬가 보는 면마다 달라진다. 코스틱은 빛이 아래로 드리우는 현상이라 월드 기준이 맞다.
half3 SampleCaustics(float3 sceneWS)
{
    float2 flow = _Time.y * _AQW_AnimationSpeed * _AQW_CausticsSpeed;
    float2 uv   = sceneWS.xz * _AQW_CausticsTiling;
    float2 uv1  =  uv + flow;
    float2 uv2  = -uv + flow * 0.8;

    UNITY_BRANCH
    if (_AQW_CausticsChromance > 0.001)
    {
        float2 o = float2(_AQW_CausticsChromance * 0.01, 0.0);
        half r = CausticsLayerMin(uv1 + o, uv2 + o);
        half g = CausticsLayerMin(uv1,     uv2);
        half b = CausticsLayerMin(uv1 - o, uv2 - o);
        return half3(r, g, b) * _AQW_CausticsBrightness;
    }

    half c = CausticsLayerMin(uv1, uv2);
    return half3(c, c, c) * _AQW_CausticsBrightness;
}

#endif // AQUARIUM_WATER_COMMON_INCLUDED
