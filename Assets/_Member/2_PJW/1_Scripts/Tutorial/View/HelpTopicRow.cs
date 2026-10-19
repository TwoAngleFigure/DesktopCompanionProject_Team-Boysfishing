using System;
using DesktopCompanion.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>도움말 도감 목록의 항목 버튼 행. 창이 해금된 항목마다 인스턴스화한다.</summary>
    public class HelpTopicRow : MonoBehaviour
    {
        [SerializeField] private Button m_button;
        [SerializeField] private TMP_Text m_label;

        [Tooltip("미열람 표시(New 뱃지 등)")]
        [SerializeField] private GameObject m_unreadBadge;

        private Action<HelpTopic> m_onClicked;
        private HelpTopic m_topic;

        public void Set(HelpTopic topic, string label, bool isUnread, Action<HelpTopic> onClicked)
        {
            m_topic = topic;
            m_onClicked = onClicked;

            if (m_label != null)
            {
                m_label.text = label;
            }

            SetUnreadBadge(isUnread);

            if (m_button != null)
            {
                m_button.onClick.RemoveAllListeners();
                m_button.onClick.AddListener(HandleClicked);
            }
        }

        public void SetUnreadBadge(bool isUnread)
        {
            if (m_unreadBadge != null)
            {
                m_unreadBadge.SetActive(isUnread);
            }
        }

        private void HandleClicked() => m_onClicked?.Invoke(m_topic);
    }
}
