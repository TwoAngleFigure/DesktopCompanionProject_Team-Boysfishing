using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DesktopCompanion.Rendering
{
    /// <summary>
    /// BF Pixelizer v2(계획 13) — 머티리얼 기반 오브젝트별 픽셀화 + 아웃라인.
    /// 대상 지정 = "BFPixelizer/PixelizedLit" 머티리얼(커스텀 LightMode 태그). 컴포넌트·레이어 불필요.
    ///
    /// V0(현재): 오프스크린 MRT 렌더(정점 스냅) → 풀해상도 합성(CellSize=1).
    ///  - 검증: 오브젝트가 화면에 보이고, 이동 시 N픽셀 스텝으로 움직이면 통과(크리프 해결의 선행 증거).
    /// V1: 두 패스 사이에 포인트 다운샘플 삽입(CellSize=N) → 실제 픽셀화.
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

        // 빌드 포함 보장을 위한 직렬화 참조(에디터에서 자동 할당).
        [SerializeField, HideInInspector] private Shader _compositeShader;
        [SerializeField, HideInInspector] private Shader _downsampleShader;

        private const string CompositeShaderName = "Hidden/BFPixelizer/Composite";
        private const string DownsampleShaderName = "Hidden/BFPixelizer/Downsample";

        private BFPixelizerPass _pass;
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
            _pass = new BFPixelizerPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_compositeMaterial == null || _downsampleMaterial == null)
            {
                Debug.LogWarning($"[BFPixelizer] 셰이더 로드 실패: {CompositeShaderName} / {DownsampleShaderName}");
                return;
            }

            _pass.Setup(_compositeMaterial, _downsampleMaterial, _pixelSize, _depthEpsilon, _autoPixelScale, _pixelScale);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth); // 가림 판정용 _CameraDepthTexture 보장
            renderer.EnqueuePass(_pass);
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
    /// 프레임당 2개 라스터 패스(V0): 오프스크린 MRT → 합성.
    /// 계획 11에서 검증된 RG 규칙 준수: 이벤트=정확히 AfterRenderingOpaques(300),
    /// raster pass 내 행렬 설정 금지, 전역 설정 pass는 AllowGlobalStateModification(true).
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
        private static readonly int s_offDepthProp = Shader.PropertyToID("_BFP_OffDepth");
        private static readonly int s_cellSizeProp = Shader.PropertyToID("_BFP_CellSize");
        private static readonly int s_depthEpsProp = Shader.PropertyToID("_BFP_DepthEps");

        private Material _compositeMaterial;
        private Material _downsampleMaterial;
        private int _pixelSize;
        private float _depthEpsilon;
        private bool _autoPixelScale;
        private float _manualPixelScale;

        public BFPixelizerPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public void Setup(Material compositeMaterial, Material downsampleMaterial, int pixelSize, float depthEpsilon, bool autoPixelScale, float manualPixelScale)
        {
            _compositeMaterial = compositeMaterial;
            _downsampleMaterial = downsampleMaterial;
            _pixelSize = Mathf.Clamp(pixelSize, 1, 5);
            _depthEpsilon = depthEpsilon;
            _autoPixelScale = autoPixelScale;
            _manualPixelScale = Mathf.Max(1f, manualPixelScale);
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
            public TextureHandle OffDepth;
            public Material Material;
            public float CellSize;
        }

        private class CompositePassData
        {
            public TextureHandle OffColor;
            public TextureHandle OffMeta;
            public TextureHandle OffDepth;
            public Material Material;
            public float CellSize;
            public float DepthEpsilon;
        }

        private class DepthTexUpdatePassData
        {
            public TextureHandle LowColor;
            public TextureHandle LowDepth;
            public Material Material;
            public float CellSize;
        }

        private class SpritePassData
        {
            public List<PixelizedSpriteObject.DrawEntry> Draws;
            public Matrix4x4 AlignedView;
            public Matrix4x4 AlignedProjGpu;
            public Matrix4x4 RestoreView;
            public Matrix4x4 RestoreProjGpu;
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

            var offColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorDescriptor, "BFP_OffColor", false, FilterMode.Point);
            var offMeta = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorDescriptor, "BFP_OffMeta", false, FilterMode.Point);
            var offDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDescriptor, "BFP_OffDepth", false, FilterMode.Point);

            // 픽셀 스케일: 자동 = 카메라 타깃 해상도 ÷ 화면 해상도(Play 중 World_RT=2, 에디트 모드 백버퍼=1).
            float pixelScale = _autoPixelScale
                ? Mathf.Clamp(Mathf.RoundToInt((float)cameraData.cameraTargetDescriptor.width / Mathf.Max(1, Screen.width)), 1, 4)
                : _manualPixelScale;

            // ── 저해상도(1/N) 버퍼 — 다운샘플 출력 ──
            int cellSizeRt = Mathf.Max(1, Mathf.RoundToInt(_pixelSize * pixelScale));
            var lowColorDescriptor = colorDescriptor;
            lowColorDescriptor.width = (colorDescriptor.width + cellSizeRt - 1) / cellSizeRt;
            lowColorDescriptor.height = (colorDescriptor.height + cellSizeRt - 1) / cellSizeRt;
            var lowDepthDescriptor = depthDescriptor;
            lowDepthDescriptor.width = lowColorDescriptor.width;
            lowDepthDescriptor.height = lowColorDescriptor.height;

            var lowColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowColorDescriptor, "BFP_LowColor", false, FilterMode.Point);
            var lowMeta = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowColorDescriptor, "BFP_LowMeta", false, FilterMode.Point);
            var lowDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowDepthDescriptor, "BFP_LowDepth", false, FilterMode.Point);

            // ── Pass 1: 대상 머티리얼(커스텀 태그) 오프스크린 렌더 ──
            var drawingSettings = CreateDrawingSettings(s_pixelizedTags, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
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
                builder.SetRenderAttachmentDepth(offDepth, AccessFlags.Write);
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
                passData.OffDepth = offDepth;
                passData.Material = _downsampleMaterial;
                passData.CellSize = cellSizeRt;

                builder.UseTexture(offColor, AccessFlags.Read);
                builder.UseTexture(offMeta, AccessFlags.Read);
                builder.UseTexture(offDepth, AccessFlags.Read);
                builder.SetRenderAttachment(lowColor, 0, AccessFlags.Write);
                builder.SetRenderAttachment(lowMeta, 1, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(lowDepth, AccessFlags.Write);
                builder.AllowGlobalStateModification(true); // _BFP_OffMeta/_BFP_OffDepth/_BFP_CellSize
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DownsamplePassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(s_offMetaProp, data.OffMeta);
                    context.cmd.SetGlobalTexture(s_offDepthProp, data.OffDepth);
                    context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                    Blitter.BlitTexture(context.cmd, data.OffColor, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                });
            }

            // ── Pass 3: 합성(CellSize=N — 저해상도 셀을 업스케일 + 아웃라인 + SV_Depth) ──
            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("BF Pixelizer Composite", out var passData))
            {
                passData.OffColor = lowColor;
                passData.OffMeta = lowMeta;
                passData.OffDepth = lowDepth;
                passData.Material = _compositeMaterial;
                passData.CellSize = cellSizeRt;
                passData.DepthEpsilon = _depthEpsilon;

                builder.UseTexture(lowColor, AccessFlags.Read);
                builder.UseTexture(lowMeta, AccessFlags.Read);
                builder.UseTexture(lowDepth, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                // discard 픽셀은 기존 값 유지가 필요하므로 ReadWrite(load) 바인딩.
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(s_offMetaProp, data.OffMeta);
                    context.cmd.SetGlobalTexture(s_offDepthProp, data.OffDepth);
                    context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                    context.cmd.SetGlobalFloat(s_depthEpsProp, data.DepthEpsilon);
                    Blitter.BlitTexture(context.cmd, data.OffColor, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
                });
            }

            // ── S0(계획 14): 오브젝트 공간 모드 — 정렬 공간 스프라이트 렌더 ──
            // 각 PixelizedSpriteObject를 "-θ 롤 정렬 뷰"의 전용 버퍼에 3D 렌더한다.
            // S0 검증: Frame Debugger에서 오브젝트가 회전 중에도 버퍼 안에서 수평(축 정렬)으로 보이면 통과.
            // (S0 단계에서는 화면 합성 없음 — 오브젝트는 기존 v2 경로로 계속 표시된다.)
            RecordSpriteObjectPasses(renderGraph, cameraData);

            // ── Pass 4: 셀 깊이를 _CameraDepthTexture에 되쓰기 ──
            // SW3 물의 수중 투영(반투명·흡수)은 _CameraDepthTexture로 "물 아래 무엇이 있는지"를
            // 판단한다. 커스텀 태그 오브젝트는 프리패스에 없으므로 여기서 블록 깊이를 주입한다.
            using (var builder = renderGraph.AddRasterRenderPass<DepthTexUpdatePassData>("BF Pixelizer DepthTex Update", out var passData))
            {
                passData.LowColor = lowColor;
                passData.LowDepth = lowDepth;
                passData.Material = _compositeMaterial;
                passData.CellSize = cellSizeRt;

                builder.UseTexture(lowColor, AccessFlags.Read);
                builder.UseTexture(lowDepth, AccessFlags.Read);
                builder.SetRenderAttachmentDepth(resourceData.cameraDepthTexture, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DepthTexUpdatePassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(s_offDepthProp, data.LowDepth);
                    context.cmd.SetGlobalFloat(s_cellSizeProp, data.CellSize);
                    Blitter.BlitTexture(context.cmd, data.LowColor, new Vector4(1f, 1f, 0f, 0f), data.Material, 1);
                });
            }
        }

        /// <summary>계획 14 S0: 스프라이트 모드 오브젝트별 정렬 공간 렌더 패스 기록.</summary>
        private void RecordSpriteObjectPasses(RenderGraph renderGraph, UniversalCameraData cameraData)
        {
            if (PixelizedSpriteObject.Active.Count == 0)
                return;
            Camera camera = cameraData.camera;
            if (camera.orthographic == false)
                return;

            // 메인 카메라와 같은 픽셀 밀도로 스프라이트 버퍼 해상도 산출.
            float pixelsPerWorldUnit = cameraData.cameraTargetDescriptor.height / (2f * camera.orthographicSize);

            // 복원용 카메라 행렬(정점 스냅과 동일 규약 — 계획 11 함정 ②: raster pass 안에서는 GPU 변환 수동).
            Matrix4x4 restoreView = cameraData.GetViewMatrix();
            Matrix4x4 restoreProjGpu = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);

            foreach (PixelizedSpriteObject spriteObject in PixelizedSpriteObject.Active)
            {
                if (spriteObject == null || spriteObject.Draws.Count == 0)
                    continue;

                // 화면면 회전각 θ: 오브젝트 up의 뷰 공간 성분. (부호/축은 S0 시각 검증으로 확정 — 계획 14)
                Vector3 upVS = restoreView.MultiplyVector(spriteObject.UpWS);
                float thetaDeg = Mathf.Atan2(upVS.x, upVS.y) * Mathf.Rad2Deg;

                // 정렬 뷰 = (-θ 롤) × 카메라 뷰. 이 공간에서 오브젝트 up은 항상 화면 위쪽.
                Matrix4x4 roll = Matrix4x4.Rotate(Quaternion.AngleAxis(thetaDeg, Vector3.forward));
                Matrix4x4 alignedView = roll * restoreView;

                // 피벗 중심의 소형 정사영(회전 불변 반경).
                float radius = spriteObject.GetRadiusWS();
                Vector3 pivotVS = alignedView.MultiplyPoint3x4(spriteObject.PivotWS);
                float near = -pivotVS.z - radius;
                float far = -pivotVS.z + radius;
                Matrix4x4 alignedProj = Matrix4x4.Ortho(
                    pivotVS.x - radius, pivotVS.x + radius,
                    pivotVS.y - radius, pivotVS.y + radius,
                    Mathf.Max(0.01f, near), far);
                Matrix4x4 alignedProjGpu = GL.GetGPUProjectionMatrix(alignedProj, true);

                int spriteSizePx = Mathf.Clamp(Mathf.CeilToInt(2f * radius * pixelsPerWorldUnit), 16, cameraData.cameraTargetDescriptor.height);

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

                var spriteColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteColorDescriptor, $"BFP_Sprite_{spriteObject.name}", false, FilterMode.Point);
                var spriteMeta = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteColorDescriptor, $"BFP_SpriteMeta_{spriteObject.name}", false, FilterMode.Point);
                var spriteDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, spriteDepthDescriptor, $"BFP_SpriteDepth_{spriteObject.name}", false, FilterMode.Point);

                using (var builder = renderGraph.AddRasterRenderPass<SpritePassData>($"BF Pixelizer Sprite ({spriteObject.name})", out var passData))
                {
                    passData.Draws = new List<PixelizedSpriteObject.DrawEntry>(spriteObject.Draws);
                    passData.AlignedView = alignedView;
                    passData.AlignedProjGpu = alignedProjGpu;
                    passData.RestoreView = restoreView;
                    passData.RestoreProjGpu = restoreProjGpu;

                    builder.SetRenderAttachment(spriteColor, 0, AccessFlags.Write);
                    builder.SetRenderAttachment(spriteMeta, 1, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(spriteDepth, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true); // 행렬·_BFP_ScreenSize 변경
                    builder.AllowPassCulling(false);            // S0: 출력 미소비(FD 검증용)
                    builder.SetRenderFunc((SpritePassData data, RasterGraphContext context) =>
                    {
                        // 정렬 공간에서는 화면 격자 정점 스냅을 끈다(0 → 셰이더가 스킵).
                        context.cmd.SetGlobalVector(s_screenSizeProp, Vector4.zero);
                        context.cmd.SetViewProjectionMatrices(data.AlignedView, data.AlignedProjGpu);
                        context.cmd.ClearRenderTarget(true, true, Color.clear);
                        foreach (PixelizedSpriteObject.DrawEntry draw in data.Draws)
                        {
                            if (draw.Renderer != null)
                                context.cmd.DrawRenderer(draw.Renderer, draw.Material, draw.SubMeshIndex, draw.PassIndex);
                        }
                        // 이후 패스를 위해 카메라 행렬 복원(RenderObjectsPass restoreCamera 패턴).
                        context.cmd.SetViewProjectionMatrices(data.RestoreView, data.RestoreProjGpu);
                    });
                }
            }
        }
    }
}
