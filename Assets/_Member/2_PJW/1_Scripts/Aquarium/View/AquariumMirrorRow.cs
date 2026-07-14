using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 인벤토리 미러 행(공용). 배치 목록·회수 목록 양쪽에서 재사용한다.
    /// 행 클릭=상세 선택(dataId), 액션 버튼=배치(dataId)/회수(index).
    /// </summary>
    public class AquariumMirrorRow : MonoBehaviour
    {
        [SerializeField] private Image m_icon;
        [SerializeField] private TMP_Text m_label;
        [SerializeField] private Button m_rowButton;      // 클릭 → 상세 선택
        [SerializeField] private Button m_actionButton;   // 배치/회수
        [SerializeField] private TMP_Text m_actionLabel;

        private int m_actionPayload;      // dataId(배치) 또는 index(회수)
        private int m_selectPayload;      // dataId(상세)
        private Action<int> m_onAction;
        private Action<int> m_onSelect;

        private void Awake()
        {
            if (m_actionButton != null) m_actionButton.onClick.AddListener(() => m_onAction?.Invoke(m_actionPayload));
            if (m_rowButton != null) m_rowButton.onClick.AddListener(() => m_onSelect?.Invoke(m_selectPayload));
        }

        public void Set(AquariumMirrorFishVD vd, Sprite icon, string actionText,
                        int actionPayload, Action<int> onAction, Action<int> onSelect)
        {
            if (m_icon != null)
            {
                m_icon.enabled = icon != null;
                m_icon.sprite = icon;
            }
            if (m_label != null)
            {
                m_label.text = vd.Count > 1 ? $"{vd.Name} x{vd.Count}" : vd.Name;
            }
            if (m_actionLabel != null) m_actionLabel.text = actionText;

            m_actionPayload = actionPayload;
            m_selectPayload = vd.DataId;
            m_onAction = onAction;
            m_onSelect = onSelect;
        }
    }
}
