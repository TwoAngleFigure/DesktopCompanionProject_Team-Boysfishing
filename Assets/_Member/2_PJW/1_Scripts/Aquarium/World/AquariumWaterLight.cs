using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 물 셰이더(DesktopCompanion/AquariumWater)의 조명을 전역으로 주입한다.
    /// SW3와 달리 URP 라이팅에 의존하지 않으므로, 물에 영향을 주는 라이트는 오직 여기 지정한 것뿐이다.
    /// 씬에 디렉셔널 라이트를 아무리 추가해도 이 컴포넌트에 연결하지 않으면 물은 반응하지 않는다.
    ///
    /// 지정한 <see cref="Light"/>는 컴포넌트가 비활성이어도 된다. 셰이더가 Transform과 색만 읽기
    /// 때문이며, 비활성으로 두면 URP 라이팅에 전혀 참여하지 않아 "물에만 영향을 주는 라이트"가 된다.
    /// 반대로 라이트를 완전히 끄려면 <b>GameObject를 비활성</b>하면 된다.
    /// 계획: Docs/Plan/32_AquariumWaterShader.md
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AquariumWaterLight : MonoBehaviour
    {
        /// <summary>추가 라이트 최대 개수. ※ 셰이더의 AQW_MAX_ADD_LIGHTS와 반드시 같아야 한다.</summary>
        public const int MaxAdditionalLights = 4;

        // ⚠ 셰이더 쪽에서 이 변수들은 UnityPerMaterial CBUFFER '밖'에 선언되어 있어야 한다.
        //    안에 있으면 SRP Batcher가 머티리얼 상수로 덮어써 주입이 무시된다.
        private static readonly int s_lightDirId = Shader.PropertyToID("_AQW_LightDir");
        private static readonly int s_lightColorId = Shader.PropertyToID("_AQW_LightColor");
        private static readonly int s_addCountId = Shader.PropertyToID("_AQW_AddLightCount");
        private static readonly int s_addPosId = Shader.PropertyToID("_AQW_AddLightPos");
        private static readonly int s_addColorId = Shader.PropertyToID("_AQW_AddLightColor");
        private static readonly int s_addAttenId = Shader.PropertyToID("_AQW_AddLightAtten");
        private static readonly int s_addSpotDirId = Shader.PropertyToID("_AQW_AddLightSpotDir");

        [Header("메인 (디렉셔널)")]
        [Tooltip("물 조명의 기준이 되는 디렉셔널 라이트. 비우면 이 오브젝트의 Transform 방향을 쓴다. " +
                 "방향만 사용하므로 Point/Spot을 넣으면 안 된다(아래 Additional Lights로).")]
        [SerializeField] private Light m_light;

        [Tooltip("라이트의 색·강도를 무시하고 아래 값을 쓴다. 라이트를 비운 경우에는 항상 아래 값을 쓴다.")]
        [SerializeField] private bool m_overrideColor;

        [SerializeField] private Color m_color = Color.white;

        [SerializeField, Min(0f)] private float m_intensity = 1f;

        [Header("추가 (Point / Spot / Directional)")]
        [Tooltip("수면 하이라이트에 참여할 추가 라이트. Point·Spot·Directional 전부 가능하며 최대 " +
                 "4개까지 반영된다. 거리·콘 감쇠는 URP와 같은 식을 쓴다.")]
        [SerializeField] private Light[] m_additionalLights = new Light[0];

        private Transform Source => m_light != null ? m_light.transform : transform;

        // 매 프레임 재사용해 GC를 만들지 않는다.
        private readonly Vector4[] m_addPos = new Vector4[MaxAdditionalLights];
        private readonly Vector4[] m_addColor = new Vector4[MaxAdditionalLights];
        private readonly Vector4[] m_addAtten = new Vector4[MaxAdditionalLights];
        private readonly Vector4[] m_addSpotDir = new Vector4[MaxAdditionalLights];

        private void OnEnable()
        {
            WarnOnMisconfiguredMainLight();
            Push();
        }

        // 라이트가 움직이거나 밝기가 바뀔 수 있으므로 매 프레임 주입한다(전부 CPU 상수 갱신이라 저렴).
        private void LateUpdate()
        {
            Push();
        }

        private void Push()
        {
            PushMainLight();
            PushAdditionalLights();
        }

        private void PushMainLight()
        {
            Vector3 forward = Source.forward;

            Color c = m_overrideColor || m_light == null
                ? m_color * m_intensity
                : m_light.color * m_light.intensity;

            Shader.SetGlobalVector(s_lightDirId, new Vector4(forward.x, forward.y, forward.z, 0f));
            Shader.SetGlobalVector(s_lightColorId, new Vector4(c.r, c.g, c.b, 1f));
        }

        private void PushAdditionalLights()
        {
            int count = 0;

            if (m_additionalLights != null)
            {
                for (int i = 0; i < m_additionalLights.Length && count < MaxAdditionalLights; i++)
                {
                    Light light = m_additionalLights[i];

                    // Light 컴포넌트의 enabled는 보지 않는다 — 꺼둔 채로 '물에만 영향을 주는 라이트'로
                    // 쓰는 것이 이 컴포넌트의 사용법이다. 끄려면 GameObject를 비활성한다.
                    if (light == null || light.gameObject.activeInHierarchy == false) continue;

                    Transform t = light.transform;
                    Color c = light.color * light.intensity;

                    m_addPos[count] = light.type == LightType.Directional
                        ? new Vector4(-t.forward.x, -t.forward.y, -t.forward.z, 0f)  // 라이트를 향한 방향
                        : new Vector4(t.position.x, t.position.y, t.position.z, 1f); // 위치
                    m_addColor[count] = new Vector4(c.r, c.g, c.b, 1f);
                    m_addAtten[count] = BuildAttenuation(light);
                    m_addSpotDir[count] = new Vector4(-t.forward.x, -t.forward.y, -t.forward.z, 0f);
                    count++;
                }
            }

            // 남은 슬롯은 비워둔다(이전 프레임 값이 남아 count가 늘었을 때 새는 것을 막는다).
            for (int i = count; i < MaxAdditionalLights; i++)
            {
                m_addPos[i] = Vector4.zero;
                m_addColor[i] = Vector4.zero;
                m_addAtten[i] = new Vector4(0f, 0f, 0f, 1f);
                m_addSpotDir[i] = Vector4.zero;
            }

            Shader.SetGlobalFloat(s_addCountId, count);
            Shader.SetGlobalVectorArray(s_addPosId, m_addPos);
            Shader.SetGlobalVectorArray(s_addColorId, m_addColor);
            Shader.SetGlobalVectorArray(s_addAttenId, m_addAtten);
            Shader.SetGlobalVectorArray(s_addSpotDirId, m_addSpotDir);
        }

        /// <summary>
        /// URP의 라이트 상수 구성과 같은 값. x = 1/range², zw = 스팟 콘(scale, offset).
        /// 기본 zw = (0, 1)이면 셰이더의 콘 감쇠가 항상 1이 되어 Point·Directional에 영향이 없다.
        /// </summary>
        private static Vector4 BuildAttenuation(Light light)
        {
            var atten = new Vector4(0f, 0f, 0f, 1f);

            if (light.type != LightType.Directional)
                atten.x = 1f / Mathf.Max(0.0001f, light.range * light.range);

            if (light.type == LightType.Spot)
            {
                float cosOuter = Mathf.Cos(Mathf.Deg2Rad * light.spotAngle * 0.5f);
                float cosInner = Mathf.Cos(Mathf.Deg2Rad * light.innerSpotAngle * 0.5f);
                float invAngleRange = 1f / Mathf.Max(0.001f, cosInner - cosOuter);

                atten.z = invAngleRange;
                atten.w = -cosOuter * invAngleRange;
            }

            return atten;
        }

        private void WarnOnMisconfiguredMainLight()
        {
            if (m_light != null && m_light.type != LightType.Directional)
            {
                Debug.LogWarning(
                    $"[AquariumWaterLight] 메인 슬롯의 '{m_light.name}'은 {m_light.type} 타입이다. " +
                    "메인은 방향만 사용하므로 거리·콘 감쇠가 적용되지 않는다. " +
                    "Additional Lights 배열로 옮길 것.", this);
            }

            if (m_additionalLights != null && m_additionalLights.Length > MaxAdditionalLights)
            {
                Debug.LogWarning(
                    $"[AquariumWaterLight] 추가 라이트가 {m_additionalLights.Length}개 지정되었으나 " +
                    $"최대 {MaxAdditionalLights}개까지만 반영된다.", this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (isActiveAndEnabled == false) return;
            Push();
        }

        private void OnDrawGizmosSelected()
        {
            Transform source = Source;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(source.position, source.forward * 2f);

            if (m_additionalLights == null) return;

            Gizmos.color = Color.yellow;
            for (int i = 0; i < m_additionalLights.Length && i < MaxAdditionalLights; i++)
            {
                Light light = m_additionalLights[i];
                if (light == null) continue;

                if (light.type == LightType.Directional)
                    Gizmos.DrawRay(light.transform.position, light.transform.forward * 2f);
                else
                    Gizmos.DrawWireSphere(light.transform.position, 0.15f);
            }
        }
#endif
    }
}
