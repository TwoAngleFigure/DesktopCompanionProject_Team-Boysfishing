using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Rendering
{
    /// <summary>
    /// BF Pixelizer의 오브젝트 공간(스프라이트) 모드를 지정하는 컴포넌트.
    /// 부착한 계층 전체가 하나의 회전 추종 픽셀 오브젝트로 처리되어,
    /// 정렬 공간 전용 버퍼에 렌더 → 다운샘플 → 회전·배치 합성 경로를 탄다.
    /// 활성 상태를 정적 목록에 등록해 렌더 피처가 순회하게 하고, 화면 격자 경로에서 제외되도록
    /// 하위 렌더러의 renderingLayerMask를 <see cref="RenderingLayerBit"/>으로 교체했다가 비활성화 시 복원한다.
    /// 하위 렌더러는 BFPixelizer/PixelizedLit 머티리얼을 사용해야 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PixelizedSpriteObject : MonoBehaviour
    {
        private const string ForwardPassName = "BFPixelizedForward";

        /// <summary>스프라이트 모드 렌더러를 표시하는 렌더링 레이어 비트. 화면 격자 경로에서 제외하는 데 쓴다.</summary>
        public const uint RenderingLayerBit = 1u << 31;

        public struct DrawEntry
        {
            public Renderer Renderer;
            public Material Material;
            public int SubMeshIndex;
            public int PassIndex;
        }

        private static readonly List<PixelizedSpriteObject> s_active = new();

        /// <summary>활성 스프라이트 모드 오브젝트 목록. 렌더 피처가 순회한다.</summary>
        public static IReadOnlyList<PixelizedSpriteObject> Active => s_active;

        private readonly List<DrawEntry> _draws = new();
        private Renderer[] _renderers;
        private uint[] _originalRenderingLayers;
        private int _rendererLayerMask;

        /// <summary>계층 렌더러들의 레이어 비트 합집합. 카메라 Culling Mask와 대조하는 데 쓴다.</summary>
        public int RendererLayerMask => _rendererLayerMask;

        /// <summary>격자 원점의 월드 좌표.</summary>
        public Vector3 PivotWS => transform.position;

        /// <summary>화면면 회전각을 추출할 때 기준으로 삼는 축.</summary>
        public Vector3 UpWS => transform.up;

        public IReadOnlyList<DrawEntry> Draws => _draws;

        private void OnEnable()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _draws.Clear();

            foreach (Renderer r in _renderers)
            {
                Material[] materials = r.sharedMaterials;
                for (int sub = 0; sub < materials.Length; sub++)
                {
                    Material mat = materials[sub];
                    if (mat == null) continue;
                    int passIndex = mat.FindPass(ForwardPassName);
                    if (passIndex < 0) continue; // PixelizedLit 머티리얼만 대상
                    _draws.Add(new DrawEntry { Renderer = r, Material = mat, SubMeshIndex = sub, PassIndex = passIndex });
                }
            }

            if (_draws.Count == 0)
                Debug.LogWarning("[BFPixelizer] 계층에 PixelizedLit 머티리얼 렌더러가 없습니다.", this);

            // 화면 격자 경로에서 제외 — 스프라이트 합성과의 이중 표시 방지.
            _originalRenderingLayers = new uint[_renderers.Length];
            _rendererLayerMask = 0;
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalRenderingLayers[i] = _renderers[i].renderingLayerMask;
                _renderers[i].renderingLayerMask = RenderingLayerBit;
                _rendererLayerMask |= 1 << _renderers[i].gameObject.layer;
            }

            s_active.Add(this);
        }

        private void OnDisable()
        {
            s_active.Remove(this);
            if (_renderers == null || _originalRenderingLayers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].renderingLayerMask = _originalRenderingLayers[i];
            }
        }

        /// <summary>
        /// 컬링용 월드 AABB. 피벗 중심 · 반경 <see cref="GetRadiusWS"/>의 구를 감싸는 상자이며,
        /// 합성이 픽셀을 남길 수 있는 최대 범위와 일치한다.
        /// </summary>
        public Bounds GetCullingBounds()
        {
            float diameter = GetRadiusWS() * 2f;
            return new Bounds(PivotWS, new Vector3(diameter, diameter, diameter));
        }

        /// <summary>피벗 기준 월드 바운딩 반경. 어떤 회전에도 오브젝트를 담는 스프라이트 버퍼 크기 산출에 쓴다.</summary>
        public float GetRadiusWS()
        {
            float radius = 0f;
            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;
                Bounds b = r.bounds;
                float d = (b.center - transform.position).magnitude + b.extents.magnitude;
                if (d > radius) radius = d;
            }
            return Mathf.Max(0.01f, radius);
        }
    }
}
