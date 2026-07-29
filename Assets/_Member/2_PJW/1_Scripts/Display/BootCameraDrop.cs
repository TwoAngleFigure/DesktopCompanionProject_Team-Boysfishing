using DG.Tweening;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 부팅 완료 시 카메라를 지정 높이에서 최종 위치로 한 번 내린다.
    /// 정해진 시간 안에 부팅 완료 신호가 오지 않으면 스스로 하강한다.
    ///
    /// 부착 대상은 카메라 자신이 아니라 부모인 카메라 루트다.
    /// 카메라 로컬 좌표는 <see cref="CameraAspectAnchor"/>가 소유하므로,
    /// 거기에 트윈을 걸면 해상도·모니터 변경 시 Apply()가 값을 덮어쓴다.
    ///
    /// WorldViewBase를 상속해 Bind()를 부팅 완료 신호로 쓰며, 주입되는 의존성은 사용하지 않는다.
    /// 이동은 Y 축만 다룬다. 카메라 X를 기준으로 동작하는 시스템이 있어 X를 흔들면 프롭 스폰·파괴가 어긋난다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BootCameraDrop : WorldViewBase
    {
        [Tooltip("시작 시 최종 위치보다 얼마나 위에서 시작할지(월드 유닛). 세로 시야가 20유닛이므로 " +
                 "이 값이 클수록 수면·배가 늦게 등장한다.")]
        [SerializeField, Min(0f)] private float m_height = 14f;

        [Tooltip("하강 시간(초)")]
        [SerializeField, Min(0.01f)] private float m_duration = 1.6f;

        [SerializeField] private Ease m_ease = Ease.OutCubic;

        [Tooltip("이 시간(초) 안에 부팅 완료가 오지 않으면 스스로 하강한다. 프리로드 실패 시 " +
                 "카메라가 위에 머물러 월드가 보이지 않는 것을 막는 안전장치. 0이면 끈다.")]
        [SerializeField, Min(0f)] private float m_failsafeSeconds = 8f;

        private Vector3 m_basePosition;   // 씬 저작값 = 최종 위치
        private bool m_played;
        private float m_readyTime;

        // 캡처와 시작 포즈를 Start가 아닌 Awake에서 한다.
        // GameManager.Start의 await가 동기 완료되면 Bind()가 이 컴포넌트의 Start보다 먼저 올 수 있고,
        // 그 경우 Start가 하강이 끝난 카메라를 다시 위로 올려버린다. Awake는 모든 Start보다 앞선다.
        private void Awake()
        {
            m_basePosition = transform.localPosition;
            transform.localPosition = m_basePosition + Vector3.up * m_height;
            m_readyTime = Time.unscaledTime;
        }

        /// <summary>부팅 완료 시 WorldManager가 호출한다. 하강을 시작한다.</summary>
        public override void Bind() => Play();

        public override void Unbind()
        {
            // 중간에 꺼져도 카메라가 공중에 남지 않게 최종 위치로 확정한다.
            transform.DOKill();
            transform.localPosition = m_basePosition;
        }

        private void Update()
        {
            if (m_played || m_failsafeSeconds <= 0f)
            {
                return;
            }
            if (Time.unscaledTime - m_readyTime < m_failsafeSeconds)
            {
                return;
            }

            Debug.LogWarning($"[BootCameraDrop] {m_failsafeSeconds}초 안에 부팅 완료가 오지 않아 강제로 하강한다 " +
                             "(프리로드 실패 가능성).", this);
            Play();
        }

        private void Play()
        {
            if (m_played)
            {
                return;   // 재활성화로 Bind가 다시 와도 1회만
            }
            m_played = true;

            transform.DOKill();
            transform.DOLocalMoveY(m_basePosition.y, m_duration)
                     .SetEase(m_ease)
                     .SetUpdate(true)        // timeScale 무관
                     .SetLink(gameObject);   // 파괴 시 자동 정리
        }
    }
}
