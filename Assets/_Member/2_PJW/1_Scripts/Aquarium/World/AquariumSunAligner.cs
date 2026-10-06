using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 루트(카메라·탱크·물고기)를 태양(SunSource = URP 메인 라이트) 방향에 상대 오프셋만큼 회전시킨다.
    /// LateUpdate에서 적용하므로 라이트·애니메이션 갱신 이후의 각도가 반영된다.
    /// 카메라가 루트에 포함되어 함께 회전하므로 카메라-탱크의 상대 자세는 유지된다.
    /// 컨텍스트 메뉴로 현재 배치에서 오프셋을 역산하거나 0으로 초기화할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AquariumSunAligner : MonoBehaviour
    {
        [Tooltip("회전 대상 루트(비우면 이 오브젝트). 카메라·탱크·물고기 루트를 포함해야 한다.")]
        [SerializeField] private Transform m_root;

        [Tooltip("물 라이트(메인). 비우면 RenderSettings.sun(Lighting 창 Sun Source) 사용.")]
        [SerializeField] private Light m_sun;

        [Tooltip("태양 방향 대비 상대 오프셋. 우클릭 메뉴 '현재 배치로 Offset 계산'으로 역산 권장.")]
        [SerializeField] private Vector3 m_offsetEuler = Vector3.zero;

        private Transform Root => m_root != null ? m_root : transform;
        private Light Sun => m_sun != null ? m_sun : RenderSettings.sun;

        private void LateUpdate()
        {
            Light sun = Sun;
            if (sun == null) return;
            Root.rotation = sun.transform.rotation * Quaternion.Euler(m_offsetEuler);
        }

        /// <summary>
        /// 현재 루트 각도와 태양 각도로부터 Offset을 역산해 채운다(offset = Inverse(sun.rotation) * root.rotation).
        /// 적용 후 런타임 루트 각도가 현재 배치와 일치한다.
        /// </summary>
        [ContextMenu("현재 배치로 Offset 계산")]
        private void CaptureOffsetFromCurrent()
        {
            Light sun = Sun;
            if (sun == null)
            {
                Debug.LogWarning("[AquariumSunAligner] Sun이 없어 Offset을 계산할 수 없습니다. m_sun을 지정하거나 Lighting 창 Sun Source를 설정하세요.");
                return;
            }

            Quaternion offset = Quaternion.Inverse(sun.transform.rotation) * Root.rotation;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Capture Aquarium Sun Offset");
#endif
            m_offsetEuler = offset.eulerAngles;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[AquariumSunAligner] 현재 배치 기준 Offset 계산: {m_offsetEuler} (sun={sun.name})");
        }

        /// <summary>Offset을 0으로 초기화한다. 루트가 태양 각도를 그대로 따른다.</summary>
        [ContextMenu("Offset 초기화(0)")]
        private void ResetOffset()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Reset Aquarium Sun Offset");
#endif
            m_offsetEuler = Vector3.zero;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
