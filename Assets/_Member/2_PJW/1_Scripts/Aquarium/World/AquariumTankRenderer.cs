using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 3D 탱크의 RT 프로듀서. 전용 카메라(cullingMask=Aquarium)를 투명 RenderTexture에 상시 렌더한다.
    /// 표현(컨슈머)은 이 <see cref="TankTexture"/>를 바인딩만 한다: 지금=UI 창 RawImage, 이후=월페이퍼(전체화면 배경).
    /// 창 개폐·표시 모드와 무관하게 항상 렌더되어야 헤엄/미리보기가 유지된다(오브젝트를 비활성화하지 말 것).
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

        /// <summary>컨슈머(RawImage/월페이퍼)가 바인딩할 렌더 텍스처.</summary>
        public RenderTexture TankTexture => m_rt;

        /// <summary>헤엄 범위(월드 좌표). 이 오브젝트 위치에 오프셋을 더한 박스.</summary>
        public Bounds TankBounds => new Bounds(transform.position + m_boundsCenter, m_boundsSize);

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
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireCube(transform.position + m_boundsCenter, m_boundsSize);
        }
#endif
    }
}
