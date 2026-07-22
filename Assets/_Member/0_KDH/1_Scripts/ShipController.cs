using UnityEngine;

namespace DesktopCompanion.Controllers
{
    public class ShipController : MonoBehaviour
    {
        [Header("항해 속도 (초당 X 이동량)")]
        [Tooltip("맵 간의 거리와 도착 시간에 맞춰 이 속도를 조절하세요.")]
        public float m_speed = 5f;

        private bool m_isTraveling = false;

        public void SetTraveling(bool isTraveling)
        {
            m_isTraveling = isTraveling;
        }

        public void ResetToOrigin()
        {
            transform.position = new Vector3(0f, transform.position.y, transform.position.z);
            Debug.Log("[ShipController] 배 좌표 원점 리셋 완료. 새로운 항해 준비!");
        }

        private void Update()
        {
            if (m_isTraveling)
            {
                transform.Translate(Vector3.left * m_speed * Time.deltaTime);
            }
        }
    }
}