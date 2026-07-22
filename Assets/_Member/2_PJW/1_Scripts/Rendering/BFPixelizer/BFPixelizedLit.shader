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
        // ⚠ 이름에 _BFP_ 접두사 필수: 단순 `_ObjectId`는 Unity 에디터의 씬 뷰 피킹이 쓰는
        // 전역 int 프로퍼티와 충돌해 머티리얼 값이 무시되고 항상 0으로 읽힌다.
        _BFP_ObjectId("Object Id (겹침 아웃라인 구분, 1~255)", Range(1, 255)) = 1
        _RenderingLayers("Rendering Layers (수광 레이어 비트, 일반=1)", Float) = 1

        // 알파(계획 15). 반투명으로 쓰려면 머티리얼 인스펙터의 Render Queue를
        // Transparent(3000)로 바꿔야 한다 — 그래야 투명 트랙(물 이후 합성)으로 분류된다.
        _Alpha("Alpha (내부 픽셀 투명도)", Range(0, 1)) = 1
        [Toggle] _OutlineFollowsAlpha("Outline Follows Alpha (아웃라인도 함께 투명)", Float) = 0
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
            // 추가 라이트 + 라이트 렌더링 레이어(이중 글로벌 라이트: 일반=1, 물=2 분리 지원)
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _PixelSize;
            half4 _OutlineColor;
            float _BFP_ObjectId;
            float _RenderingLayers;
            float _Alpha;
            float _OutlineFollowsAlpha;
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

            // 채널 배치(계획 15 G0). 커버리지는 meta.r(ObjectId, 배경=0)이 담당하므로
            // color.a는 아웃라인 강도 전용이 되었다(구 0.5+a*0.5 인코딩 폐지).
            struct FragOutput
            {
                half4 color : SV_Target0; // rgb = 라이팅 결과, a = 아웃라인 강도(0~1)
                half4 meta  : SV_Target1; // r = ObjectId(0 = 배경 = 커버리지), gba = 아웃라인 RGB
                half2 alpha : SV_Target2; // r = 오브젝트 알파, g = 아웃라인 투명도
            };

            FragOutput frag(Varyings input)
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);

                // 수광 레이어: unity_RenderingLayer(드로우별)는 DrawRenderer 경로에서 신뢰 불가 → 머티리얼 프로퍼티 고정.
                // 기존 머티리얼은 새 프로퍼티가 재직렬화 전까지 0으로 올 수 있음 → 0 = 미설정 = 기본 레이어(1)로 해석.
                uint meshRenderingLayers = (uint)_RenderingLayers;
                if (meshRenderingLayers == 0u)
                    meshRenderingLayers = 1u;
                half4 shadowMask = half4(1, 1, 1, 1);

                // 그림자 좌표는 실제(스냅 전) 월드 위치 기준. '거리 페이드 포함' 오버로드 필수(범위 밖 감쇠 0 방지).
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                half3 lighting = half3(0, 0, 0);

                // 메인 라이트 — 레이어 일치 시에만 수광.
                // distanceAttenuation(=unity_LightData.z, 드로우별)은 DrawRenderer 경로에서 0 → 사용 금지.
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
                #endif
                {
                    lighting += LightingLambert(mainLight.color * mainLight.shadowAttenuation, mainLight.direction, normalWS);
                }

                // 추가 라이트 — 이중 글로벌 라이트 구성에서 '메인으로 선정되지 못한' 디렉셔널이 여기로 온다.
                #if defined(_ADDITIONAL_LIGHTS)
                // LIGHT_LOOP_BEGIN(클러스터 경로)이 'inputData' 변수를 매크로 내부에서 직접 참조하므로 선언 필수.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();

                #if USE_CLUSTER_LIGHT_LOOP
                // Forward+: 디렉셔널 추가 라이트는 클러스터 밖 선행 구간(URP Lit과 동일 패턴).
                [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                {
                    Light addDirLight = GetAdditionalLight(dirIndex, input.positionWS, shadowMask);
                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(addDirLight.layerMask, meshRenderingLayers))
                    #endif
                    {
                        lighting += LightingLambert(addDirLight.color * (addDirLight.distanceAttenuation * addDirLight.shadowAttenuation), addDirLight.direction, normalWS);
                    }
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(addLight.layerMask, meshRenderingLayers))
                    #endif
                    {
                        lighting += LightingLambert(addLight.color * (addLight.distanceAttenuation * addLight.shadowAttenuation), addLight.direction, normalWS);
                    }
                LIGHT_LOOP_END
                #endif

                half3 ambient = SampleSH(normalWS);

                FragOutput output;
                output.color = half4(albedo.rgb * (lighting + ambient), saturate(_OutlineColor.a));
                output.meta = half4(_BFP_ObjectId, _OutlineColor.rgb);
                // 아웃라인 투명도는 여기서 확정한다(토글이 합성 셰이더까지 전파될 필요 없음).
                // OFF면 1 → 내부가 투명해져도 테두리는 불투명하게 남는다(아쿠아리움 유리 룩).
                half objectAlpha = saturate(_Alpha);
                output.alpha = half2(objectAlpha, _OutlineFollowsAlpha > 0.5 ? objectAlpha : 1.0);
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
            float _BFP_ObjectId;
            float _RenderingLayers;
            float _Alpha;
            float _OutlineFollowsAlpha;
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
