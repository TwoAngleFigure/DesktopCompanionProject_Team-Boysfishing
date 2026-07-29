using DG.Tweening;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 페이드 셰이더(DitherFadeLit / DitherFadeLitSmooth / FadeLitAlpha)의 공통 프로퍼티
    /// _Fade(0=투명, 1=불투명)를 DOTween으로 전환한다.
    /// DOFade·DOFadeIn·DOFadeOut은 <see cref="Tween"/>을 반환하므로 Sequence에 조합할 수 있다.
    /// <code>
    /// DOTween.Sequence()
    ///        .Append(fade.DOFadeIn(0.4f))
    ///        .AppendInterval(1f)
    ///        .Append(fade.DOFadeOut(0.4f));
    /// </code>
    /// _Fade는 MaterialPropertyBlock이 아니라 인스턴스 머티리얼에 직접 쓴다.
    /// MPB는 SRP Batcher 호환 셰이더에서 나머지 UnityPerMaterial 프로퍼티를 0으로 만들어 모델이 검게 렌더된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DitherFade : MonoBehaviour
    {
        [Tooltip("대상 렌더러. 비우면 자식에서 자동 수집")]
        [SerializeField] private Renderer[] m_renderers;

        [Tooltip("인자 없는 DOFade*/자동 페이드의 기본 소요 시간(초)")]
        [SerializeField, Min(0.01f)] private float m_duration = 0.4f;

        [Tooltip("페이드 이징")]
        [SerializeField] private Ease m_ease = Ease.Linear;

        [Tooltip("활성화 시 자동으로 0→1 페이드 인")]
        [SerializeField] private bool m_fadeInOnEnable = true;

        private static readonly int s_fade = Shader.PropertyToID("_Fade");
        private Material[] m_mats;
        private float m_current = 1f;

        /// <summary>현재 _Fade 값(0~1).</summary>
        public float Fade => m_current;

        /// <summary>기본 페이드 소요 시간(초).</summary>
        public float Duration => m_duration;

        private void Awake()
        {
            if (m_renderers == null || m_renderers.Length == 0)
            {
                m_renderers = GetComponentsInChildren<Renderer>(true);
            }
            // 인스턴스 머티리얼에 직접 _Fade를 쓴다(MPB 불가 — 위 주석 참고).
            m_mats = new Material[m_renderers.Length];
            for (int i = 0; i < m_renderers.Length; i++)
            {
                m_mats[i] = m_renderers[i] != null ? m_renderers[i].material : null;
            }

            SetFade(m_fadeInOnEnable ? 0f : 1f);
        }

        private void OnEnable()
        {
            if (m_fadeInOnEnable)
            {
                // 재활성화 누적 방지: 진행 중인 자체 트윈만 정리하고 새로 시작.
                DOTween.Kill(this);
                SetFade(0f);
                DOFadeIn();
            }
        }

        private void OnDestroy()
        {
            // SetLink로 GameObject 파괴 시 자동 정리되지만, 명시적으로 한 번 더 방어.
            DOTween.Kill(this);
        }

        // ── DOTween: Sequence 조합용 Tween 반환 API ─────────────────────────────

        /// <summary>_Fade를 endValue(0~1)로 전환하는 Tween을 반환한다.</summary>
        public Tween DOFade(float endValue, float duration)
        {
            return DOTween.To(() => m_current, SetFade, Mathf.Clamp01(endValue), Mathf.Max(0.0001f, duration))
                          .SetEase(m_ease)
                          .SetTarget(this)      // DOTween.Kill(this)로 일괄 제어
                          .SetLink(gameObject); // GameObject 파괴 시 자동 Kill
        }

        /// <summary>기본 소요 시간으로 _Fade를 endValue로 전환하는 Tween을 반환한다.</summary>
        public Tween DOFade(float endValue) => DOFade(endValue, m_duration);

        /// <summary>불투명(1)으로 페이드 인하는 Tween을 반환한다.</summary>
        public Tween DOFadeIn(float duration) => DOFade(1f, duration);

        /// <summary>기본 소요 시간으로 페이드 인하는 Tween을 반환한다.</summary>
        public Tween DOFadeIn() => DOFade(1f, m_duration);

        /// <summary>투명(0)으로 페이드 아웃하는 Tween을 반환한다.</summary>
        public Tween DOFadeOut(float duration) => DOFade(0f, duration);

        /// <summary>기본 소요 시간으로 페이드 아웃하는 Tween을 반환한다.</summary>
        public Tween DOFadeOut() => DOFade(0f, m_duration);

        // ── 즉시 제어 ──────────────────────────────────────────────────────────

        /// <summary>진행 중인 페이드를 중단하고 값을 즉시 설정한다.</summary>
        public void SetFadeImmediate(float value)
        {
            DOTween.Kill(this);
            SetFade(value);
        }

        /// <summary>_Fade를 지정 값으로 모든 인스턴스 머티리얼에 적용한다.</summary>
        public void SetFade(float value)
        {
            m_current = Mathf.Clamp01(value);
            if (m_mats == null) return;
            for (int i = 0; i < m_mats.Length; i++)
            {
                if (m_mats[i] != null) m_mats[i].SetFloat(s_fade, m_current);
            }
        }
    }
}
