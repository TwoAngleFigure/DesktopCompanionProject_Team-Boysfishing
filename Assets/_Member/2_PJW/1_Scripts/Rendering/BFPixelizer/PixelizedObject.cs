using UnityEngine;

namespace DesktopCompanion.Rendering
{
    /// <summary>
    /// BF Pixelizer 대상 지정 컴포넌트(계획 12).
    /// 부착하면 하위 렌더러들을 픽셀화 레이어로 옮기고, 매 프레임 MPB로
    /// ID·픽셀 크기·월드 피벗을 메타 패스 셰이더에 전달한다.
    /// 오브젝트의 기존 머티리얼은 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PixelizedObject : MonoBehaviour
    {
        /// <summary>픽셀화 대상 레이어 이름(Project Settings > Tags and Layers에 추가 필요).</summary>
        public const string PixelatedLayerName = "Pixelated";

        [Tooltip("매크로 픽셀 한 변의 크기(스크린 픽셀). 격자는 이 오브젝트의 피벗에 고정된다.")]
        [Range(1, 5)]
        [SerializeField] private int _pixelSize = 3;

        [Tooltip("아웃라인 색(P3에서 사용).")]
        [SerializeField] private Color _outlineColor = Color.black;

        private static readonly int s_idProp = Shader.PropertyToID("_BFP_ID");
        private static readonly int s_pixelSizeProp = Shader.PropertyToID("_BFP_PixelSize");
        private static readonly int s_pivotWsProp = Shader.PropertyToID("_BFP_PivotWS");

        private static int s_nextId = 1;

        private int _id;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private int _originalLayer;

        public int Id => _id;
        public Color OutlineColor => _outlineColor;

        private void OnEnable()
        {
            _id = s_nextId;
            s_nextId = s_nextId >= 255 ? 1 : s_nextId + 1;

            _mpb = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<Renderer>();

            int layer = LayerMask.NameToLayer(PixelatedLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[BFPixelizer] '{PixelatedLayerName}' 레이어가 없습니다. Project Settings > Tags and Layers에 추가하세요.", this);
                return;
            }

            _originalLayer = gameObject.layer;
            foreach (Renderer r in _renderers)
                r.gameObject.layer = layer;
        }

        private void OnDisable()
        {
            if (_renderers == null) return;
            foreach (Renderer r in _renderers)
            {
                if (r != null)
                    r.gameObject.layer = _originalLayer;
            }
        }

        private void LateUpdate()
        {
            // 피벗(=transform.position)은 매 프레임 갱신 — 격자가 오브젝트를 따라가는 것이 크리프 방지의 핵심.
            foreach (Renderer r in _renderers)
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(s_idProp, _id);
                _mpb.SetFloat(s_pixelSizeProp, _pixelSize);
                _mpb.SetVector(s_pivotWsProp, transform.position);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
