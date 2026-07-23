// BF Pixelizer v2 합성 패스(계획 13).
// 오프스크린(또는 V1의 다운샘플) 버퍼를 카메라 컬러/깊이에 합성한다.
//  - _BFP_CellSize = 1 (V0: 풀해상도 통과) / N_rt (V1: 다운샘플 버퍼 업스케일).
//  - 커버리지(a) 없는 픽셀은 discard → 원본 유지.
//  - 가림: 씬 깊이가 셀 깊이보다 확실히 가까우면 discard(셀=단일 깊이라 경사 오차 불필요).
//  - 아웃라인: 4이웃 셀의 커버리지/ID 비교(탐색 아님 — 결정적 4탭).
//  - 셀 깊이를 SV_Depth로 출력 → 이후 물(투명) 합성 정상.
Shader "Hidden/BFPixelizer/Composite"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BFPixelizerComposite"
            ZWrite On
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 4.5
            // URP Core.hlsl이 Blit.hlsl에 필요한 TEXTURE2D_X 계열 매크로를 정의한다.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // _BlitTexture(Blit.hlsl) = 오프스크린/다운샘플 컬러(rgb=라이팅, a=아웃라인 강도).
            TEXTURE2D_X(_BFP_OffMeta);            // r=ObjectId(0=배경=커버리지), gba=아웃라인 색
            TEXTURE2D_X(_BFP_OffAlpha);           // r=오브젝트 알파, g=아웃라인 투명도
            TEXTURE2D_X_FLOAT(_BFP_OffDepth);     // 셀 깊이
            float _BFP_CellSize;                  // V0=1, V1=N_rt
            float _BFP_DepthEps;

            struct FragOutput
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            FragOutput frag(Varyings input)
            {
                // 화면 좌표·셀 크기는 항상 음수가 아니다. 부호 있는 정수 나눗셈은 GPU에서 느리므로
                // 나눗셈만 uint로 수행하고, 이웃 셀 오프셋(음수 포함) 연산을 위해 int2로 되돌린다.
                uint2 pu = uint2(input.positionCS.xy);
                uint cellSize = max(1u, (uint)_BFP_CellSize);
                int2 p = int2(pu);
                int2 cell = int2(pu / cellSize);

                // 커버리지 = ObjectId(배경은 Color.clear로 0). 계획 15 G0에서 color.a 판정을 대체.
                half4 cellMeta = LOAD_TEXTURE2D_X(_BFP_OffMeta, cell);
                if (cellMeta.r < 0.5)
                    discard; // 비대상 → 원본 유지

                float cellDepth = LOAD_TEXTURE2D_X(_BFP_OffDepth, cell).r;
                float sceneDepth = LoadSceneDepth(p);
                #if UNITY_REVERSED_Z
                if (sceneDepth > cellDepth + _BFP_DepthEps)
                    discard; // 다른 오브젝트에 가려짐
                #else
                if (sceneDepth < cellDepth - _BFP_DepthEps)
                    discard;
                #endif

                half4 cellColor = LOAD_TEXTURE2D_X(_BlitTexture, cell);
                half2 cellAlpha = LOAD_TEXTURE2D_X(_BFP_OffAlpha, cell).rg; // r=오브젝트 알파, g=아웃라인 투명도
                half strength = saturate(cellColor.a);                      // 아웃라인 강도

                // 아웃라인: 4이웃 셀이 비어 있거나 다른 ID면 경계 셀.
                bool edge = false;
                int2 dirs[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };
                [unroll] for (int k = 0; k < 4; k++)
                {
                    half neighborId = LOAD_TEXTURE2D_X(_BFP_OffMeta, cell + dirs[k]).r;
                    if (neighborId < 0.5 || abs(neighborId - cellMeta.r) > 0.25)
                        edge = true;
                }

                // 강도 0이면 경계 셀도 내부와 동일해진다(아웃라인 없을 때 테두리 링 아티팩트 방지).
                // 알파는 1로 고정한다 — 이 패스는 불투명 트랙 전용이며(Blend Off), 알파를 그대로 쓰면
                // World_RT에 구멍이 생겨 데스크톱이 비친다. 반투명은 Render Queue를 Transparent로
                // 바꿔 투명 트랙(패스 4)으로 보내야 한다(URP/Lit이 Opaque에서 알파를 무시하는 것과 동일).
                FragOutput output;
                output.color = edge
                    ? half4(lerp(cellColor.rgb, cellMeta.gba, strength), 1)
                    : half4(cellColor.rgb, 1);
                output.depth = cellDepth;
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            // 셀 깊이를 _CameraDepthTexture에 되쓰기(피처의 Pass 4에서 사용).
            // SW3 물 등 깊이 텍스처 소비자가 픽셀화 오브젝트의 수중 실루엣을 보게 한다.
            Name "BFPixelizerDepthTexUpdate"
            ZWrite On
            ZTest Always
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_BFP_OffMeta);        // r = ObjectId(0 = 배경 = 커버리지)
            TEXTURE2D_X_FLOAT(_BFP_OffDepth); // 저해상도 셀 깊이
            float _BFP_CellSize;

            float frag(Varyings input) : SV_Depth
            {
                // 좌표·셀 크기 모두 음수가 아니므로 uint 나눗셈을 쓴다(부호 있는 나눗셈 회피).
                uint2 p = uint2(input.positionCS.xy);
                uint2 cell = p / max(1u, (uint)_BFP_CellSize);

                if (LOAD_TEXTURE2D_X(_BFP_OffMeta, cell).r < 0.5)
                    discard;

                return LOAD_TEXTURE2D_X(_BFP_OffDepth, cell).r;
            }
            ENDHLSL
        }

        Pass
        {
            // 스프라이트(오브젝트 공간) 모드 합성(계획 14 S1+S2).
            // 저해상도 스프라이트 셀을 "정렬 시점의 역변환"으로 회전·배치해 카메라에 합성한다.
            // _BlitTexture = 저해상도 스프라이트 컬러(a=커버리지+아웃라인 알파 인코딩).
            Name "BFPixelizerSpriteComposite"
            ZWrite On
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_BFP_OffMeta);        // 저해상도 스프라이트 메타(r=ObjectId=커버리지, gba=아웃라인 색)
            TEXTURE2D_X(_BFP_OffAlpha);       // r=오브젝트 알파, g=아웃라인 투명도
            TEXTURE2D_X_FLOAT(_BFP_OffDepth); // 저해상도 스프라이트 깊이
            float _BFP_CellSize;
            float _BFP_DepthEps;
            float4 _BFP_SpriteRot;            // S0 정렬에 쓴 롤 행렬의 뷰 공간 2x2(행 우선) 그대로
            float4 _BFP_SpritePivot;          // xy = 카메라 raster 피벗(px), zw = 저해상도 스프라이트 중심(셀)
            float4 _BFP_SpriteAxis;           // x = 카메라 raster→뷰 y부호, y = 뷰→스프라이트 행 y부호
            float4 _BFP_SpriteDepthRange;     // x=스프라이트 near, y=스프라이트 far, z=카메라 near, w=카메라 far

            // 스프라이트 정렬 투영의 깊이 → 카메라 투영의 깊이(정사영: 선형 재매핑).
            // 범위가 다른 두 정사영 사이라 이것 없이는 물(투명)의 깊이 테스트가 어긋난다.
            float RemapSpriteDepth(float d)
            {
                #if UNITY_REVERSED_Z
                float zView = _BFP_SpriteDepthRange.y - d * (_BFP_SpriteDepthRange.y - _BFP_SpriteDepthRange.x);
                return saturate((_BFP_SpriteDepthRange.w - zView) / (_BFP_SpriteDepthRange.w - _BFP_SpriteDepthRange.z));
                #else
                float zView = _BFP_SpriteDepthRange.x + d * (_BFP_SpriteDepthRange.y - _BFP_SpriteDepthRange.x);
                return saturate((zView - _BFP_SpriteDepthRange.z) / (_BFP_SpriteDepthRange.w - _BFP_SpriteDepthRange.z));
                #endif
            }

            struct FragOutput
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            FragOutput frag(Varyings input)
            {
                float2 p = input.positionCS.xy;

                // 화면 픽셀(raster) → 뷰 공간 → 정렬(스프라이트 뷰) 공간 → 셀 인덱스.
                // y부호는 타깃별 UV 원점에서 산출된 유니폼 — 플립 규약 하드코딩 없음.
                float2 relPx = p - _BFP_SpritePivot.xy;
                float2 relView = float2(relPx.x, _BFP_SpriteAxis.x * relPx.y);
                float2 alignedView = float2(dot(_BFP_SpriteRot.xy, relView), dot(_BFP_SpriteRot.zw, relView));
                float2 cellF = _BFP_SpritePivot.zw + float2(alignedView.x, _BFP_SpriteAxis.y * alignedView.y) / max(1.0, _BFP_CellSize);
                int2 cell = int2(floor(cellF));

                // 커버리지 = ObjectId(스프라이트 버퍼 밖·미커버는 0).
                half4 cellMeta = LOAD_TEXTURE2D_X(_BFP_OffMeta, cell);
                if (cellMeta.r < 0.5)
                    discard; // 스프라이트 밖/비커버 → 원본 유지

                float cellDepth = RemapSpriteDepth(LOAD_TEXTURE2D_X(_BFP_OffDepth, cell).r);
                float sceneDepth = LoadSceneDepth(int2(p));
                #if UNITY_REVERSED_Z
                if (sceneDepth > cellDepth + _BFP_DepthEps)
                    discard;
                #else
                if (sceneDepth < cellDepth - _BFP_DepthEps)
                    discard;
                #endif

                half4 cellColor = LOAD_TEXTURE2D_X(_BlitTexture, cell);
                half2 cellAlpha = LOAD_TEXTURE2D_X(_BFP_OffAlpha, cell).rg;
                half strength = saturate(cellColor.a);

                // 아웃라인: 정렬(스프라이트) 공간의 4이웃 셀 — 회전과 함께 도는 경계.
                bool edge = false;
                int2 dirs[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };
                [unroll] for (int k = 0; k < 4; k++)
                {
                    if (LOAD_TEXTURE2D_X(_BFP_OffMeta, cell + dirs[k]).r < 0.5)
                        edge = true;
                }

                // 스프라이트 모드는 현재 불투명 트랙 전용이므로 알파를 1로 고정(위 패스와 동일 이유).
                FragOutput output;
                output.color = edge
                    ? half4(lerp(cellColor.rgb, cellMeta.gba, strength), 1)
                    : half4(cellColor.rgb, 1);
                output.depth = cellDepth;
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            // 스프라이트 셀 깊이를 _CameraDepthTexture에 되쓰기(재매핑 포함) —
            // SW3 물의 수중 투영이 스프라이트 모드 오브젝트를 보게 한다(v2의 pass 1과 동일 역할).
            Name "BFPixelizerSpriteDepthTexUpdate"
            ZWrite On
            ZTest Always
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_BFP_OffMeta);        // r = ObjectId(0 = 배경 = 커버리지)
            TEXTURE2D_X_FLOAT(_BFP_OffDepth);
            float _BFP_CellSize;
            float4 _BFP_SpriteRot;
            float4 _BFP_SpritePivot;
            float4 _BFP_SpriteAxis;
            float4 _BFP_SpriteDepthRange;

            float RemapSpriteDepth(float d)
            {
                #if UNITY_REVERSED_Z
                float zView = _BFP_SpriteDepthRange.y - d * (_BFP_SpriteDepthRange.y - _BFP_SpriteDepthRange.x);
                return saturate((_BFP_SpriteDepthRange.w - zView) / (_BFP_SpriteDepthRange.w - _BFP_SpriteDepthRange.z));
                #else
                float zView = _BFP_SpriteDepthRange.x + d * (_BFP_SpriteDepthRange.y - _BFP_SpriteDepthRange.x);
                return saturate((zView - _BFP_SpriteDepthRange.z) / (_BFP_SpriteDepthRange.w - _BFP_SpriteDepthRange.z));
                #endif
            }

            float frag(Varyings input) : SV_Depth
            {
                float2 p = input.positionCS.xy;
                float2 relPx = p - _BFP_SpritePivot.xy;
                float2 relView = float2(relPx.x, _BFP_SpriteAxis.x * relPx.y);
                float2 alignedView = float2(dot(_BFP_SpriteRot.xy, relView), dot(_BFP_SpriteRot.zw, relView));
                float2 cellF = _BFP_SpritePivot.zw + float2(alignedView.x, _BFP_SpriteAxis.y * alignedView.y) / max(1.0, _BFP_CellSize);
                int2 cell = int2(floor(cellF));

                if (LOAD_TEXTURE2D_X(_BFP_OffMeta, cell).r < 0.5)
                    discard;

                return RemapSpriteDepth(LOAD_TEXTURE2D_X(_BFP_OffDepth, cell).r);
            }
            ENDHLSL
        }

        Pass
        {
            // 투명 트랙 합성(계획 15). 물(투명 큐) 이후에 실행되어 유리 너머로 물·물고기가 비친다.
            //  - ZWrite Off: 깊이를 쓰면 이후 소비자가 유리에 막힌다. 가림은 _CameraDepthTexture 비교로 처리.
            //  - 알파 블렌드의 알파 항이 One OneMinusSrcAlpha라 올바른 "over" 누적이 되어
            //    불투명 씬 위에서는 World_RT 알파 1이 유지된다(데스크톱 관통 없음).
            Name "BFPixelizerCompositeTransparent"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_BFP_OffMeta);
            TEXTURE2D_X(_BFP_OffAlpha);
            TEXTURE2D_X_FLOAT(_BFP_OffDepth);
            float _BFP_CellSize;
            float _BFP_DepthEps;

            half4 frag(Varyings input) : SV_Target
            {
                uint2 pu = uint2(input.positionCS.xy);
                uint cellSize = max(1u, (uint)_BFP_CellSize);
                int2 p = int2(pu);
                int2 cell = int2(pu / cellSize);

                half4 cellMeta = LOAD_TEXTURE2D_X(_BFP_OffMeta, cell);
                if (cellMeta.r < 0.5)
                    discard;

                float cellDepth = LOAD_TEXTURE2D_X(_BFP_OffDepth, cell).r;
                float sceneDepth = LoadSceneDepth(p);
                #if UNITY_REVERSED_Z
                if (sceneDepth > cellDepth + _BFP_DepthEps)
                    discard; // 앞의 불투명 물체에 가려짐
                #else
                if (sceneDepth < cellDepth - _BFP_DepthEps)
                    discard;
                #endif

                half4 cellColor = LOAD_TEXTURE2D_X(_BlitTexture, cell);
                half2 cellAlpha = LOAD_TEXTURE2D_X(_BFP_OffAlpha, cell).rg;
                half strength = saturate(cellColor.a);

                bool edge = false;
                int2 dirs[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };
                [unroll] for (int k = 0; k < 4; k++)
                {
                    half neighborId = LOAD_TEXTURE2D_X(_BFP_OffMeta, cell + dirs[k]).r;
                    if (neighborId < 0.5 || abs(neighborId - cellMeta.r) > 0.25)
                        edge = true;
                }

                return edge
                    ? half4(lerp(cellColor.rgb, cellMeta.gba, strength), lerp(cellAlpha.r, cellAlpha.g, strength))
                    : half4(cellColor.rgb, cellAlpha.r);
            }
            ENDHLSL
        }
    }
}
