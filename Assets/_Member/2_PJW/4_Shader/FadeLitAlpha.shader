// 디더가 아닌 '진짜 알파' 부드러운 페이드. Opaque 제약의 두 원인(텍스처 깨짐·데스크톱 관통)을
// 아래 두 장치로 해결하며 매끄럽게 페이드한다:
//  1) ZWrite On + ZTest LEqual → 자기 겹침(Tripo 메시)에서 가장 앞면만 그려 텍스처 깨짐 방지.
//  2) 프리멀티플라이드 알파(Blend One OneMinusSrcAlpha) → RGB는 배경과 부드럽게 섞이고,
//     알파는 result = _Fade + dstA*(1-_Fade) 로 '배경 이상'으로만 유지 → 배경이 불투명(알파1)이면
//     결과 알파도 1 = 데스크톱 무관통.
// 단일 패스라 오파크 텍스처/뎁스 프리패스 불필요 → 경량. _Fade 프로퍼티가 같아 DitherFade 드라이버 그대로.
// 계획: Docs/Plan/07_OpaqueTransparencyShader.md
Shader "DesktopCompanion/FadeLitAlpha"
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
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On                    // 자기 겹침에서 가장 앞면만(텍스처 깨짐 방지)
            ZTest LEqual
            Blend One OneMinusSrcAlpha    // 프리멀티플라이드: RGB 부드러운 페이드 + 알파 배경 이상 유지

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float  _Fade;
                half   _Metallic;
                half   _Smoothness;
            CBUFFER_END

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
                surface.alpha      = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, IN.fogCoord);

                // 프리멀티플라이드 출력: RGB×_Fade, A=_Fade.
                // Blend One OneMinusSrcAlpha → 부드러운 페이드 + 알파는 배경 이상 유지(데스크톱 무관통).
                return half4(color.rgb * _Fade, _Fade);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
