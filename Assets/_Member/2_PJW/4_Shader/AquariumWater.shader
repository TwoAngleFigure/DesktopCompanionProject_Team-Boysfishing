// 아쿠아리움 전용 물 셰이더. SW3(Stylized Water 3)를 대체한다.
// 설계·근거: Docs/Plan/32_AquariumWaterShader.md
//
//  · URP 라이트를 전혀 읽지 않는다(Lighting.hlsl·라이팅 pragma 없음) → 태양(SunSource) 회전과 무관.
//    조명은 AquariumWaterLight.cs가 주입하는 전역 _AQW_LightDir/_AQW_LightColor만 쓴다.
//  · 물속 색 = 유리면에서 뒤 물체까지의 거리로 지수 감쇠. 뒤가 비면 _AQW_MaxDistance로 클램프.
//  · 6면체 = 같은 머티리얼을 붙인 평면 6장. Cull Back이라 카메라를 향한 근면만 그려지므로,
//    카메라가 탱크 주위를 궤도 회전해도 어느 면이 '유리'인지가 자동으로 바뀐다(볼록 형상 전제).
//    ※ 평면의 법선은 반드시 탱크 '바깥'을 향해야 한다.
//  · 수면/유리 구분은 면 법선의 up 성분으로 자동 판별하며, 스펙큘러 가중치로만 쓴다.
//    감쇠·굴절·코스틱은 전 면 공통이다(윗면으로 봐도 물을 통과해 보는 것은 같으므로).
//  · Blend Off + 알파 1 → 탱크 내부가 불투명하게 채워져 데스크톱 관통이 없다.
Shader "DesktopCompanion/AquariumWater"
{
    Properties
    {
        [Header(Underwater)]
        [Space(4)]
        _AQW_DeepColor   ("Deep Color", Color) = (0.05, 0.35, 0.55, 1)
        _AQW_MaxDistance ("Max Distance (완전 심해색 거리)", Float) = 6
        _AQW_Density     ("Density (감쇠 곡선)", Range(0.01, 5)) = 1

        [Header(Water Lighting)]
        [Space(4)]
        [Toggle(_AQW_DIFFUSE)] _AQW_DiffuseOn ("Diffuse (심해색이 조명에 반응)", Float) = 0
        _AQW_AmbientStrength ("Ambient (라이트 없을 때의 바닥 밝기)", Range(0, 2)) = 0.3
        _AQW_DiffuseStrength ("Diffuse Strength", Range(0, 4)) = 0.7

        [Header(Normals)]
        [Space(4)]
        [Toggle(_AQW_NORMALMAP)] _AQW_NormalMapOn ("Enable", Float) = 1
        [NoScaleOffset] _AQW_BumpMap ("Normal Map", 2D) = "bump" {}
        _AQW_NormalTiling    ("Tiling (월드 1m당 반복)", Vector) = (0.5, 0.5, 0, 0)
        _AQW_NormalSubTiling ("    Sub-layer (multiplier)", Float) = 0.5
        _AQW_NormalSpeed     ("Speed", Float) = 0.4
        _AQW_NormalSubSpeed  ("    Sub-layer (multiplier)", Float) = -0.25
        _AQW_NormalStrength  ("Strength", Range(0, 1)) = 0.5
        _AQW_AnimationSpeed  ("Animation Speed", Float) = 1
        _AQW_ScrollDir       ("Direction", Vector) = (1, 0, 0, 0)
        _AQW_TriplanarSharpness ("Triplanar Sharpness", Range(1, 16)) = 8

        [Toggle(_AQW_DISTANCE_NORMALS)] _AQW_DistanceNormalsOn ("Distance Normals", Float) = 0
        [NoScaleOffset] _AQW_BumpMapLarge ("    Large Normal Map", 2D) = "bump" {}
        _AQW_DistanceNormalsTiling   ("    Tiling", Float) = 0.05
        _AQW_DistanceNormalsFadeDist ("    Blend Range (min, max)", Vector) = (5, 30, 0, 0)

        [Header(Surface)]
        [Space(4)]
        [Toggle(_AQW_SPECULAR)] _AQW_SpecularOn ("Specular", Float) = 1
        _AQW_SurfaceAngle ("Surface Angle Threshold", Range(-1, 1)) = 0.5
        _AQW_SurfaceBand  ("Surface Blend Band", Range(0.001, 1)) = 0.2
        _AQW_SunReflectionSize     ("Highlight Size", Range(1, 512)) = 64
        _AQW_SunReflectionStrength ("Highlight Strength (메인 디렉셔널)", Float) = 1
        _AQW_PointSpotReflectionStrength ("Highlight Strength (Point / Spot)", Float) = 1
        _AQW_FallbackLightDir ("Fallback Light Dir (전역 미주입 시)", Vector) = (-0.3, -0.8, -0.5, 0)

        [Header(Refraction)]
        [Space(4)]
        [Toggle(_AQW_REFRACTION)] _AQW_RefractionOn ("Refraction", Float) = 1
        _AQW_RefractionStrength ("Strength", Range(0, 4)) = 1

        [Header(Caustics)]
        [Space(4)]
        [Toggle(_AQW_CAUSTICS)] _AQW_CausticsOn ("Caustics", Float) = 1
        [NoScaleOffset] _AQW_CausticsMap ("Caustics Map", 2D) = "black" {}
        _AQW_CausticsTiling     ("Tiling", Float) = 0.1
        _AQW_CausticsSpeed      ("Speed", Float) = 0.1
        _AQW_CausticsBrightness ("Brightness", Float) = 2
        _AQW_CausticsChromance  ("Chromance", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "AquariumWaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back        // 볼록 형상의 근면만 그린다(카메라 궤도 회전 대응)
            ZWrite On
            ZTest LEqual
            Blend Off        // 알파 1 확정 → 탱크 내부 데스크톱 무관통

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _AQW_NORMALMAP
            #pragma shader_feature_local _AQW_DISTANCE_NORMALS
            #pragma shader_feature_local_fragment _AQW_DIFFUSE
            #pragma shader_feature_local_fragment _AQW_SPECULAR
            #pragma shader_feature_local_fragment _AQW_REFRACTION
            #pragma shader_feature_local_fragment _AQW_CAUSTICS

            // URP 라이팅 pragma 없음 — 태양/라이트 레이어/그림자와 무관하다(교체의 핵심).
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "AquariumWaterCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS   : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = n.normalWS;
                // 삼평면 투영은 오브젝트 공간에서 한다. 메시 노말을 그대로 넘겨야
                // 비균등 스케일에서도 투영 축 판정이 정확하다(월드→오브젝트 역변환은 노말에 부정확).
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS   = IN.normalOS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 geoNormalWS = normalize(IN.normalWS);
                float2 screenUV    = GetNormalizedScreenSpaceUV(IN.positionCS);
                float  camDist     = distance(GetCameraPositionWS(), IN.positionWS);

                // ── 1) 노말 (오브젝트공간 삼평면)
                float3 normalWS = geoNormalWS;
                #if defined(_AQW_NORMALMAP)
                    float3 nOS = normalize(IN.normalOS);
                    half3  perturbedOS = TriplanarNormalOS(IN.positionOS, nOS, camDist);
                    normalWS = normalize(TransformObjectToWorldNormal(perturbedOS));
                #endif

                // ── 2) 굴절 — 밀어낸 샘플이 물보다 '앞'이면 취소한다.
                //      (가드가 없으면 물 앞을 지나는 오브젝트의 실루엣이 물 안으로 번진다)
                float2 sampleUV = screenUV;
                #if defined(_AQW_REFRACTION)
                {
                    float3 nVS = TransformWorldToViewDir(normalWS);
                    float2 uvR = saturate(screenUV + nVS.xy * (_AQW_RefractionStrength * 0.02));

                    bool   hasSceneR;
                    float3 wsR = SceneWorldPos(uvR, hasSceneR);
                    if (hasSceneR == false || WaterEyeDepth(wsR) >= WaterEyeDepth(IN.positionWS))
                        sampleUV = uvR;
                }
                #endif

                // ── 3) 물속 감쇠 (유리면 → 뒤 물체 거리). 뒤가 비면 최대 감쇠.
                bool   hasSceneObject;
                float3 sceneWS    = SceneWorldPos(sampleUV, hasSceneObject);
                half3  sceneColor = SampleSceneColor(sampleUV);
                half   fog        = hasSceneObject ? WaterFog(IN.positionWS, sceneWS) : 1.0h;

                // 조명은 물 '자신의' 색에만 곱한다. sceneColor(물고기·바닥)는 이미 URP로 조명된
                // 결과라 여기서 또 곱하면 이중 조명이 된다.
                half3 deepColor = (half3)_AQW_DeepColor.rgb;
                #if defined(_AQW_DIFFUSE)
                    deepColor *= WaterLightAmount(IN.positionWS);
                #endif

                half3 col = lerp(sceneColor, deepColor, fog);

                // ── 4) 코스틱 — 뒤에 실제 물체가 있는 곳에만, 감쇠가 큰 곳에서는 약하게
                #if defined(_AQW_CAUSTICS)
                    UNITY_BRANCH
                    if (hasSceneObject)
                        col += SampleCaustics(sceneWS) * (1.0h - fog);
                #endif

                // ── 5) 수면 스펙큘러 — 법선 자동 판별로 윗면(구형은 위쪽 캡)에만
                #if defined(_AQW_SPECULAR)
                    half surfaceMask = (half)smoothstep(_AQW_SurfaceAngle - _AQW_SurfaceBand,
                                                        _AQW_SurfaceAngle + _AQW_SurfaceBand,
                                                        dot(geoNormalWS, float3(0, 1, 0)));
                    UNITY_BRANCH
                    if (surfaceMask > 0.001h)
                    {
                        float3 V = normalize(GetCameraPositionWS() - IN.positionWS);

                        // 메인 디렉셔널(태양 대체). _AQW_LightDir은 라이트 → 표면 방향이라 뒤집는다.
                        half3 spec = WaterSpecular(normalWS, V, -GetWaterLightDir(), GetWaterLightColor(),
                                                   1.0h, _AQW_SunReflectionStrength);

                        // 추가 Point / Spot / Directional
                        spec += AdditionalWaterSpecular(IN.positionWS, normalWS, V);

                        col += spec * surfaceMask;
                    }
                #endif

                return half4(col, 1.0h);   // 알파 1 확정(불투명 채움)
            }
            ENDHLSL
        }
    }

    // 그림자 캐스팅·깊이 프리패스 패스를 두지 않는다.
    // DepthOnly가 있으면 물이 _CameraDepthTexture에 들어가 스스로를 샘플해 감쇠가 0이 된다.
    Fallback Off
}
