using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    public class MixtureSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button m_button;

        // [SerializeField] private Image m_iconImage; 

        private MixtureRecipeSO m_recipe;
        private Action<MixtureRecipeSO> m_onClick;
        private Action<MixtureRecipeSO> m_onHoverEnter;
        private Action m_onHoverExit;

        public void Initialize(MixtureRecipeSO recipe, Action<MixtureRecipeSO> onClick, Action<MixtureRecipeSO> onHoverEnter, Action onHoverExit)
        {
            m_recipe = recipe;
            m_onClick = onClick;
            m_onHoverEnter = onHoverEnter;
            m_onHoverExit = onHoverExit;

            m_button.onClick.RemoveAllListeners();
            m_button.onClick.AddListener(() => m_onClick?.Invoke(m_recipe));

            // 추후 연동: m_iconImage.sprite = recipe.IconSprite;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_onHoverEnter?.Invoke(m_recipe);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_onHoverExit?.Invoke();
        }
    }
}