using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DesktopCompanion.Rendering
{
    /// <summary>
    /// BF Pixelizer(계획 12) — 오브젝트별 픽셀화 + 아웃라인.
    ///
    /// P0(현재): 레이어 + overrideShader RendererList가 오프스크린 RT에 그려지는지 선검증.
    ///  - Frame Debugger의 "BF Pixelizer Meta" 패스 출력에 대상 오브젝트가 마젠타로 보이면 통과.
    /// 이후 단계: P1 메타 인코딩 → P2 Resolve 픽셀화 → P3 아웃라인 → P4 통합.
    /// </summary>
    public class BFPixelizerFeature : ScriptableRendererFeature
    {
        [Tooltip("픽셀화 대상 레이어. P0 검증은 Everything(-1)으로 시작해도 된다. P4에서 전용 레이어로 고정.")]
        [SerializeField] private LayerMask _pixelatedLayers = -1;

        private const string MetaShaderName = "Hidden/BFPixelizer/Meta";

        private BFPixelizerMetaPass _metaPass;
        private Shader _metaShader;

        public override void Create()
        {
            // TODO(P4): 빌드 포함 보장을 위해 Shader.Find 대신 직렬화 참조로 전환.
            _metaShader = Shader.Find(MetaShaderName);
            _metaPass = new BFPixelizerMetaPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_metaShader == null)
            {
                Debug.LogWarning($"[BFPixelizer] 셰이더를 찾을 수 없음: {MetaShaderName}");
                return;
            }

            _metaPass.Setup(_metaShader, _pixelatedLayers);
            renderer.EnqueuePass(_metaPass);
        }
    }

    /// <summary>
    /// 메타 패스: 대상 레이어 렌더러를 overrideShader로 오프스크린 메타 RT(+전용 깊이)에 렌더.
    /// 계획 11에서 검증된 RG 규칙 준수:
    ///  - renderPassEvent는 정확히 AfterRenderingOpaques(300) — -1 오프셋은 불투명 앞 범위로 기록됨.
    ///  - raster pass 안에서 행렬 설정 금지 — 그래프의 카메라 전역(unity_MatrixVP)이 이미 유효.
    /// </summary>
    public class BFPixelizerMetaPass : ScriptableRenderPass
    {
        // 렌더러 '선택'은 레이어 마스크가 담당하고, 태그는 URP 표준 머티리얼 매칭용(RenderObjects와 동일 구성).
        private static readonly List<ShaderTagId> s_forwardTags = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForwardOnly"),
        };

        private Shader _overrideShader;
        private LayerMask _layers;

        public BFPixelizerMetaPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public void Setup(Shader overrideShader, LayerMask layers)
        {
            _overrideShader = overrideShader;
            _layers = layers;
        }

        private class MetaPassData
        {
            public RendererListHandle RendererList;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                return;

            // 메타 RT: RGBAHalf(P1에서 R=ID, G=PixelSize, BA=투영 피벗을 담을 포맷을 미리 사용).
            var metaDescriptor = cameraData.cameraTargetDescriptor;
            metaDescriptor.useMipMap = false;
            metaDescriptor.msaaSamples = 1;
            metaDescriptor.depthBufferBits = 0;
            metaDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;

            var depthDescriptor = cameraData.cameraTargetDescriptor;
            depthDescriptor.useMipMap = false;
            depthDescriptor.msaaSamples = 1;
            depthDescriptor.graphicsFormat = GraphicsFormat.None;
            depthDescriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;

            var metaTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, metaDescriptor, "BFP_Meta", false, FilterMode.Point);
            var metaDepthTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDescriptor, "BFP_MetaDepth", false, FilterMode.Point);

            var drawingSettings = CreateDrawingSettings(s_forwardTags, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
            drawingSettings.overrideShader = _overrideShader;
            drawingSettings.overrideShaderPassIndex = 0;

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, _layers);
            var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            var rendererList = renderGraph.CreateRendererList(rendererListParams);

            using (var builder = renderGraph.AddRasterRenderPass<MetaPassData>("BF Pixelizer Meta", out var passData))
            {
                passData.RendererList = rendererList;

                builder.UseRendererList(rendererList);
                builder.SetRenderAttachment(metaTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(metaDepthTexture, AccessFlags.Write);
                builder.AllowPassCulling(false); // P0: 출력을 아직 소비하지 않으므로 컬링 방지 필수
                builder.SetRenderFunc((MetaPassData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(true, true, Color.black);
                    context.cmd.DrawRendererList(data.RendererList);
                });
            }
        }
    }
}
