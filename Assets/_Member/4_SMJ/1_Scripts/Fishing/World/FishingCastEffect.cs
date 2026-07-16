using UnityEngine;
public class FishingCastEffectView : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem m_waterImpactEffect;

    public void OnBobberWaterImpact()
    {
        if (m_waterImpactEffect == null)
        {
            Debug.LogWarning(
                "[FishingCastEffectView] Water Impact Effect가 없습니다."
            );
            return;
        }

        m_waterImpactEffect.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        m_waterImpactEffect.Play(true);
    }
}
