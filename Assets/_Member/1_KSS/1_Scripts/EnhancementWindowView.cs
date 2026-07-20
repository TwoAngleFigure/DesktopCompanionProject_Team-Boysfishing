using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    public class EnhancementWindowView : UIViewBase
    {
        [SerializeField] private EnhancementSlotWidget m_slotWidget;
        [SerializeField] private TextMeshProUGUI m_statText;
        [SerializeField] private TextMeshProUGUI m_goldText;
        [SerializeField] private TextMeshProUGUI m_materialText;
        [SerializeField] private Button m_enhanceBtn;

        private EnhancementViewModel m_viewModel;

        public override void Bind()
        {
            m_viewModel = new EnhancementViewModel();
            m_viewModel.Bind();

            if (m_statText != null)
            {
                m_viewModel.StatIncreaseText.OnChanged += text => m_statText.text = text;
            }
            if (m_materialText != null)
            {
                m_viewModel.RequiredMaterialText.OnChanged += text => m_materialText.text = text;
            }
            if (m_goldText != null)
            {
                m_viewModel.RequiredGold.OnChanged += UpdateGoldUI;
                m_viewModel.CurrentGold.OnChanged += _ => UpdateGoldUI(m_viewModel.RequiredGold.Value);
            }
            if (m_enhanceBtn != null)
            {
                m_enhanceBtn.onClick.AddListener(() => m_viewModel.EnhanceCommand.Execute());
            }
        }

        private void UpdateGoldUI(int requiredGold)
        {
            int current = m_viewModel.CurrentGold.Value;
            string colorHex = current >= requiredGold ? "#00FF00" : "#FF0000";
            m_goldText.text = $"필요 골드 <color={colorHex}>{current}</color> / {requiredGold}";
        }

        public override void Unbind()
        {
            if (m_viewModel != null)
            {
                m_viewModel.Unbind();
                m_viewModel = null;
            }
        }
    }
}
