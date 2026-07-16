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
    /// P2(현재): 메타 → 씬 컬러 복사 → Resolve(블록 앵커 재샘플) 3패스로 실제 픽셀화 표시.
    /// 남은 단계: P3 아웃라인 → P4 통합(카메라 게이팅·빌드 셰이더 참조).
    /// </summary>
    public class BFPixelizerFeature : ScriptableRendererFeature
    {
        [Tooltip("픽셀화 대상 레이어(전용 레이어 'Pixelated' 권장).")]
        [SerializeField] private LayerMask _pixelatedLayers = -1;

        [Tooltip("가림 판정 깊이 허용 오차(raw depth). 경사면 블록에서 원본이 새어 보이면 키운다.")]
        [SerializeField] private float _depthEpsilon = 0.001f;

        private const string MetaShaderName = "Hidden/BFPixelizer/Meta";
        private const string ResolveShaderName = "Hidden/BFPixelizer/Resolve";

        private BFPixelizerPass _pass;
        private Shader _metaShader;
        private Material _resolveMaterial;

        public override void Create()
        {
            // TODO(P4): 빌드 포함 보장을 위해 Shader.Find 대신 직렬화 참조로 전환.
            _metaShader = Shader.Find(MetaShaderName);
            Shader resolveShader = Shader.Find(ResolveShaderName);
            if (resolveShader != null)
                _resolveMaterial = CoreUtils.CreateEngineMaterial(resolveShader);
            _pass = new BFPixelizerPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_metaShader == null || _resolveMaterial == null)
            {
                Debug.LogWarning($"[BFPixelizer] 셰이더 로드 실패: {MetaShaderName} / {ResolveShaderName}");
                return;
            }

            // 대상이 하나도 없으면 전체 스킵(에디터 씬뷰·비활성 상태 포함).
            if (PixelizedObject.ActiveCount == 0)
                return;

            _pass.Setup(_metaShader, _resolveMaterial, _pixelatedLayers, _depthEpsilon);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth); // 가림 판정용 _CameraDepthTexture 보장
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                CoreUtils.Destroy(_resolveMaterial);
        }
    }

    /// <summary>
    /// 프레임당 3개 라스터 패스를 기록한다: 메타 → 씬 컬러 복사 → Resolve.
    /// 계획 11에서 검증된 RG 규칙 준수:
    ///  - renderPassEvent는 정확히 AfterRenderingOpaques(300) — -1 오프셋은 불투명 앞 범위로 기록됨.
    ///  - raster pass 안에서 행렬 설정 금지 — 그래프의 카메라 전역(unity_MatrixVP)이 이미 유효.
    ///  - 전역(텍스처/벡터/플로트) 설정 pass는 AllowGlobalStateModification(true) 필수.
    /// </summary>
    public class BFPixelizerPass : ScriptableRenderPass
    {
        // 렌더러 '선택'은 레이어 마스크가 담당하고, 태그는 URP 표준 머티리얼 매칭용(RenderObjects와 동일 구성).
        private static readonly List<ShaderTagId> s_forwardTags = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForwardOnly"),
        };

        private static readonly int s_screenSizeProp = Shader.PropertyToID("_BFP_ScreenSize");
        private static readonly int s_metaProp = Shader.PropertyToID("_BFP_Meta");
        private static readonly int s_metaDepthProp = Shader.PropertyToID("_BFP_MetaDepth");
        private static readonly int s_depthEpsProp = Shader.PropertyToID("_BFP_DepthEps");

        private Shader _metaShader;
        private Material _resolveMaterial;
        private LayerMask _layers;
        private float _depthEpsilon;

        public BFPixelizerPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public void Setup(Shader metaShader, Material resolveMaterial, LayerMask layers, float depthEpsilon)
        {
            _metaShader = metaShader;
            _resolveMaterial = resolveMaterial;
            _layers = layers;
            _depthEpsilon = depthEpsilon;
        }

        private class MetaPassData
        {
            public RendererListHandle RendererList;
            public Vector4 ScreenSize;
        }

        private class CopyPassData
        {
            public TextureHandle Source;
        }

        private class ResolvePassData
        {
            public TextureHandle SceneCopy;
            public TextureHandle Meta;
            public TextureHandle MetaDepth;
            public Material Material;
            public float DepthEpsilon;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                return;
            // 카메라가 대상 레이어를 보지 않으면(예: Screen Clear 카메라) 전체 스킵.
            if ((cameraData.camera.cullingMask & _layers) == 0)
                return;
            // 가림 판정에 _CameraDepthTexture 필요(ConfigureInput으로 요청됨).
            if (resourceData.cameraDepthTexture.IsValid() == false)
                return;

            // ── 프레임 텍스처 ──
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

            var sceneCopyDescriptor = cameraData.cameraTargetDescriptor;
            sceneCopyDescriptor.useMipMap = false;
            sceneCopyDescriptor.msaaSamples = 1;
            sceneCopyDescriptor.depthBufferBits = 0;

            var metaTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, metaDescriptor, "BFP_Meta", false, FilterMode.Point);
            var metaDepthTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDescriptor, "BFP_MetaDepth", false, FilterMode.Point);
            var sceneCopyTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, sceneCopyDescriptor, "BFP_SceneCopy", false, FilterMode.Point);

            // ── Pass 1: 메타(레이어 + overrideShader) ──
            var drawingSettings = CreateDrawingSettings(s_forwardTags, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
            drawingSettings.overrideShader = _metaShader;
            drawingSettings.overrideShaderPassIndex = 0;

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, _layers);
            var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            var rendererList = renderGraph.CreateRendererList(rendererListParams);

            using (var builder = renderGraph.AddRasterRenderPass<MetaPassData>("BF Pixelizer Meta", out var passData))
            {
                passData.RendererList = rendererList;
                passData.ScreenSize = new Vector4(metaDescriptor.width, metaDescriptor.height, 1f / metaDescriptor.width, 1f / metaDescriptor.height);

                builder.UseRendererList(rendererList);
                builder.SetRenderAttachment(metaTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(metaDepthTexture, AccessFlags.Write);
                builder.AllowGlobalStateModification(true); // _BFP_ScreenSize 전역 설정
                builder.SetRenderFunc((MetaPassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(s_screenSizeProp, data.ScreenSize);
                    context.cmd.ClearRenderTarget(true, true, Color.black);
                    context.cmd.DrawRendererList(data.RendererList);
                });
            }

            // ── Pass 2: 씬 컬러 복사(같은 텍스처 동시 읽기/쓰기 불가 → Resolve의 소스 확보) ──
            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("BF Pixelizer Copy Scene", out var passData))
            {
                passData.Source = resourceData.activeColorTexture;

                builder.UseTexture(passData.Source, AccessFlags.Read);
                builder.SetRenderAttachment(sceneCopyTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), 0f, false);
                });
            }

            // ── Pass 3: Resolve(블록 앵커 재샘플 → 카메라 컬러/깊이 되쓰기) ──
            using (var builder = renderGraph.AddRasterRenderPass<ResolvePassData>("BF Pixelizer Resolve", out var passData))
            {
                passData.SceneCopy = sceneCopyTexture;
                passData.Meta = metaTexture;
                passData.MetaDepth = metaDepthTexture;
                passData.Material = _resolveMaterial;
                passData.DepthEpsilon = _depthEpsilon;

                builder.UseTexture(sceneCopyTexture, AccessFlags.Read);
                builder.UseTexture(metaTexture, AccessFlags.Read);
                builder.UseTexture(metaDepthTexture, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                // discard 픽셀은 기존 값 유지가 필요하므로 ReadWrite(load) 바인딩.
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true); // 전역 텍스처/플로트 바인딩
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((ResolvePassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(s_metaProp, data.Meta);
                    context.cmd.SetGlobalTexture(s_metaDepthProp, data.MetaDepth);
                    context.cmd.SetGlobalFloat(s_depthEpsProp, data.DepthEpsilon);
                    Blitter.BlitTexture(context.cmd, data.SceneCopy, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                });
            }
        }
    }
}
