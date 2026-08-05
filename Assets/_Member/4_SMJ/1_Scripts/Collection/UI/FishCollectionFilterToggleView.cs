using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class FishCollectionFilterToggleView : MonoBehaviour
    {
        [SerializeField] private Toggle m_toggle;
        [SerializeField] private TMP_Text m_labelText;

        private Action<bool> m_onValueChanged;

        public void Initialize(
            string label,
            bool isSelected,
            Action<bool> onValueChanged)
        {
            m_labelText.text = label;
            m_onValueChanged = onValueChanged;

            m_toggle.SetIsOnWithoutNotify(isSelected);
            m_toggle.onValueChanged.AddListener(
                HandleValueChanged);
        }

        public void SetSelected(bool isSelected)
        {
            m_toggle.SetIsOnWithoutNotify(isSelected);
        }

        public void Release()
        {
            m_toggle.onValueChanged.RemoveListener(
                HandleValueChanged);

            m_onValueChanged = null;
        }

        private void HandleValueChanged(bool isSelected)
        {
            m_onValueChanged?.Invoke(isSelected);
        }
    }
}