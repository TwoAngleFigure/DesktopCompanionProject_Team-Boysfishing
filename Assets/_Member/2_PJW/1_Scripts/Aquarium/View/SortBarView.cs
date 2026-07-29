using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 정렬 기준 드롭다운과 오름/내림 토글 버튼으로 구성된 정렬 바.
    /// 정렬 키를 enum이 아닌 int 인덱스로 다루므로 물고기·재료 어느 목록에도 같은 프리팹을 쓴다.
    /// </summary>
    public class SortBarView : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown m_keyDropdown;
        [SerializeField] private Button m_directionButton;
        [SerializeField] private TMP_Text m_directionLabel;

        private Action<int, SortDirection> m_onChanged;
        private SortDirection m_direction = SortDirection.Ascending;

        /// <summary>정렬 라벨과 현재 선택으로 위젯을 구성하고 변경 콜백을 연결한다. 창의 Bind()에서 호출한다.</summary>
        public void Initialize(string[] labels, int keyIndex, SortDirection direction, Action<int, SortDirection> onChanged)
        {
            m_onChanged = onChanged;
            m_direction = direction;

            if (m_keyDropdown != null)
            {
                m_keyDropdown.onValueChanged.RemoveAllListeners();   // 재초기화 시 중복 구독 방지
                m_keyDropdown.ClearOptions();
                m_keyDropdown.AddOptions(new List<string>(labels));
                m_keyDropdown.SetValueWithoutNotify(Mathf.Clamp(keyIndex, 0, labels.Length - 1));
                m_keyDropdown.onValueChanged.AddListener(HandleKeyChanged);
            }

            if (m_directionButton != null)
            {
                m_directionButton.onClick.RemoveAllListeners();
                m_directionButton.onClick.AddListener(ToggleDirection);
            }
            RefreshDirectionLabel();
        }

        /// <summary>위젯 리스너와 콜백 참조를 해제한다. 창의 Unbind()에서 호출한다.</summary>
        public void Release()
        {
            if (m_keyDropdown != null) m_keyDropdown.onValueChanged.RemoveAllListeners();
            if (m_directionButton != null) m_directionButton.onClick.RemoveAllListeners();
            m_onChanged = null;
        }

        private void HandleKeyChanged(int index) => m_onChanged?.Invoke(index, m_direction);

        private void ToggleDirection()
        {
            m_direction = m_direction == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;

            RefreshDirectionLabel();
            m_onChanged?.Invoke(m_keyDropdown != null ? m_keyDropdown.value : 0, m_direction);
        }

        private void RefreshDirectionLabel()
        {
            if (m_directionLabel != null)
            {
                m_directionLabel.text = m_direction == SortDirection.Ascending ? "▲" : "▼";
            }
        }
    }
}
