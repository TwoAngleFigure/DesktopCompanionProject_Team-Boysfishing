using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Rendering
{
    /// <summary>
    /// BF Pixelizer 오브젝트 공간 모드(계획 14) 지정 컴포넌트.
    /// 부착한 계층 전체가 "하나의 회전 추종 픽셀 오브젝트"로 처리된다:
    /// 정렬 공간 전용 버퍼에 3D 렌더 → 다운샘플 → 회전·배치 합성(픽셀이 오브젝트와 함께 회전).
    /// 하위 렌더러는 BFPixelizer/PixelizedLit 머티리얼을 사용해야 한다(외형·아웃라인 설정은 머티리얼).
    /// </summary>
    [DisallowMultipleComponent]
    public class PixelizedSpriteObject : MonoBehaviour
    {
        private const string ForwardPassName = "BFPixelizedForward";

        public struct DrawEntry
        {
            public Renderer Renderer;
            public Material Material;
            public int SubMeshIndex;
            public int PassIndex;
        }

        private static readonly List<PixelizedSpriteObject> s_active = new();

        /// <summary>활성 스프라이트 모드 오브젝트 목록(렌더 피처가 순회).</summary>
        public static IReadOnlyList<PixelizedSpriteObject> Active => s_active;

        private readonly List<DrawEntry> _draws = new();
        private Renderer[] _renderers;

        /// <summary>격자 원점(월드) = transform.position.</summary>
        public Vector3 PivotWS => transform.position;

        /// <summary>화면면 회전각 추출 기준 축.</summary>
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

            s_active.Add(this);
        }

        private void OnDisable()
        {
            s_active.Remove(this);
        }

        /// <summary>피벗 기준 바운딩 반경(월드) — 어떤 회전에도 오브젝트를 담는 스프라이트 버퍼 크기 산출용.</summary>
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
