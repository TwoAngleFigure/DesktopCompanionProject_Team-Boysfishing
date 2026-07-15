// BF Pixelizer 메타 패스(계획 12).
// P0: 단색 마젠타 출력 — 레이어+overrideShader RendererList 드로우 검증용.
// P1에서 프래그먼트를 ID/PixelSize/투영 피벗 인코딩으로 교체한다.
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
                return half4(1, 0, 1, 1); // P0 검증용 마젠타
            }
            ENDHLSL
        }
    }
}
