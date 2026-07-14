using UnityEngine;
using DesktopCompanion.Views;
using DesktopCompanion.Systems;

[RequireComponent(typeof(Animator))]
public class FishingCharacterAnimationView : WorldViewBase
{
    [SerializeField] private Animator m_animator;

    private static readonly int s_fishingStateHash = Animator.StringToHash("FishingState");

    private FishingSystem m_fishingSystem;

    public override void Bind()
    {
        m_fishingSystem = SystemManager.GetSystem<FishingSystem>();

        if (m_animator == null)
        {
            Debug.LogWarning("[FishingCharacterAnimationView] Animator가 없습니다.");
            return;
        }

        if (m_fishingSystem == null)
        {
           return;
        }

        m_fishingSystem.OnStateChanged += HandleStateChanged;

        HandleStateChanged(m_fishingSystem.State);
    }

    public override void Unbind()
    {
        if (m_fishingSystem != null)
        {
            m_fishingSystem.OnStateChanged -= HandleStateChanged;
        }

        m_fishingSystem = null;
    }

    private void HandleStateChanged(FishingState state)
    {
        m_animator.SetInteger(s_fishingStateHash, (int)state);
    }

}
