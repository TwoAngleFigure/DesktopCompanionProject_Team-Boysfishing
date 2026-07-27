using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 루트(카메라+탱크+물고기)를 SunSource(=URP 메인 라이트=물 라이트) 방향에 실시간 동기화 회전시킨다.
    /// SW3 물은 메인 라이트를 공유하고 바다가 SunSource 기준이므로, 태양은 그대로 두고 아쿠아리움 쪽만 회전한다.
    /// 카메라가 루트에 포함되어 함께 회전하므로 RT 구도(카메라-탱크 상대 자세)는 유지된다.
    /// LateUpdate에서 적용해 라이트/애니메이션 갱신 이후 회전을 반영한다.
    ///
    /// Offset은 손으로 맞추기 비직관적이므로, 인스펙터에 배치해둔 현재 각도에서 역산하는 기능을 제공한다
    /// (컴포넌트 우클릭 메뉴 "현재 배치로 Offset 계산"). 계산식: offset = Inverse(sun.rot) * root.rot.
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
        /// 현재 씬에 배치된 루트 각도와 태양 각도로부터 Offset을 역산해 채운다.
        /// 지금의 배치 상태를 그대로 보존하면서(런타임에 root.rotation이 현재값과 동일해짐), 태양만 따라 상대 회전하게 된다.
        /// 에디트 모드에서 루트를 원하는 각도로 배치한 뒤 실행하면 된다.
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

        /// <summary>Offset을 0으로 초기화(태양 각도를 그대로 사용).</summary>
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
