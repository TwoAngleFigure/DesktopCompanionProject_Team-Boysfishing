using DesktopCompanion.Systems;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class FishCollectionEntryView : MonoBehaviour
    {
        [SerializeField] private Button m_button;
        [SerializeField] private TMP_Text m_nameText;
        [SerializeField] private Image[] m_qualityStars;

        private int m_fishDataId;
        private Action<int> m_onClicked;

        private void Awake()
        {
            m_button?.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            m_button?.onClick.RemoveListener(HandleClicked);
        }

        public void SetEntry(
            FishCollectionDisplayEntry entry,
            Action<int> onClicked)
        {
            m_fishDataId = entry.FishDataId;
            m_onClicked = onClicked;

            if (!entry.IsRegistered)
            {
                m_nameText.text = "???";
                SetQualityStars(0);
                return;
            }

            m_nameText.text = entry.FishName;
            SetQualityStars((int)entry.BestQuality);
        }

        private void SetQualityStars(int count)
        {
            int visibleCount = Mathf.Clamp(count, 0, m_qualityStars.Length);

            for (int i = 0; i < m_qualityStars.Length; i++)
            {
                if (m_qualityStars[i] != null)
                {
                    m_qualityStars[i].gameObject.SetActive(i < visibleCount);
                }
            }
        }

        private void HandleClicked()
        {
            m_onClicked?.Invoke(m_fishDataId);
        }
    }
}
