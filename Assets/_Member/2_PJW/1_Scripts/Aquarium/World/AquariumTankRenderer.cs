using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 3D 탱크의 RT 프로듀서. 전용 카메라를 투명 RenderTexture에 상시 렌더하고,
    /// 헤엄 범위(로컬 박스)와 탱크 로컬 공간 Transform을 에이전트·월드 뷰에 제공한다.
    /// 컨슈머는 <see cref="TankTexture"/>를 바인딩해 표시한다.
    /// 창 개폐·표시 모드와 무관하게 항상 렌더되어야 하므로 오브젝트를 비활성화하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AquariumTankRenderer : MonoBehaviour
    {
        [Tooltip("Aquarium 레이어 전용 카메라. cullingMask는 Aquarium만.")]
        [SerializeField] private Camera m_camera;

        [Tooltip("RenderTexture 해상도. RawImage 종횡비와 맞추면 왜곡이 없다.")]
        [SerializeField] private Vector2Int m_resolution = new Vector2Int(1024, 1024);

        [Tooltip("헤엄 범위(월드). center는 이 오브젝트 기준 오프셋.")]
        [SerializeField] private Vector3 m_boundsCenter = Vector3.zero;
        [SerializeField] private Vector3 m_boundsSize = new Vector3(6f, 3f, 3f);

        [Tooltip("배경 투명(true) 여부. false면 카메라 SolidColor 배경 유지.")]
        [SerializeField] private bool m_transparent = true;

        private RenderTexture m_rt;

        /// <summary>컨슈머가 바인딩할 탱크 렌더 텍스처.</summary>
        public RenderTexture TankTexture => m_rt;

        /// <summary>로컬 헤엄 박스. 에이전트가 이 범위에서 목표 지점을 샘플링한다.</summary>
        public Bounds LocalBounds => new Bounds(m_boundsCenter, m_boundsSize);

        /// <summary>탱크 로컬 공간의 기준 Transform. 루트 회전이 반영된다.</summary>
        public Transform TankSpace => transform;

        /// <summary>
        /// 루트 회전·스케일을 반영한 월드 AABB. 로컬 박스의 8코너를 월드로 변환해 감싼 값이다.
        /// </summary>
        public Bounds TankBounds
        {
            get
            {
                Vector3 c = m_boundsCenter, e = m_boundsSize * 0.5f;
                Matrix4x4 m = transform.localToWorldMatrix;
                var b = new Bounds(m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z)), Vector3.zero);
                b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(e.x, -e.y, -e.z)));
                b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x, e.y, -e.z)));
                b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(e.x, e.y, -e.z)));
                b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, e.z)));
                b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(e.x, -e.y, e.z)));
                b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(-e.x, e.y, e.z)));
                b.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(e.x, e.y, e.z)));
                return b;
            }
        }

        private void Awake()
        {
            if (m_camera == null)
            {
                Debug.LogError("[AquariumTankRenderer] 카메라 미할당");
                return;
            }

            int w = Mathf.Max(1, m_resolution.x);
            int h = Mathf.Max(1, m_resolution.y);
            m_rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "Aquarium_RT" };
            m_rt.Create();

            m_camera.targetTexture = m_rt;   // 카메라 종횡비는 RT를 따른다.
            if (m_transparent)
            {
                m_camera.clearFlags = CameraClearFlags.SolidColor;
                m_camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
        }

        private void OnDestroy()
        {
            if (m_camera != null) m_camera.targetTexture = null;
            if (m_rt != null) { m_rt.Release(); Destroy(m_rt); }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 로컬 박스를 루트 회전·스케일에 맞춰 그린다(회전한 헤엄 범위 확인용).
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireCube(m_boundsCenter, m_boundsSize);
        }
#endif
    }
}
