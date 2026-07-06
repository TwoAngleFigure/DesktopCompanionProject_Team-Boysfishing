using UnityEngine;

public class UIBase : MonoBehaviour
{
    [SerializeField] private CanvasGroup m_cg;

    public void Show()
    {
        m_cg.alpha = 1.0f;
        m_cg.interactable = true;
        m_cg.blocksRaycasts = true;
    }

    public void Hide()
    {
        m_cg.alpha = 0f;
        m_cg.interactable = false;
        m_cg.blocksRaycasts = false;
    }
}