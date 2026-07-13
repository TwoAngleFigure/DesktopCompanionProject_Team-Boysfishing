using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 탱크 Bounds 안을 랜덤 웨이포인트로 부드럽게 헤엄치는 경량 에이전트(물리/보이드 없음).
    /// <see cref="AquariumWorldView"/>가 스폰한 물고기 모델에 부착·초기화한다.
    /// </summary>
    public class AquariumFishAgent : MonoBehaviour
    {
        private Bounds m_bounds;
        private Vector3 m_target;
        private float m_speed;
        private float m_turnSpeed;
        private bool m_init;

        public void Init(Bounds bounds, float speed = 0.6f, float turnSpeed = 3f)
        {
            m_bounds = bounds;
            m_speed = Mathf.Max(0.01f, speed);
            m_turnSpeed = Mathf.Max(0.1f, turnSpeed);
            m_target = RandomPoint();
            m_init = true;
        }

        private void Update()
        {
            if (!m_init) return;

            Vector3 pos = transform.position;
            Vector3 to = m_target - pos;
            if (to.sqrMagnitude < 0.04f)   // 도달 → 다음 목표
            {
                m_target = RandomPoint();
                to = m_target - pos;
            }

            Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, m_turnSpeed * Time.deltaTime);
            transform.position = pos + transform.forward * (m_speed * Time.deltaTime);
        }

        private Vector3 RandomPoint()
            => new Vector3(
                Random.Range(m_bounds.min.x, m_bounds.max.x),
                Random.Range(m_bounds.min.y, m_bounds.max.y),
                Random.Range(m_bounds.min.z, m_bounds.max.z));
    }
}
