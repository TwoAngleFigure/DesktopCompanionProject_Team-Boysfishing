using UnityEngine;

public class FishingCatchSplashEffect : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem[] m_particles;

    public void PlayAt(Vector3 worldPosition)
    {
        transform.position = worldPosition;

        foreach (ParticleSystem particle in m_particles)
        {
            if (particle == null)
            {
                continue;
            }

            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        foreach (ParticleSystem particle in m_particles)
        {
            particle?.Play(true);
        }
    }

    public void Stop()
    {
        foreach (ParticleSystem particle in m_particles)
        {
            if (particle == null)
            {
                continue;
            }

            particle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
