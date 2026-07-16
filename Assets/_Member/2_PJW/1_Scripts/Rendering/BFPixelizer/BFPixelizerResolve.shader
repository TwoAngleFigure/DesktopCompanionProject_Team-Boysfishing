// BF Pixelizer Resolve 패스(계획 12, P2).
// 각 화면 픽셀을 "오브젝트 피벗에 고정된 격자"의 블록 앵커 색으로 재샘플한다.
//  - 후보 탐색: 주변 9x9(반경 4 = 최대 픽셀크기 5-1)의 메타 샘플에서 (ID, N, 위상)을 얻고,
//    p가 속한 블록의 앵커를 계산 → 앵커가 같은 오브젝트 위면 유효 클레임.
//  - 복수 클레임은 깊이가 가장 가까운 것 채택. 색 = 씬 컬러(불투명 결과)의 앵커 픽셀(알파 포함).
//  - 가림: 씬 깊이(_CameraDepthTexture)가 블록 깊이보다 확실히 가까우면 원본 유지(다른 오브젝트에 가려짐).
//  - 깊이도 앵커 값으로 출력(SV_Depth) → 이후 물(투명)과의 합성 정상.
Shader "Hidden/BFPixelizer/Resolve"
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
            Name "BFPixelizerResolve"
            ZWrite On
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // _BlitTexture(Blit.hlsl 제공) = 씬 컬러 복사본. Blitter.BlitTexture의 source로 바인딩됨.
            TEXTURE2D_X(_BFP_Meta);            // R=ID, G=N, BA=위상
            TEXTURE2D_X_FLOAT(_BFP_MetaDepth); // 메타 전용 깊이
            float _BFP_DepthEps;               // 가림 판정 허용 오차(raw depth)

            #define BFP_MAX_RADIUS 4           // 최대 픽셀크기 5 → 탐색 반경 4

            struct FragOutput
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            FragOutput frag(Varyings input)
            {
                int2 p = int2(input.positionCS.xy);
                float2 pf = float2(p);

                // 빠른 배출: 반경 4를 간격 2 격자(5x5)로 프리체크 — 배경 픽셀 대부분 여기서 종료.
                bool anyMeta = false;
                [unroll] for (int sy = -BFP_MAX_RADIUS; sy <= BFP_MAX_RADIUS; sy += 2)
                {
                    [unroll] for (int sx = -BFP_MAX_RADIUS; sx <= BFP_MAX_RADIUS; sx += 2)
                    {
                        if (LOAD_TEXTURE2D_X(_BFP_Meta, p + int2(sx, sy)).r > 0.5)
                            anyMeta = true;
                    }
                }
                if (anyMeta == false)
                    discard;

                float sceneDepth = LoadSceneDepth(p);

                bool found = false;
                float bestDepth = 0;
                #if !UNITY_REVERSED_Z
                bestDepth = 1;
                #endif
                half4 bestColor = half4(0, 0, 0, 0);

                [loop] for (int v = -BFP_MAX_RADIUS; v <= BFP_MAX_RADIUS; v++)
                {
                    [loop] for (int u = -BFP_MAX_RADIUS; u <= BFP_MAX_RADIUS; u++)
                    {
                        half4 m = LOAD_TEXTURE2D_X(_BFP_Meta, p + int2(u, v));
                        if (m.r < 0.5)
                            continue; // 배경

                        float n = m.g;
                        float2 phase = m.ba;

                        // p가 속한 블록: blockStart = p - ((p - phase) mod N), 앵커 = 블록 중앙 픽셀.
                        float2 delta = fmod(fmod(pf - phase, n) + n, n);
                        int2 blockStart = int2(pf - delta);
                        int2 anchor = blockStart + (int)(n * 0.5);

                        half4 anchorMeta = LOAD_TEXTURE2D_X(_BFP_Meta, anchor);
                        if (abs(anchorMeta.r - m.r) > 0.25)
                            continue; // 앵커가 이 오브젝트 위가 아님 → 무효 클레임

                        float blockDepth = LOAD_TEXTURE2D_X(_BFP_MetaDepth, anchor).r;

                        // 가림 판정 + 최근접 선택(reversed-Z: 큰 값 = 가까움)
                        #if UNITY_REVERSED_Z
                        if (sceneDepth > blockDepth + _BFP_DepthEps)
                            continue;
                        bool nearer = blockDepth > bestDepth;
                        #else
                        if (sceneDepth < blockDepth - _BFP_DepthEps)
                            continue;
                        bool nearer = blockDepth < bestDepth;
                        #endif

                        if (found == false || nearer)
                        {
                            found = true;
                            bestDepth = blockDepth;
                            bestColor = LOAD_TEXTURE2D_X(_BlitTexture, anchor); // 알파 포함(투명 오버레이 보존)
                        }
                    }
                }

                if (found == false)
                    discard;

                FragOutput output;
                output.color = bestColor;
                output.depth = bestDepth;
                return output;
            }
            ENDHLSL
        }
    }
}
