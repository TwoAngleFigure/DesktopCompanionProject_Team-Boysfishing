using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace DesktopCompanion.Views
{
    [RequireComponent(typeof(RectTransform))]
    public class MapNodeItemView : MonoBehaviour
    {
        [Header("UI 컴포넌트 연결")]
        [SerializeField] private Button m_nodeButton;
        [SerializeField] private TextMeshProUGUI m_nodeNameText;

        private int m_stageId;
        private Action<int> m_onClickCallback;

        public void Setup(int stageId, string stageName, Vector2 mapPosition, Action<int> onClickCallback)
        {
            m_stageId = stageId;
            m_onClickCallback = onClickCallback;

            if (m_nodeNameText != null)
                m_nodeNameText.text = stageName;

            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = mapPosition;
            }

            if (m_nodeButton != null)
            {
                m_nodeButton.onClick.RemoveAllListeners();
                m_nodeButton.onClick.AddListener(OnNodeClicked);
            }
        }

        private void OnNodeClicked()
        {
            m_onClickCallback?.Invoke(m_stageId);
        }

        private void OnDestroy()
        {
            if (m_nodeButton != null)
            {
                m_nodeButton.onClick.RemoveAllListeners();
            }
        }
    }
}