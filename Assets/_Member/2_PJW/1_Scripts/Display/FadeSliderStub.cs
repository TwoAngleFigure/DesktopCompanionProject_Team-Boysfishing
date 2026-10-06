using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// uGUI 슬라이더로 대상 렌더러의 셰이더 _Fade(0=투명, 1=불투명)를 실시간 조절하는 확인용 컴포넌트.
    /// 값은 인스턴스 머티리얼에 직접 쓰며, 현재 값을 라벨에 표시한다.
    /// </summary>
    public class FadeSliderStub : MonoBehaviour
    {
        [Tooltip("전체화면 UI 캔버스 하위의 uGUI Slider")]
        [SerializeField] private Slider m_slider;

        [Tooltip("_Fade를 적용할 렌더러. 비우면 이 오브젝트 자식에서 자동 수집")]
        [SerializeField] private Renderer[] m_targets;

        [Tooltip("현재 값 표시(선택)")]
        [SerializeField] private TMP_Text m_label;

        [Tooltip("시작 값(0=투명, 1=불투명)")]
        [SerializeField, Range(0f, 1f)] private float m_startValue = 1f;

        private static readonly int s_fade = Shader.PropertyToID("_Fade");
        private Material[] m_mats;

        private void Awake()
        {
            if (m_targets == null || m_targets.Length == 0)
            {
                m_targets = GetComponentsInChildren<Renderer>(true);
            }
            // 인스턴스 머티리얼 확보(.material) — 다른 프로퍼티를 보존한 채 _Fade만 바꾼다.
            m_mats = new Material[m_targets.Length];
            for (int i = 0; i < m_targets.Length; i++)
            {
                m_mats[i] = m_targets[i] != null ? m_targets[i].material : null;
            }
        }

        private void Start()
        {
            if (m_slider == null)
            {
                Debug.LogWarning("[FadeSliderStub] Slider 미할당 — 전체화면 UI 캔버스에 Slider를 두고 연결하세요.");
                return;
            }

            m_slider.minValue = 0f;
            m_slider.maxValue = 1f;
            m_slider.wholeNumbers = false;
            m_slider.SetValueWithoutNotify(m_startValue);
            m_slider.onValueChanged.AddListener(Apply);

            Apply(m_startValue);
        }

        private void OnDestroy()
        {
            if (m_slider != null) m_slider.onValueChanged.RemoveListener(Apply);
        }

        private void Apply(float value)
        {
            value = Mathf.Clamp01(value);
            if (m_mats != null)
            {
                for (int i = 0; i < m_mats.Length; i++)
                {
                    if (m_mats[i] != null) m_mats[i].SetFloat(s_fade, value);
                }
            }
            if (m_label != null) m_label.text = $"Fade: {value:0.00}";
        }
    }
}
