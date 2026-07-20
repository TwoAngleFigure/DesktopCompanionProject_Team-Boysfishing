// DitherFadeLit의 더 매끄러운·경량 변형.
// 디더 임계값을 4x4 Bayer 대신 Interleaved Gradient Noise(IGN)로 계산한다:
//  - IGN은 격자 패턴이 덜 보여 페이드가 더 매끄럽게 보임.
//  - 텍스처/배열 인덱싱 없이 순수 ALU 몇 줄 → Bayer(배열 조회)보다도 가벼움(블루노이즈/MSAA 불필요).
// 그 외(Opaque 유지, 알파=1 출력→데스크톱 무관통, _Fade 0→1)는 DitherFadeLit과 동일.
// _Fade 프로퍼티명이 같아 DitherFade 드라이버를 그대로 사용한다.
Shader "DesktopCompanion/DitherFadeLitSmooth"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Fade ("Fade (0=투명, 1=불투명)", Range(0,1)) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            float  _Fade;
            half   _Metallic;
            half   _Smoothness;
        CBUFFER_END

        // Interleaved Gradient Noise (Jorge Jimenez). 화면 픽셀 위치 기준 0..1.
        // 배열/텍스처 없이 순수 연산 → 매우 저비용, 격자 없이 매끄러운 분포.
        float DitherThreshold(float2 screenPos)
        {
            const float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
            return frac(magic.z * frac(dot(screenPos, magic.xy)));
        }
        ENDHLSL

        // ───── ForwardLit ─────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // Forward+ 추가 라이트(이것 없으면 메인 라이트만 동작) + 라이트 렌더링 레이어 분리 지원
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float2 uv:TEXCOORD0;
                float3 positionWS:TEXCOORD1;
                float3 normalWS:TEXCOORD2;
                float  fogCoord:TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS   = n.normalWS;
                o.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                o.fogCoord   = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                clip(_Fade - DitherThreshold(IN.positionCS.xy));

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                InputData inputData = (InputData)0;
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord        = IN.fogCoord;

                SurfaceData surface = (SurfaceData)0;
                surface.albedo     = tex.rgb * _BaseColor.rgb;
                surface.metallic   = _Metallic;
                surface.smoothness = _Smoothness;
                surface.occlusion  = 1.0;
                surface.alpha      = 1.0;   // 그린 픽셀은 알파 1(데스크톱 무관통)

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        // ───── ShadowCaster ─────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual Cull [_Cull]
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; };

            V shadowVert(A IN)
            {
                V o;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(IN.normalOS);
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = posCS;
                return o;
            }

            half4 shadowFrag(V IN) : SV_Target
            {
                clip(_Fade - DitherThreshold(IN.positionCS.xy));
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
