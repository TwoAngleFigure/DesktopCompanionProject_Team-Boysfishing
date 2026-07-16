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

            // _BlitTexture(Blit.hlsl) = 오프스크린/다운샘플 컬러(a=커버리지).
            TEXTURE2D_X(_BFP_OffMeta);            // R=ID, GBA=아웃라인 색
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
                int2 p = int2(input.positionCS.xy);
                int cellSize = max(1, (int)_BFP_CellSize);
                int2 cell = p / cellSize;

                half4 cellColor = LOAD_TEXTURE2D_X(_BlitTexture, cell);
                if (cellColor.a < 0.5)
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

                half4 cellMeta = LOAD_TEXTURE2D_X(_BFP_OffMeta, cell);

                // 아웃라인: 4이웃 셀이 비어 있거나 다른 ID면 경계 셀.
                bool edge = false;
                int2 dirs[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };
                [unroll] for (int k = 0; k < 4; k++)
                {
                    int2 nc = cell + dirs[k];
                    half neighborCoverage = LOAD_TEXTURE2D_X(_BlitTexture, nc).a;
                    half neighborId = LOAD_TEXTURE2D_X(_BFP_OffMeta, nc).r;
                    if (neighborCoverage < 0.5 || abs(neighborId - cellMeta.r) > 0.25)
                        edge = true;
                }

                FragOutput output;
                // 커버리지 채널에 인코딩된 아웃라인 알파(0.5~1.0 → 0~1)로 블록 색과 블렌드.
                half outlineAlpha = saturate((cellColor.a - 0.5) * 2.0);
                output.color = edge
                    ? half4(lerp(cellColor.rgb, cellMeta.gba, outlineAlpha), 1)
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

            TEXTURE2D_X_FLOAT(_BFP_OffDepth); // 저해상도 셀 깊이
            float _BFP_CellSize;

            float frag(Varyings input) : SV_Depth
            {
                int2 p = int2(input.positionCS.xy);
                int2 cell = p / max(1, (int)_BFP_CellSize);

                // _BlitTexture = 저해상도 컬러(a=커버리지)
                if (LOAD_TEXTURE2D_X(_BlitTexture, cell).a < 0.5)
                    discard;

                return LOAD_TEXTURE2D_X(_BFP_OffDepth, cell).r;
            }
            ENDHLSL
        }
    }
}
