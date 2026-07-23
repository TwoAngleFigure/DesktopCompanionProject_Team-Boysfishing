// BF Pixelizer v2 다운샘플 패스(계획 13, V1).
// 오프스크린 풀해상도 버퍼를 1/N 해상도로 포인트 샘플한다(각 셀 = 블록 중앙 텍셀 1개).
// 평균이 아닌 포인트 샘플 = 픽셀아트 선명도 + ID 무결성.
// 정점 스냅이 모든 피벗을 같은 전역 N격자에 정렬시키므로, 이 단순 다운샘플만으로
// "오브젝트 고정 격자" 픽셀화가 완성된다(탐색·위상 메타 불필요).
Shader "Hidden/BFPixelizer/Downsample"
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
            Name "BFPixelizerDownsample"
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

            // _BlitTexture(Blit.hlsl) = 오프스크린 컬러(풀해상도).
            TEXTURE2D_X(_BFP_OffMeta);
            TEXTURE2D_X(_BFP_OffAlpha);
            TEXTURE2D_X_FLOAT(_BFP_OffDepth);
            float _BFP_CellSize; // N_rt

            struct FragOutput
            {
                half4 color : SV_Target0;
                half4 meta : SV_Target1;
                half2 alpha : SV_Target2; // r = 오브젝트 알파, g = 아웃라인 투명도(계획 15)
                float depth : SV_Depth;
            };

            FragOutput frag(Varyings input)
            {
                // 좌표·셀 크기 모두 음수가 아니므로 uint로 계산한다(부호 있는 정수 나눗셈 회피).
                uint2 cell = uint2(input.positionCS.xy);    // 저해상도 타깃의 픽셀 = 셀
                uint n = max(1u, (uint)_BFP_CellSize);
                uint2 src = cell * n + n / 2;                // 블록 중앙 텍셀(전역 격자와 정렬)

                FragOutput output;
                output.color = LOAD_TEXTURE2D_X(_BlitTexture, src);
                output.meta = LOAD_TEXTURE2D_X(_BFP_OffMeta, src);
                output.alpha = LOAD_TEXTURE2D_X(_BFP_OffAlpha, src).rg;
                output.depth = LOAD_TEXTURE2D_X(_BFP_OffDepth, src).r;
                return output;
            }
            ENDHLSL
        }
    }
}
