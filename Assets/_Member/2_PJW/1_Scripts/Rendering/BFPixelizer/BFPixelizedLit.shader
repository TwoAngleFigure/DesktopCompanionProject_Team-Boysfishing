// BF Pixelizer v2 — 대상 오브젝트용 머티리얼 셰이더(계획 13, V3).
// 이 머티리얼을 쓰는 것 자체가 픽셀화 대상 지정이다(컴포넌트·레이어 불필요).
//  - 메인 패스 LightMode = "BFPixelizedForward"(커스텀) → URP 불투명 패스가 그리지 않고,
//    BFPixelizerFeature의 RendererList만 이 태그로 드로우한다.
//  - 정점 스냅: 오브젝트 피벗(모델 행렬 원점)을 전역 매크로픽셀 격자에 맞추는 서브픽셀
//    보정을 정점에서 수행 → 격자와 지오메트리가 항상 정렬(크리프 구조적 제거).
//  - MRT: RT0 = 라이팅 색(a=커버리지 1), RT1 = (ID, 아웃라인 RGB).
//  - V3: 메인 라이트 그림자 수신(케스케이드/소프트) + ShadowCaster(그림자 캐스팅).
//    ShadowCaster는 라이트 공간이므로 정점 스냅을 적용하지 않는다.
Shader "BFPixelizer/PixelizedLit"
{
    Properties
    {
        _BaseMap("Albedo", 2D) = "white" {}
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        _PixelSize("Pixel Size (V1: 전역 격자 사용 — 유보)", Range(1, 5)) = 3
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _ObjectId("Object Id (겹침 아웃라인 구분, 1~255)", Range(1, 255)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BFPixelizedForward"
            Tags { "LightMode" = "BFPixelizedForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 메인 라이트 그림자 수신 배리언트
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _PixelSize;
            half4 _OutlineColor;
            float _ObjectId;
            CBUFFER_END

            // 피처가 오프스크린 패스에서 설정하는 전역(메타 RT 크기·수퍼샘플 배율·전역 셀 크기).
            float4 _BFP_ScreenSize;
            float _BFP_PixelScale;
            // V1: 전역 단일 격자(RT px). 다운샘플 격자와 정점 스냅이 반드시 같은 값을 써야 하므로
            // 머티리얼 _PixelSize보다 우선한다(_PixelSize는 향후 크기 그룹 확장용으로 유보).
            float _BFP_CellSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                // ── 정점 스냅(계획 13 핵심) ──
                // 피벗을 전역 N격자에 맞추는 서브픽셀 잔차를 오브젝트 전체에 가산.
                // 잔차만 쓰므로 y-플립 규약에 불변. 전역 미설정(프리뷰 등) 시 스킵.
                if (_BFP_ScreenSize.x >= 1.0)
                {
                    float n = _BFP_CellSize >= 1.0
                        ? _BFP_CellSize
                        : max(1.0, round(_PixelSize)) * max(1.0, _BFP_PixelScale);
                    float3 pivotWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                    float4 pivotCS = TransformWorldToHClip(pivotWS);
                    float2 pivotPx = (pivotCS.xy / pivotCS.w * 0.5 + 0.5) * _BFP_ScreenSize.xy;
                    float2 snappedPx = round(pivotPx / n) * n;
                    float2 deltaNdc = (snappedPx - pivotPx) * _BFP_ScreenSize.zw * 2.0;
                    output.positionCS.xy += deltaNdc * output.positionCS.w;
                }

                return output;
            }

            struct FragOutput
            {
                half4 color : SV_Target0; // 라이팅 결과, a = 커버리지(1)
                half4 meta : SV_Target1;  // R = ID, GBA = 아웃라인 색
            };

            FragOutput frag(Varyings input)
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);

                // 그림자 좌표는 실제(스냅 전) 월드 위치 기준 — 그림자는 부드럽게 따라온다.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half lightAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 diffuse = LightingLambert(mainLight.color * lightAttenuation, mainLight.direction, normalWS);
                half3 ambient = SampleSH(normalWS);

                FragOutput output;
                // a = 커버리지 + 아웃라인 알파 인코딩: 0.5(알파0) ~ 1.0(알파1). 커버리지 판정(≥0.5)과 호환.
                output.color = half4(albedo.rgb * (diffuse + ambient), 0.5 + saturate(_OutlineColor.a) * 0.5);
                output.meta = half4(_ObjectId, _OutlineColor.rgb);
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            // 그림자 캐스팅(라이트 공간 — 정점 스냅 미적용).
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // SRP Batcher 호환: 메인 패스와 동일한 UnityPerMaterial 유지.
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _PixelSize;
            half4 _OutlineColor;
            float _ObjectId;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings shadowVert(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                Varyings output;
                output.positionCS = positionCS;
                return output;
            }

            half4 shadowFrag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
