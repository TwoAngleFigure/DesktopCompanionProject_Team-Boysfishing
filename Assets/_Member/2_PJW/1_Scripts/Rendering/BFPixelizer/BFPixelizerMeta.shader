// BF Pixelizer 메타 패스(계획 12, P1).
// 대상 오브젝트의 픽셀화 정보를 RGBAHalf 메타 RT에 인코딩한다:
//   R = 오브젝트 ID(1..255, 0 = 비대상/배경)
//   G = 매크로 픽셀 크기 N(1..5)
//   BA = 격자 위상(phase) = 투영 피벗 픽셀좌표 mod N  (0..N-1, half 정밀도 안전 범위)
// 절대 피벗 좌표 대신 위상을 저장하는 이유: half는 2048 초과 정수가 부정확하지만
// resolve의 블록 앵커 계산에는 위상만 있으면 충분하다.
Shader "Hidden/BFPixelizer/Meta"
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
            Name "BFPixelizerMeta"
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // PixelizedObject가 MPB로 렌더러 단위 전달(머티리얼 프로퍼티 아님 — SRP 배칭 제외 허용).
            float _BFP_ID;        // 1..255, 미설정(비대상) = 0
            float _BFP_PixelSize; // 1..5
            float4 _BFP_PivotWS;  // 격자 원점(월드) = transform.position

            // 메타 RT 크기(픽셀). 패스가 전역으로 설정 — _ScreenParams/RTHandle 스케일 불일치 회피.
            float4 _BFP_ScreenSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag() : SV_Target
            {
                float n = max(1.0, round(_BFP_PixelSize));

                // 피벗을 래스터라이저 픽셀 공간으로 투영. 정수 반올림 = 격자가 오브젝트와
                // 함께 정수 픽셀 단위로 이동 → 내부 텍셀 안정(크리프 방지)의 핵심.
                float4 pivotCS = TransformWorldToHClip(_BFP_PivotWS.xyz);
                float2 pivotNdc = pivotCS.xy / pivotCS.w * 0.5 + 0.5;
                float2 pivotPx = pivotNdc * _BFP_ScreenSize.xy;
                #if UNITY_UV_STARTS_AT_TOP
                // ProPixelizer 검증 방식. P2 시각 검증에서 격자 어긋남이 보이면 _ProjectionParams.x 기반으로 조정.
                pivotPx.y = _BFP_ScreenSize.y - pivotPx.y;
                #endif
                pivotPx = round(pivotPx);

                // 위상 = pivot mod N (음수 좌표 대비 이중 fmod 정규화)
                float2 phase = fmod(fmod(pivotPx, n) + n, n);

                return half4(_BFP_ID, n, phase.x, phase.y);
            }
            ENDHLSL
        }
    }
}
