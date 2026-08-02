using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 탱크 로컬 공간(<see cref="AquariumTankRenderer.TankSpace"/>) 안을 랜덤 웨이포인트로 헤엄치는 에이전트.
    /// 목표 지점을 로컬 좌표로 잡으므로 루트가 회전해도 탱크 범위 안에 머문다.
    /// 모델은 rotation(0,0,0)에서 -Y가 정면·+Z가 상방인 규약을 따르며, <see cref="ForwardOffset"/>로 보정한다.
    /// <see cref="AquariumWorldView"/>가 스폰한 물고기 모델에 부착·초기화한다.
    /// </summary>
    public class AquariumFishAgent : MonoBehaviour
    {
        // 모델 -Y(정면)/+Z(상방)를 표준(LookRotation: +Z 전방/+Y 상방)으로 보내는 고정 오프셋.
        // 검산: Euler(-90,0,0)는 (0,-1,0)→(0,0,1), (0,0,1)→(0,1,0).
        private static readonly Quaternion ForwardOffset = Quaternion.Euler(-90f, 0f, 0f);

        private Transform m_space;       // 탱크 로컬 공간 기준(TankSpace)
        private Bounds m_localBounds;    // 로컬 헤엄 박스(LocalBounds)
        private Vector3 m_localTarget;
        private Vector3 m_baseScale = Vector3.one;   // 프리팹 원본 스케일
        private float m_speed;
        private float m_turnSpeed;
        private bool m_init;

        public void Init(Transform space, Bounds localBounds, Vector3 baseScale, float size,
                         float speed = 0.6f, float turnSpeed = 3f)
        {
            m_space = space;
            m_localBounds = localBounds;
            m_baseScale = baseScale;
            m_speed = Mathf.Max(0.01f, speed);
            m_turnSpeed = Mathf.Max(0.1f, turnSpeed);
            m_localTarget = RandomPoint();
            SetSize(size);
            m_init = true;
        }

        /// <summary>스케일을 기준 스케일 × Size로 설정한다. Size가 0 이하이면 1로 폴백한다.</summary>
        public void SetSize(float size)
            => transform.localScale = m_baseScale * (size > 0f ? size : 1f);

        private void Update()
        {
            if (!m_init || m_space == null) return;

            float dt = Time.deltaTime;
            Vector3 localPos = m_space.InverseTransformPoint(transform.position);
            Vector3 to = m_localTarget - localPos;
            if (to.sqrMagnitude < 0.04f)   // 도달 → 다음 목표(로컬)
            {
                m_localTarget = RandomPoint();
                to = m_localTarget - localPos;
            }

            // 목표를 향한 자세: 상방=탱크 로컬 up, 모델 정면 오프셋 적용.
            Vector3 worldDir = m_space.TransformDirection(to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.up);
            Quaternion look = Quaternion.LookRotation(worldDir, m_space.up) * ForwardOffset;
            transform.rotation = Quaternion.Slerp(transform.rotation, look, m_turnSpeed * dt);

            // 현재 향한 모델 전방(-Y)의 월드 방향으로 전진.
            transform.position += (transform.rotation * Vector3.down) * (m_speed * dt);
        }

        private Vector3 RandomPoint()
            => new Vector3(
                Random.Range(m_localBounds.min.x, m_localBounds.max.x),
                Random.Range(m_localBounds.min.y, m_localBounds.max.y),
                Random.Range(m_localBounds.min.z, m_localBounds.max.z));
    }
}
