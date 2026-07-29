using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DesktopCompanion.Rendering
{
    /// <summary>
    /// 머티리얼 기반으로 오브젝트별 픽셀화와 아웃라인을 적용하는 렌더 피처.
    /// 대상은 "BFPixelizer/PixelizedLit" 머티리얼(커스텀 LightMode 태그)로 지정하므로 컴포넌트·레이어가 필요 없다.
    /// 경로는 오프스크린 MRT 렌더 → 포인트 다운샘플 → 합성이며, 불투명·투명 트랙을 각각 기록한다.
    /// </summary>
    public class BFPixelizerFeature : ScriptableRendererFeature
    {
        [Tooltip("전역 픽셀 크기(화면 픽셀). V1: 모든 대상이 이 단일 격자를 공유한다(머티리얼 _PixelSize는 유보).")]
        [Range(1, 5)]
        [SerializeField] private int _pixelSize = 3;

        [Tooltip("가림 판정 깊이 허용 오차(raw depth).")]
        [SerializeField] private float _depthEpsilon = 0.001f;

        [Tooltip("픽셀 스케일 자동 감지: 카메라 타깃 해상도 ÷ 화면 해상도. World_RT 수퍼샘플 배율을 자동 추종해 에디터/Play/빌드에서 블록의 화면 크기가 동일해진다.")]
        [SerializeField] private bool _autoPixelScale = true;

        [Tooltip("수동 배율(자동 감지 해제 시). 카메라가 수퍼샘플 RT에 렌더하면 그 배율과 일치시킬 것(본 프로젝트 World_RT=2).")]
        [Range(1f, 4f)]
        [SerializeField] private float _pixelScale = 1f;

        [Header("월드 격자 (계획 23)")]
        [Tooltip("블록 크기를 월드 유닛 기준으로 고정한다. 해상도가 달라도 오브젝트가 같은 도트 수로 그려진다. " +
                 "격자 기준값은 PixelGridDesign이 소유한다(640×360 · 18도트/유닛). " +
                 "해제 시 화면 격자(_pixelSize 기준)로 동작한다 — 비교·폴백용.")]
        [SerializeField] private bool _worldSpaceGrid = true;

        // 빌드 포함 보장을 위한 직렬화 참조(에디터에서 자동 할당).
        [SerializeField, HideInInspector] private Shader _compositeShader;
        [SerializeField, HideInInspector] private Shader _downsampleShader;

        private const string CompositeShaderName = "Hidden/BFPixelizer/Composite";
        private const string DownsampleShaderName = "Hidden/BFPixelizer/Downsample";

        // 불투명/투명 2트랙: 렌더 큐로 분리한다. 이벤트가 달라 패스 인스턴스도 분리해야 한다.
        //  - 불투명: AfterRenderingOpaques, 깊이 기록 + DepthTex 주입(현행)
        //  - 투명(유리): AfterRenderingTransparents, 블렌딩·깊이 미기록 → 물 너머가 비친다
        private BFPixelizerPass _opaquePass;
        private BFPixelizerPass _transparentPass;
        private Material _compositeMaterial;
        private Material _downsampleMaterial;

        public override void Create()
        {
#if UNITY_EDITOR
            // 에디터에서 참조를 채워 직렬화 → 빌드에 셰이더 포함 보장.
            if (_compositeShader == null || _downsampleShader == null)
            {
                _compositeShader = Shader.Find(CompositeShaderName);
                _downsampleShader = Shader.Find(DownsampleShaderName);
                if (_compositeShader != null && _downsampleShader != null)
                    UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
            if (_compositeShader != null)
                _compositeMaterial = CoreUtils.CreateEngineMaterial(_compositeShader);
            if (_downsampleShader != null)
                _downsampleMaterial = CoreUtils.CreateEngineMaterial(_downsampleShader);
            _opaquePass = new BFPixelizerPass(false);
            _transparentPass = new BFPixelizerPass(true);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_compositeMaterial == null || _downsampleMaterial == null)
            {
                Debug.LogWarning($"[BFPixelizer] 셰이더 로드 실패: {CompositeShaderName} / {DownsampleShaderName}");
                return;
            }

            _opaquePass.Setup(_compositeMaterial, _downsampleMaterial, _pixelSize, _depthEpsilon, _autoPixelScale, _pixelScale,
                _worldSpaceGrid);
            _opaquePass.ConfigureInput(ScriptableRenderPassInput.Depth); // 가림 판정용 _CameraDepthTexture 보장
            renderer.EnqueuePass(_opaquePass);

            _transparentPass.Setup(_compositeMaterial, _downsampleMaterial, _pixelSize, _depthEpsilon, _autoPixelScale, _pixelScale,
                _worldSpaceGrid);
            _transparentPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_transparentPass);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                CoreUtils.Destroy(_compositeMaterial);
                CoreUtils.Destroy(_downsampleMaterial);
            }
        }
    }

    /// <summary>
    /// 오프스크린 MRT 렌더 → 다운샘플 → 합성 패스를 Render Graph에 기록하는 패스.
    /// raster pass 안에서는 행렬을 설정하지 않고, 전역 상태를 바꾸는 패스는 AllowGlobalStateModification을 켠다.
    /// </summary>
    public class BFPixelizerPass : ScriptableRenderPass
    {
        // 대상 선택은 이 커스텀 태그가 담당 — PixelizedLit 머티리얼의 메인 패스만 매칭된다.
        private static readonly List<ShaderTagId> s_pixelizedTags = new()
        {
            new ShaderTagId("BFPixelizedForward"),
        };

        private static readonly int s_screenSizeProp = Shader.PropertyToID("_BFP_ScreenSize");
        private static readonly int s_pixelScaleProp = Shader.PropertyToID("_BFP_PixelScale");
        private static readonly int s_offMetaProp = Shader.PropertyToID("_BFP_OffMeta");
        private static readonly int s_offAlphaProp = Shader.PropertyToID("_BFP_OffAlpha");
        private static readonly int s_offDepthProp = Shader.PropertyToID("_BFP_OffDepth");
        private static readonly int s_cellSizeProp = Shader.PropertyToID("_BFP_CellSize");
        private static readonly int s_depthEpsProp = Shader.PropertyToID("_BFP_DepthEps");
        private static readonly int s_spriteRotProp = Shader.PropertyToID("_BFP_SpriteRot");
        private static readonly int s_spritePivotProp = Shader.PropertyToID("_BFP_SpritePivot");
        private static readonly int s_spriteAxisProp = Shader.PropertyToID("_BFP_SpriteAxis");
        private static readonly int s_spriteDepthRangeProp = Shader.PropertyToID("_BFP_SpriteDepthRange");

        // 앰비언트 SH(unity_SH*) — cmd.DrawRenderer는 드로우별 프로브 데이터를 실어주지 않아
        // 잔여값(때때로 0 = 검은 앰비언트)을 물려받으므로, 스프라이트 패스에서 직접 공급한다.
        private static readonly int[] s_shProps =
        {
            Shader.PropertyToID("unity_SHAr"), Shader.PropertyToID("unity_SHAg"), Shader.PropertyToID("unity_SHAb"),
            Shader.PropertyToID("unity_SHBr"), Shader.PropertyToID("unity_SHBg"), Shader.PropertyToID("unity_SHBb"),
            Shader.PropertyToID("unity_SHC"),
        };

        private readonly Vector4[] _shConstants = new Vector4[7];

        /// <summary>스프라이트 경로의 프러스텀 컬링에 재사용하는 평면 버퍼.</summary>
        private readonly Plane[] _frustumPlanes = new Plane[6];

        /// <summary>RenderSettings.ambientProbe를 셰이더 SH 상수(표준 패킹)로 변환한다.</summary>
        private void UpdateAmbientShConstants()
        {
            UnityEngine.Rendering.SphericalHarmonicsL2 sh = RenderSettings.ambientProbe;
            for (int c = 0; c < 3; c++)
            {
                _shConstants[c] = new Vector4(sh[c, 3], sh[c, 1], sh[c, 2], sh[c, 0] - sh[c, 6]);
                _shConstants[3 + c] = new Vector4(sh[c, 4], sh[c, 5], sh[c, 6] * 3f, sh[c, 7]);
            }
            _shConstants[6] = new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], 1f);
        }

        private Material _compositeMaterial;
        private Material _downsampleMaterial;
        private int _pixelSize;
        private float _depthEpsilon;
        private bool _autoPixelScale;
        private float _manualPixelScale;
        private bool _worldSpaceGrid;
        private bool _warnedSubPixel;
        private bool _warnedDepthTexNotAttachable;

        /// <summary>투명 트랙 여부. 큐 범위·합성 패스·주입 시점·깊이 기록 정책이 이 값에 따라 갈린다.</summary>
        private readonly bool _transparentTrack;

        public BFPixelizerPass(bool transparentTrack)
        {
            _transparentTrack = transparentTrack;
            // 소스 검증(UniversalRendererRenderGraph.cs 1264~1269): AfterRenderingTransparents는
            // 투명 드로우 '이후'에 기록된다 → 유리가 물 위에 블렌딩된다.
            renderPassEvent = transparentTrack
                ? RenderPassEvent.AfterRenderingTransparents
                : RenderPassEvent.AfterRenderingOpaques;
        }

        public void Setup(Material compositeMaterial, Material downsampleMaterial, int pixelSize, float depthEpsilon,
            bool autoPixelScale, float manualPixelScale, bool worldSpaceGrid)
        {
            _compositeMaterial = compositeMaterial;
            _downsampleMaterial = downsampleMaterial;
            _pixelSize = Mathf.Clamp(pixelSize, 1, 5);
            _depthEpsilon = depthEpsilon;
            _autoPixelScale = autoPixelScale;
            _manualPixelScale = Mathf.Max(1f, manualPixelScale);
            _worldSpaceGrid = worldSpaceGrid;
        }

        private class OffscreenPassData
        {
            public RendererListHandle RendererList;
            public Vector4 ScreenSize;
            public float PixelScale;
            public float CellSize;
        }

        private class DownsamplePassData
        {
            public TextureHandle OffColor;
            public TextureHandle OffMeta;
            public TextureHandle OffAlpha;
            public TextureHandle OffDepth;
            public Material Material;
            public float CellSize;
        }

        private class CompositePassData
        {
            public TextureHandle OffColor;
            public TextureHandle OffMeta;
            public TextureHandle OffAlpha;
            public TextureHandle OffDepth;
            public Material Material;
            public float CellSize;
            public float DepthEpsilon;
            public int CompositePassIndex; // 0 = 불투명, 4 = 투명(블렌드·깊이 미기록)
        }

        private class DepthTexUpdatePassData
        {
            public TextureHandle LowColor;
            public TextureHandle LowMeta;
            public TextureHandle LowDepth;
            public Material Material;
            public float CellSize;
        }

        private class SpritePassData
        {
            public List<PixelizedSpriteObject.DrawEntry> Draws;
            public Matrix4x4 AlignedView;
            public Matrix4x4 AlignedProj;   // 비GPU — 플립은 렌더 함수에서 실제 타깃 UV 원점으로 결정
            public Matrix4x4 RestoreView;
            public Matrix4x4 RestoreProj;   // 비GPU
            public Vector4[] AmbientSh;     // unity_SH* 7개(DrawRenderer는 프로브를 안 실어줌)
        }

        private class SpriteCompositePassData
        {
            public TextureHandle LowColor;
            public TextureHandle LowMeta;
            public TextureHandle LowAlpha;
            public TextureHandle LowDepth;
            public Material Material;
            public float CellSize;
            public float DepthEpsilon;
            public Vector4 SpriteRot;       // 롤 행렬 뷰 공간 2x2 그대로
            public Vector2 PivotNdc01;      // 비GPU 투영 기준(y위) — raster 변환은 렌더 함수에서
            public Vector2 CenterCell;
            public Vector2 TargetSize;
            public Vector4 DepthRange;      // (스프라이트 near, far, 카메라 near, far) — 깊이 재매핑
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection)
                return;
            if (cameraData.renderType == CameraRenderType.Overlay)
                return;
            if (cameraData.camera.cullingMask == 0) // Screen Clear 카메라 등
                return;
            if (resourceData.cameraDepthTexture.IsValid() == false)
                return;

            // ── 오프스크린 MRT(풀해상도) ──
            var colorDescriptor = cameraData.cameraTargetDescriptor;
            colorDescriptor.useMipMap = false;
            colorDescriptor.msaaSamples = 1;
            colorDescriptor.depthBufferBits = 0;
            colorDescriptor.colorFormat = RenderTextureFormat.ARGBHalf; // 알파(커버리지) 필수

            var depthDescriptor = cameraData.cameraTargetDescriptor;
            depthDescriptor.useMipMap = false;
            depthDescriptor.msaaSamples = 1;
            depthDescriptor.graphicsFormat = GraphicsFormat.None;
            depthDescriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;

            // 알파 전용 버퍼: r = 오브젝트 알파, g = 아웃라인 투명도. 2채널이면 충분.
            var alphaDescriptor = colorDescriptor;
            alphaDescriptor.colorFormat = RenderTextureFormat.RGHalf;

            // _CameraDepthTexture를 깊이 어태치먼트로 쓸 수 있는지 판별한다.
            // URP는 구성에 따라 이 텍스처를 진짜 깊이 포맷(DepthNormals 프리패스 — 예: SSAO 활성)으로
            // 만들기도 하고, CopyDepth 경로에서 컬러 포맷(R32_SFloat)으로 만들기도 한다. 후자를
            // SetRenderAttachmentDepth에 바인딩하면 RenderGraph 에러로 렌더링이 통째로 실패한다.
            bool depthTexAttachable = GraphicsFormatUtility.IsDepthFormat(
                renderGraph.GetTextureDesc(resourceData.cameraDepthTexture).format);
            if (depthTexAttachable == false && _warnedDepthTexNotAttachable == false)
            {
                _warnedDepthTexNotAttachable = true;
                Debug.LogWarning(
                    "[BFPixelizer] _CameraDepthTexture가 깊이 포맷이 아니라 DepthTex Update를 건너뜁니다. " +
                    "픽셀화 오브젝트가 물(SW3)의 수중 투영에 반영되지 않습니다. " +
                    "복구하려면 렌더러에서 깊이 프리패스를 유발하는 기능(예: SSAO)을 켜세요.");
            }

            // 두 트랙이 같은 프레임에 공존하므로 버퍼 이름을 분리한다(Frame Debugger 식별용).
            string tag = _transparentTrack ? "Tr" : "Op";

            var offColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorDescriptor, $"BFP_{tag}_OffColor", false, FilterMode.Point);
            var offMeta = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorDescriptor, $"BFP_{tag}_OffMeta", false, FilterMode.Point);
            var offAlpha = UniversalRenderer.CreateRenderGraphTexture(renderGraph, alphaDescriptor, $"BFP_{tag}_OffAlpha", false, FilterMode.Point);
            var offDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDescriptor, $"BFP_{tag}_OffDepth", false, FilterMode.Point);

            // 픽셀 스케일: 자동 = 카메라 타깃 해상도 ÷ 화면 해상도(Play 중 World_RT=2, 에디트 모드 백버퍼=1).
            float pixelScale = _autoPixelScale
                ? Mathf.Clamp(Mathf.RoundToInt((float)cameraData.cameraTargetDescriptor.width / Mathf.Max(1, Screen.width)), 1, 4)
                : _manualPixelScale;

            // ── 저해상도(1/N) 버퍼 — 다운샘플 출력 ──
            int cellSizeRt = Mathf.Max(1, ResolveCellSize(cameraData, pixelScale));
            var lowColorDescriptor = colorDescriptor;
            lowColorDescriptor.width = (colorDescriptor.width + cellSizeRt - 1) / cellSizeRt;
            lowColorDescriptor.height = (colorDescriptor.height + cellSizeRt - 1) / cellSizeRt;
            var lowAlphaDescriptor = alphaDescriptor;
            lowAlphaDescriptor.width = lowColorDescriptor.width;
            lowAlphaDescriptor.height = lowColorDescriptor.height;
            var lowDepthDescriptor = depthDescriptor;
            lowDepthDescriptor.width = lowColorDescriptor.width;
            lowDepthDescriptor.height = lowColorDescriptor.height;

            var lowColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowColorDescriptor, $"BFP_{tag}_LowColor", false, FilterMode.Point);
            var lowMeta = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowColorDescriptor, $"BFP_{tag}_LowMeta", false, FilterMode.Point);
            var lowAlpha = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowAlphaDescriptor, $"BFP_{tag}_LowAlpha", false, FilterMode.Point);
            var lowDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowDepthDescriptor, $"BFP_{tag}_LowDepth", false, FilterMode.Point);

            // ── Pass 1: 대상 머티리얼(커스텀 태그) 오프스크린 렌더 ──
            // 정렬은 투명 트랙도 불투명 기준을 쓴다 — 오프스크린 버퍼는 깊이로 해결되고,
            // 픽셀화 오브젝트끼리의 반투명 정렬은 지원 범위 밖.
            var drawingSettings = CreateDrawingSettings(s_pixelizedTags, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
            var filteringSettings = new FilteringSettings(_transparentTrack ? RenderQueueRange.transparent : RenderQueueRange.opaque)
            {
                // 스프라이트 모드 오브젝트는 화면 격자 경로에서 제외.
                renderingLayerMask = ~PixelizedSpriteObject.RenderingLayerBit,
            };
            var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            var rendererList = renderGraph.CreateRendererList(rendererListParams);

            using (var builder = renderGraph.AddRasterRenderPass<OffscreenPassData>("BF Pixelizer Offscreen", out var passData))
            {
                passData.RendererList = rendererList;
                passData.ScreenSize = new Vector4(colorDescriptor.width, colorDescriptor.height, 1f / colorDescriptor.width, 1f / colorDescriptor.height);
                passData.PixelScale = pixelScale;
                passData.CellSize = cellSizeRt;

                builder.UseRendererList(rendererList);
                builder.SetRenderAttachment(offColor, 0, AccessFlags.Write);
                builder.SetRenderAttachment(offMeta, 1, AccessFlags.Write);
                builder.SetRenderAttachment(offAlpha, 2, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(offDepth, AccessFlags.Write);
                // 그림자 수신 샘플링 의존 선언(RenderObjectsPass와 동일 패턴).
                if (resourceData.mainShadowsTexture.IsValid())
                    builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);
                if (resourceData.additionalShadowsTexture.IsValid())
                    builder.UseTexture(resourceData.additionalShadowsTexture, AccessFlags.Read);
                builder.AllowGlobalStateModification(true); // _BFP_ScreenSize/_BFP_PixelScale/_BFP_CellSize — 정점 스냅에 필요
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((OffscreenPassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(s_screenSizeProp, data.ScreenSize);
                    context.cmd.SetGlobalFloat(s_pixelScaleProp, data.PixelScale);
                    context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                    context.cmd.ClearRenderTarget(true, true, Color.clear);
                    context.cmd.DrawRendererList(data.RendererList);
                });
            }

            // ── Pass 2: 포인트 다운샘플(1/N — 픽셀화의 실체) ──
            using (var builder = renderGraph.AddRasterRenderPass<DownsamplePassData>("BF Pixelizer Downsample", out var passData))
            {
                passData.OffColor = offColor;
                passData.OffMeta = offMeta;
                passData.OffAlpha = offAlpha;
                passData.OffDepth = offDepth;
                passData.Material = _downsampleMaterial;
                passData.CellSize = cellSizeRt;

                builder.UseTexture(offColor, AccessFlags.Read);
                builder.UseTexture(offMeta, AccessFlags.Read);
                builder.UseTexture(offAlpha, AccessFlags.Read);
                builder.UseTexture(offDepth, AccessFlags.Read);
                builder.SetRenderAttachment(lowColor, 0, AccessFlags.Write);
                builder.SetRenderAttachment(lowMeta, 1, AccessFlags.Write);
                builder.SetRenderAttachment(lowAlpha, 2, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(lowDepth, AccessFlags.Write);
                builder.AllowGlobalStateModification(true); // _BFP_OffMeta/_BFP_OffAlpha/_BFP_OffDepth/_BFP_CellSize
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DownsamplePassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(s_offMetaProp, data.OffMeta);
                    context.cmd.SetGlobalTexture(s_offAlphaProp, data.OffAlpha);
                    context.cmd.SetGlobalTexture(s_offDepthProp, data.OffDepth);
                    context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                    Blitter.BlitTexture(context.cmd, data.OffColor, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                });
            }

            // ── Pass 3: 합성(CellSize=N — 저해상도 셀을 업스케일 + 아웃라인) ──
            // 불투명: 패스 0(Blend Off, SV_Depth 기록) / 투명: 패스 4(알파 블렌드, 깊이 미기록).
            string compositeName = _transparentTrack ? "BF Pixelizer Composite (Transparent)" : "BF Pixelizer Composite";
            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(compositeName, out var passData))
            {
                passData.OffColor = lowColor;
                passData.OffMeta = lowMeta;
                passData.OffAlpha = lowAlpha;
                passData.OffDepth = lowDepth;
                passData.Material = _compositeMaterial;
                passData.CellSize = cellSizeRt;
                passData.DepthEpsilon = _depthEpsilon;
                passData.CompositePassIndex = _transparentTrack ? 4 : 0;

                builder.UseTexture(lowColor, AccessFlags.Read);
                builder.UseTexture(lowMeta, AccessFlags.Read);
                builder.UseTexture(lowAlpha, AccessFlags.Read);
                builder.UseTexture(lowDepth, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                // discard 픽셀은 기존 값 유지가 필요하므로 ReadWrite(load) 바인딩.
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                // 투명 트랙은 ZWrite Off라 깊이 어태치먼트가 필요 없다(유리가 이후 소비자를 막지 않게).
                if (_transparentTrack == false)
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(s_offMetaProp, data.OffMeta);
                    context.cmd.SetGlobalTexture(s_offAlphaProp, data.OffAlpha);
                    context.cmd.SetGlobalTexture(s_offDepthProp, data.OffDepth);
                    context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                    context.cmd.SetGlobalFloat(s_depthEpsProp, data.DepthEpsilon);
                    Blitter.BlitTexture(context.cmd, data.OffColor, new Vector4(1f, 1f, 0f, 0f), data.Material, data.CompositePassIndex);
                });
            }

            // 투명 트랙은 여기서 종료: 스프라이트(회전 추종) 모드와 DepthTex 주입은 불투명 전용.
            // 유리가 깊이 텍스처에 들어가면 SW3 물의 수중 투영이 유리에 막힌다.
            if (_transparentTrack)
                return;

            // ── 오브젝트 공간 모드: 정렬 렌더 → 다운샘플 → 회전 배치 합성 ──
            RecordSpriteObjectPasses(renderGraph, cameraData, resourceData, cellSizeRt, depthTexAttachable);

            // ── Pass 4: 셀 깊이를 _CameraDepthTexture에 되쓰기 ──
            // SW3 물의 수중 투영(반투명·흡수)은 _CameraDepthTexture로 "물 아래 무엇이 있는지"를
            // 판단한다. 커스텀 태그 오브젝트는 프리패스에 없으므로 여기서 블록 깊이를 주입한다.
            if (depthTexAttachable == false)
                return; // 깊이 포맷이 아니면 주입 불가(위에서 경고) — 렌더링 자체는 계속된다.

            using (var builder = renderGraph.AddRasterRenderPass<DepthTexUpdatePassData>("BF Pixelizer DepthTex Update", out var passData))
            {
                passData.LowColor = lowColor;
                passData.LowMeta = lowMeta;
                passData.LowDepth = lowDepth;
                passData.Material = _compositeMaterial;
                passData.CellSize = cellSizeRt;

                builder.UseTexture(lowColor, AccessFlags.Read);
                builder.UseTexture(lowMeta, AccessFlags.Read);
                builder.UseTexture(lowDepth, AccessFlags.Read);
                builder.SetRenderAttachmentDepth(resourceData.cameraDepthTexture, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DepthTexUpdatePassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(s_offMetaProp, data.LowMeta);
                    context.cmd.SetGlobalTexture(s_offDepthProp, data.LowDepth);
                    context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                    Blitter.BlitTexture(context.cmd, data.LowColor, new Vector4(1f, 1f, 0f, 0f), data.Material, 1);
                });
            }
        }

        /// <summary>
        /// 격자 셀 크기(RT 픽셀)를 산출한다.
        ///
        /// 월드 격자: 블록의 월드 크기를 <see cref="PixelGridDesign.BlocksPerUnit"/>로 고정해
        /// 해상도가 달라도 오브젝트가 같은 도트 수로 그려진다.
        ///   블록(화면px) = RT_height ÷ (2 × orthographicSize) ÷ blocksPerUnit ÷ pixelScale
        /// 화면 블록 크기를 먼저 정수로 반올림한 뒤 배율을 곱하므로 블록 경계가 반픽셀에 걸리지 않는다.
        ///
        /// 화면 격자(폴백): 블록이 항상 _pixelSize 화면 픽셀이며, 해상도별로 오브젝트의 도트 수가 달라진다.
        /// </summary>
        private int ResolveCellSize(UniversalCameraData cameraData, float pixelScale)
        {
            Camera camera = cameraData.camera;
            if (_worldSpaceGrid == false || camera.orthographic == false || camera.orthographicSize <= 0f)
            {
                return Mathf.RoundToInt(_pixelSize * pixelScale);
            }

            int scaleStep = Mathf.Max(1, Mathf.RoundToInt(pixelScale));
            float ppuRt = cameraData.cameraTargetDescriptor.height / (2f * camera.orthographicSize);
            float exact = ppuRt / PixelGridDesign.BlocksPerUnit / scaleStep;

            int blockScreenPx = Mathf.RoundToInt(exact);
            if (blockScreenPx < 1)
            {
                // 도트가 물리 픽셀보다 작아지는 해상도 — 하한(1px)으로 잘리며 아트가 뭉개진다.
                // Scene 뷰 카메라는 사용자가 자유롭게 줌하는 대상(실제 타깃 해상도 아님)이라 경고가 노이즈다.
                // 또한 Game 카메라와 번갈아 렌더될 때 _warnedSubPixel이 매번 리셋되어 반복 경고를 유발한다.
                if (_warnedSubPixel == false && cameraData.cameraType != CameraType.SceneView)
                {
                    _warnedSubPixel = true;
                    Debug.LogWarning(
                        $"[BFPixelizer] 지원 하한 미만 해상도 — 블록이 {exact:F2}화면픽셀로 산출되어 1px로 제한합니다.\n" +
                        $"  기준 도트 해상도 {PixelGridDesign.BaseDotsWide}×{PixelGridDesign.BaseDotsHigh} 기준, " +
                        $"넓은 화면은 세로 {PixelGridDesign.BaseDotsHigh}px, 좁은 화면은 가로 {PixelGridDesign.BaseDotsWide}px 이상이 필요합니다.");
                }
                blockScreenPx = 1;
            }
            else
            {
                _warnedSubPixel = false;
            }
            return blockScreenPx * scaleStep;
        }

        /// <summary>스프라이트 모드 오브젝트마다 [정렬 렌더 → 다운샘플 → 회전 배치 합성] 패스를 기록한다.</summary>
        private void RecordSpriteObjectPasses(RenderGraph renderGraph, UniversalCameraData cameraData, UniversalResourceData resourceData,
            int cellSizeRt, bool depthTexAttachable)
        {
            if (PixelizedSpriteObject.Active.Count == 0)
                return;
            Camera camera = cameraData.camera;
            if (camera.orthographic == false)
                return;

            // 메인 카메라와 같은 픽셀 밀도로 스프라이트 버퍼 해상도 산출.
            float pixelsPerWorldUnit = cameraData.cameraTargetDescriptor.height / (2f * camera.orthographicSize);

            // 복원용 카메라 행렬(비GPU) — GPU 변환(플립)은 렌더 함수에서 실제 타깃 UV 원점으로 결정.
            Matrix4x4 restoreView = cameraData.GetViewMatrix();
            Matrix4x4 restoreProj = cameraData.GetProjectionMatrix();

            UpdateAmbientShConstants();

            // 카메라별 컬링 — 이 경로는 cmd.DrawRenderer를 직접 호출하므로 엔진 컬링(cullResults)이
            // 적용되지 않는다. 없으면 (a) 보이지도 않는 오브젝트에 카메라마다 풀스크린 패스 2개가
            // 낭비되고, (b) 다른 카메라(예: 아쿠아리움) 뷰에 메인 씬 오브젝트가 누출될 수 있다.
            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (PixelizedSpriteObject spriteObject in PixelizedSpriteObject.Active)
            {
                if (spriteObject == null || spriteObject.Draws.Count == 0)
                    continue;
                if ((camera.cullingMask & spriteObject.RendererLayerMask) == 0)
                    continue; // 이 카메라가 보지 않는 레이어
                if (GeometryUtility.TestPlanesAABB(_frustumPlanes, spriteObject.GetCullingBounds()) == false)
                    continue; // 시야 밖

                // 화면면 회전각 θ: 오브젝트 up의 뷰 공간 성분.
                Vector3 upVS = restoreView.MultiplyVector(spriteObject.UpWS);
                float thetaDeg = Mathf.Atan2(upVS.x, upVS.y) * Mathf.Rad2Deg;

                // 정렬 뷰 = (-θ 롤) × 카메라 뷰. 이 공간에서 오브젝트 up은 항상 화면 위쪽.
                Matrix4x4 roll = Matrix4x4.Rotate(Quaternion.AngleAxis(thetaDeg, Vector3.forward));
                Matrix4x4 alignedView = roll * restoreView;

                // 회전 불변 반경 → 버퍼 크기(셀 배수·짝수 셀 — 중심(피벗)이 항상 셀 경계에 놓여 격자 위상 고정).
                float radius = spriteObject.GetRadiusWS();
                int spriteSizePx = Mathf.Clamp(Mathf.CeilToInt(2f * radius * pixelsPerWorldUnit), 16, cameraData.cameraTargetDescriptor.height);
                int cellCount = (spriteSizePx + cellSizeRt - 1) / cellSizeRt;
                cellCount = ((cellCount + 3) / 4) * 4; // 4셀 단위 반올림(짝수 보장) — 이동 중 버퍼 크기 요동으로 인한 RG 재컴파일 완화
                spriteSizePx = cellCount * cellSizeRt;

                // 피벗 중심의 소형 정사영 — 창 크기는 버퍼 픽셀 수에 맞춰(메인 카메라와 동일 픽셀 밀도).
                float halfSizeWorld = spriteSizePx / (2f * pixelsPerWorldUnit);
                Vector3 pivotVS = alignedView.MultiplyPoint3x4(spriteObject.PivotWS);
                float near = -pivotVS.z - radius;
                float far = -pivotVS.z + radius;
                Matrix4x4 alignedProj = Matrix4x4.Ortho(
                    pivotVS.x - halfSizeWorld, pivotVS.x + halfSizeWorld,
                    pivotVS.y - halfSizeWorld, pivotVS.y + halfSizeWorld,
                    Mathf.Max(0.01f, near), far);

                var spriteColorDescriptor = cameraData.cameraTargetDescriptor;
                spriteColorDescriptor.width = spriteSizePx;
                spriteColorDescriptor.height = spriteSizePx;
                spriteColorDescriptor.useMipMap = false;
                spriteColorDescriptor.msaaSamples = 1;
                spriteColorDescriptor.depthBufferBits = 0;
                spriteColorDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;

                var spriteDepthDescriptor = spriteColorDescriptor;
                spriteDepthDescriptor.colorFormat = RenderTextureFormat.Depth;
                spriteDepthDescriptor.graphicsFormat = GraphicsFormat.None;
                spriteDepthDescriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;

                var spriteAlphaDescriptor = spriteColorDescriptor;
                spriteAlphaDescriptor.colorFormat = RenderTextureFormat.RGHalf;

                var spriteColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteColorDescriptor, $"BFP_Sprite_{spriteObject.name}", false, FilterMode.Point);
                var spriteMeta = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteColorDescriptor, $"BFP_SpriteMeta_{spriteObject.name}", false, FilterMode.Point);
                var spriteAlpha = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteAlphaDescriptor, $"BFP_SpriteAlpha_{spriteObject.name}", false, FilterMode.Point);
                var spriteDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteDepthDescriptor, $"BFP_SpriteDepth_{spriteObject.name}", false, FilterMode.Point);

                using (var builder = renderGraph.AddRasterRenderPass<SpritePassData>($"BF Pixelizer Sprite ({spriteObject.name})", out var passData))
                {
                    passData.Draws = new List<PixelizedSpriteObject.DrawEntry>(spriteObject.Draws);
                    passData.AlignedView = alignedView;
                    passData.AlignedProj = alignedProj;
                    passData.RestoreView = restoreView;
                    passData.RestoreProj = restoreProj;
                    passData.AmbientSh = _shConstants;

                    builder.SetRenderAttachment(spriteColor, 0, AccessFlags.Write);
                    builder.SetRenderAttachment(spriteMeta, 1, AccessFlags.Write);
                    builder.SetRenderAttachment(spriteAlpha, 2, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(spriteDepth, AccessFlags.Write);
                    // 그림자 수신 샘플링 의존 선언(RenderObjectsPass와 동일) — 미선언 시 RG 컴파일에 따라
                    // 바인딩이 무효가 되어 라이팅이 검게 나오는 불규칙 증상이 발생한다.
                    if (resourceData.mainShadowsTexture.IsValid())
                        builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);
                    if (resourceData.additionalShadowsTexture.IsValid())
                        builder.UseTexture(resourceData.additionalShadowsTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true); // 행렬·_BFP_ScreenSize 변경
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((SpritePassData data, RasterGraphContext context) =>
                    {
                        // ★ 근본 규칙: cmd.SetViewProjectionMatrices는 'CPU 규약(비GPU)' 행렬을 받아
                        // 엔진이 드로우 시점에 현재 타깃에 맞춰 자동 변환한다(y-플립·reversed-Z).
                        // GL.GetGPUProjectionMatrix를 함께 쓰면 '이중 변환'되어 상하 반전/깊이 역전이 발생한다.
                        // (URP 자신도 비GPU로 넘김 — ScriptableRenderer.cs:188. GPU 변환 행렬이 필요한 것은
                        //  RenderingUtils.SetViewAndProjectionMatrices 쪽이다.)

                        // 정렬 공간에서는 화면 격자 정점 스냅을 끈다(0 → 셰이더가 스킵).
                        context.cmd.SetGlobalVector(s_screenSizeProp, Vector4.zero);

                        // 앰비언트 SH 공급 — DrawRenderer 경로의 프로브 부재로 인한 '검은 앰비언트' 방지.
                        for (int i = 0; i < s_shProps.Length; i++)
                            context.cmd.SetGlobalVector(s_shProps[i], data.AmbientSh[i]);

                        context.cmd.SetViewProjectionMatrices(data.AlignedView, data.AlignedProj);
                        context.cmd.ClearRenderTarget(true, true, Color.clear);
                        foreach (PixelizedSpriteObject.DrawEntry draw in data.Draws)
                        {
                            if (draw.Renderer != null)
                                context.cmd.DrawRenderer(draw.Renderer, draw.Material, draw.SubMeshIndex, draw.PassIndex);
                        }
                        // 이후 패스를 위해 카메라 행렬 복원 — URP 원본 설정과 동일하게 비GPU 그대로.
                        context.cmd.SetViewProjectionMatrices(data.RestoreView, data.RestoreProj);
                    });
                }

                // ── 스프라이트 다운샘플(1/N) ──
                var spriteLowColorDescriptor = spriteColorDescriptor;
                spriteLowColorDescriptor.width = cellCount;
                spriteLowColorDescriptor.height = cellCount;
                var spriteLowAlphaDescriptor = spriteAlphaDescriptor;
                spriteLowAlphaDescriptor.width = cellCount;
                spriteLowAlphaDescriptor.height = cellCount;
                var spriteLowDepthDescriptor = spriteDepthDescriptor;
                spriteLowDepthDescriptor.width = cellCount;
                spriteLowDepthDescriptor.height = cellCount;

                var spriteLowColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteLowColorDescriptor, $"BFP_SpriteLow_{spriteObject.name}", false, FilterMode.Point);
                var spriteLowMeta = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteLowColorDescriptor, $"BFP_SpriteLowMeta_{spriteObject.name}", false, FilterMode.Point);
                var spriteLowAlpha = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteLowAlphaDescriptor, $"BFP_SpriteLowAlpha_{spriteObject.name}", false, FilterMode.Point);
                var spriteLowDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteLowDepthDescriptor, $"BFP_SpriteLowDepth_{spriteObject.name}", false, FilterMode.Point);

                using (var builder = renderGraph.AddRasterRenderPass<DownsamplePassData>($"BF Pixelizer Sprite Downsample ({spriteObject.name})", out var passData))
                {
                    passData.OffColor = spriteColor;
                    passData.OffMeta = spriteMeta;
                    passData.OffAlpha = spriteAlpha;
                    passData.OffDepth = spriteDepth;
                    passData.Material = _downsampleMaterial;
                    passData.CellSize = cellSizeRt;

                    builder.UseTexture(spriteColor, AccessFlags.Read);
                    builder.UseTexture(spriteMeta, AccessFlags.Read);
                    builder.UseTexture(spriteAlpha, AccessFlags.Read);
                    builder.UseTexture(spriteDepth, AccessFlags.Read);
                    builder.SetRenderAttachment(spriteLowColor, 0, AccessFlags.Write);
                    builder.SetRenderAttachment(spriteLowMeta, 1, AccessFlags.Write);
                    builder.SetRenderAttachment(spriteLowAlpha, 2, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(spriteLowDepth, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((DownsamplePassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(s_offMetaProp, data.OffMeta);
                        context.cmd.SetGlobalTexture(s_offAlphaProp, data.OffAlpha);
                        context.cmd.SetGlobalTexture(s_offDepthProp, data.OffDepth);
                        context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                        Blitter.BlitTexture(context.cmd, data.OffColor, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                    });
                }

                // ── 회전 배치 합성 ──
                // 피벗 NDC(비GPU, y위) — raster 변환·y부호는 렌더 함수에서 실제 UV 원점으로 결정.
                Vector4 pivotClip = restoreProj * (restoreView * new Vector4(spriteObject.PivotWS.x, spriteObject.PivotWS.y, spriteObject.PivotWS.z, 1f));
                Vector2 pivotNdc01 = new Vector2(pivotClip.x / pivotClip.w, pivotClip.y / pivotClip.w) * 0.5f + new Vector2(0.5f, 0.5f);

                using (var builder = renderGraph.AddRasterRenderPass<SpriteCompositePassData>($"BF Pixelizer Sprite Composite ({spriteObject.name})", out var passData))
                {
                    passData.LowColor = spriteLowColor;
                    passData.LowMeta = spriteLowMeta;
                    passData.LowAlpha = spriteLowAlpha;
                    passData.LowDepth = spriteLowDepth;
                    passData.Material = _compositeMaterial;
                    passData.CellSize = cellSizeRt;
                    passData.DepthEpsilon = _depthEpsilon;
                    passData.SpriteRot = new Vector4(roll.m00, roll.m01, roll.m10, roll.m11); // 뷰 공간 2x2 그대로
                    passData.PivotNdc01 = pivotNdc01;
                    passData.CenterCell = new Vector2(cellCount * 0.5f, cellCount * 0.5f);
                    passData.TargetSize = new Vector2(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height);
                    passData.DepthRange = new Vector4(Mathf.Max(0.01f, near), far, camera.nearClipPlane, camera.farClipPlane);

                    builder.UseTexture(spriteLowColor, AccessFlags.Read);
                    builder.UseTexture(spriteLowMeta, AccessFlags.Read);
                    builder.UseTexture(spriteLowAlpha, AccessFlags.Read);
                    builder.UseTexture(spriteLowDepth, AccessFlags.Read);
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((SpriteCompositePassData data, RasterGraphContext context) =>
                    {
                        // Play(RT 타깃) 실측: 엔진 자동 변환이 RT 렌더에 y-플립을 적용해 내용이 bottom-left(행0=월드 아래).
                        //  → 피벗 raster y = ndc01.y × H (2차 시도에서 위치 정합이 실측 검증된 공식),
                        //    raster→뷰 y부호 = +1, 뷰→스프라이트 행 y부호 = +1(스프라이트 버퍼도 동일 규약).
                        float pivotRasterY = data.PivotNdc01.y * data.TargetSize.y;
                        var spritePivot = new Vector4(data.PivotNdc01.x * data.TargetSize.x, pivotRasterY, data.CenterCell.x, data.CenterCell.y);
                        var spriteAxis = new Vector4(1f, 1f, 0f, 0f);

                        context.cmd.SetGlobalTexture(s_offMetaProp, data.LowMeta);
                        context.cmd.SetGlobalTexture(s_offAlphaProp, data.LowAlpha);
                        context.cmd.SetGlobalTexture(s_offDepthProp, data.LowDepth);
                        context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                        context.cmd.SetGlobalFloat(s_depthEpsProp, data.DepthEpsilon);
                        context.cmd.SetGlobalVector(s_spriteRotProp, data.SpriteRot);
                        context.cmd.SetGlobalVector(s_spritePivotProp, spritePivot);
                        context.cmd.SetGlobalVector(s_spriteAxisProp, spriteAxis);
                        context.cmd.SetGlobalVector(s_spriteDepthRangeProp, data.DepthRange);
                        Blitter.BlitTexture(context.cmd, data.LowColor, new Vector4(1f, 1f, 0f, 0f), data.Material, 2);
                    });
                }

                // ── 스프라이트 셀 깊이 → _CameraDepthTexture 되쓰기(물 수중 투영용, v2 Pass 4와 동일 역할) ──
                if (depthTexAttachable == false)
                    continue; // 깊이 포맷이 아니면 주입 불가(호출부에서 경고) — 합성까지는 정상 수행됨.

                using (var builder = renderGraph.AddRasterRenderPass<SpriteCompositePassData>($"BF Pixelizer Sprite DepthTex ({spriteObject.name})", out var passData))
                {
                    passData.LowColor = spriteLowColor;
                    passData.LowMeta = spriteLowMeta;
                    passData.LowDepth = spriteLowDepth;
                    passData.Material = _compositeMaterial;
                    passData.CellSize = cellSizeRt;
                    passData.SpriteRot = new Vector4(roll.m00, roll.m01, roll.m10, roll.m11);
                    passData.PivotNdc01 = pivotNdc01;
                    passData.CenterCell = new Vector2(cellCount * 0.5f, cellCount * 0.5f);
                    passData.TargetSize = new Vector2(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height);
                    passData.DepthRange = new Vector4(Mathf.Max(0.01f, near), far, camera.nearClipPlane, camera.farClipPlane);

                    builder.UseTexture(spriteLowColor, AccessFlags.Read);
                    builder.UseTexture(spriteLowMeta, AccessFlags.Read);
                    builder.UseTexture(spriteLowDepth, AccessFlags.Read);
                    builder.SetRenderAttachmentDepth(resourceData.cameraDepthTexture, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((SpriteCompositePassData data, RasterGraphContext context) =>
                    {
                        float pivotRasterY = data.PivotNdc01.y * data.TargetSize.y;
                        var spritePivot = new Vector4(data.PivotNdc01.x * data.TargetSize.x, pivotRasterY, data.CenterCell.x, data.CenterCell.y);
                        var spriteAxis = new Vector4(1f, 1f, 0f, 0f);

                        context.cmd.SetGlobalTexture(s_offMetaProp, data.LowMeta);
                        context.cmd.SetGlobalTexture(s_offDepthProp, data.LowDepth);
                        context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                        context.cmd.SetGlobalVector(s_spriteRotProp, data.SpriteRot);
                        context.cmd.SetGlobalVector(s_spritePivotProp, spritePivot);
                        context.cmd.SetGlobalVector(s_spriteAxisProp, spriteAxis);
                        context.cmd.SetGlobalVector(s_spriteDepthRangeProp, data.DepthRange);
                        Blitter.BlitTexture(context.cmd, data.LowColor, new Vector4(1f, 1f, 0f, 0f), data.Material, 3);
                    });
                }
            }
        }
    }
}
