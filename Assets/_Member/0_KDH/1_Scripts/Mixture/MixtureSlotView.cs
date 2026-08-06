using System;
using UnityEngine;
using UnityEngine.UI;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 조합 레시피 1칸. 결과물 아이템을 공용 <see cref="ItemSlotView"/>로 표시하고 클릭만 창에 전달한다.
    /// hover 팝업은 슬롯이 스스로 띄우므로 이 위젯은 hover를 다루지 않는다.
    /// </summary>
    public class MixtureSlotView : MonoBehaviour
    {
        [Tooltip("비우면 같은 오브젝트에서 자동으로 찾는다")]
        [SerializeField] private Button m_button;

        [Tooltip("아이콘·티어 테두리·hover 팝업 담당")]
        [SerializeField] private ItemSlotView m_slot;

        [Header("선택 표시")]
        [Tooltip("선택 시 색을 바꿀 칸 배경. 비우면 버튼의 Target Graphic → 같은 오브젝트의 Image 순으로 찾는다. " +
                 "★버튼 Transition이 Color Tint면 서로 색을 덮어쓴다 — Transition을 None으로 두거나 별도 이미지를 지정할 것")]
        [SerializeField] private Image m_background;
        [SerializeField] private Color m_normalColor = Color.white;
        [Tooltip("선택된 칸의 색. 약간 어둡게 눌린 느낌을 준다")]
        [SerializeField] private Color m_selectedColor = new Color(0.78f, 0.78f, 0.78f, 1f);

        private RecipeData_Mixture m_recipe;
        private Action<RecipeData_Mixture> m_onClick;

        /// <summary>이 칸이 표시 중인 레시피. 창이 선택 여부를 가릴 때 쓴다.</summary>
        public RecipeData_Mixture Recipe => m_recipe;

        private void Awake()
        {
            if (m_button == null)
            {
                m_button = GetComponent<Button>();
            }
            if (m_button != null)
            {
                m_button.onClick.AddListener(RaiseClicked);
            }

            if (m_background == null)
            {
                m_background = m_button != null ? m_button.targetGraphic as Image : null;
                if (m_background == null) m_background = GetComponent<Image>();
            }
        }

        private void OnDestroy()
        {
            if (m_button != null)
            {
                m_button.onClick.RemoveListener(RaiseClicked);
            }
        }

        /// <summary>
        /// 칸을 채운다. <paramref name="tooltipSource"/>는 hover 팝업 상세를 공급할 창이다.
        /// </summary>
        public void Set(RecipeData_Mixture recipe, ItemSlotVD slotVD, Sprite icon,
                        IItemTooltipSource tooltipSource, Action<RecipeData_Mixture> onClick)
        {
            m_recipe = recipe;
            m_onClick = onClick;

            if (m_slot != null) m_slot.Set(slotVD, icon, tooltipSource);

            SetSelected(false);
        }

        /// <summary>선택 표시를 켜고 끈다. 선택된 칸만 배경이 살짝 어두워진다.</summary>
        public void SetSelected(bool selected)
        {
            if (m_background != null)
            {
                m_background.color = selected ? m_selectedColor : m_normalColor;
            }
        }

        private void RaiseClicked() => m_onClick?.Invoke(m_recipe);
    }
}
