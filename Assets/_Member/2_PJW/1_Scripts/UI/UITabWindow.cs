using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 여러 UIWindowBase를 탭으로 묶는 컨테이너 창. 탭 버튼을 누르면 짝지은 창만 표시하고 나머지는 숨긴다.
    /// 컨테이너 자신이 UIManager의 활성 스택·자동 배치·닫기 입력에 참여하며,
    /// 자식 창은 이 컨테이너가 Show()/Hide()로 제어한다(각 창의 HideMode는 유지된다).
    ///
    /// 자식 창은 Participate In Layout / Closable By Shortcut을 꺼야 한다.
    /// </summary>
    public class UITabWindow : UIWindowBase
    {
        /// <summary>탭 하나의 구성. 전환 버튼과 그 버튼이 표시할 창의 쌍이다.</summary>
        [Serializable]
        public class Tab
        {
            [Tooltip("이 탭으로 전환하는 버튼")]
            [SerializeField] private Button m_button;

            [Tooltip("이 탭에서 보일 창")]
            [SerializeField] private UIWindowBase m_panel;

            public Button Button => m_button;
            public UIWindowBase Panel => m_panel;
            public bool IsValid => m_button != null && m_panel != null;
        }

        [Header("Tabs")]
        [Tooltip("탭 목록. 나열 순서가 곧 탭 인덱스다")]
        [SerializeField] private List<Tab> m_tabs = new();

        [Tooltip("창을 열 때 선택할 기본 탭 인덱스")]
        [SerializeField] private int m_defaultTabIndex = 0;

        [Tooltip("체크: 창을 열 때마다 기본 탭으로 되돌린다 / 해제: 마지막에 보던 탭을 유지한다. " +
                 "HideMode=Deactivate에서만 차이가 난다(CanvasGroup은 Bind가 1회뿐이라 항상 유지)")]
        [SerializeField] private bool m_resetToDefaultOnOpen = true;

        [Tooltip("체크 시 선택된 탭의 버튼을 interactable=false로 잠근다. " +
                 "Unity가 그 버튼을 Disabled 색상(회색)으로 그리므로, 버튼 자체 연출로 현재 탭을 표시할 거면 꺼둔다")]
        [SerializeField] private bool m_lockSelectedButton = false;

        private UnityAction[] m_handlers;   // 버튼별 리스너 — Unbind에서 이것만 걷어낸다(타 리스너 보존)
        private int m_selectedIndex = -1;

        /// <summary>현재 선택된 탭 인덱스. 아직 선택된 적이 없으면 -1이다.</summary>
        public int SelectedIndex => m_selectedIndex;

        public int TabCount => m_tabs.Count;

        public override void Bind()
        {
            m_handlers = new UnityAction[m_tabs.Count];

            for (int i = 0; i < m_tabs.Count; i++)
            {
                Tab tab = m_tabs[i];
                if (tab == null || tab.IsValid == false)
                {
                    Debug.LogWarning($"[UITabWindow] 탭 {i} 설정 누락(버튼 또는 창 미할당)", this);
                    continue;
                }

                int index = i;   // 클로저가 루프 변수를 잡지 않도록 복사
                m_handlers[i] = () => SelectTab(index);
                tab.Button.onClick.AddListener(m_handlers[i]);
            }

            // 열림 정책에 따라 시작 탭 결정. 강제 적용해 '겹쳐 보이는 초기 상태'를 정리한다.
            int start = (m_resetToDefaultOnOpen || m_selectedIndex < 0) ? m_defaultTabIndex : m_selectedIndex;
            ApplyTab(start, true);
        }

        public override void Unbind()
        {
            if (m_handlers == null)
            {
                return;
            }

            for (int i = 0; i < m_tabs.Count && i < m_handlers.Length; i++)
            {
                Tab tab = m_tabs[i];
                if (tab == null || tab.Button == null || m_handlers[i] == null)
                {
                    continue;
                }
                tab.Button.onClick.RemoveListener(m_handlers[i]);
                tab.Button.interactable = true;   // 잠금 상태가 창을 닫은 뒤까지 남지 않도록 되돌린다
            }
            m_handlers = null;
        }

        /// <summary>탭을 전환한다. 인덱스가 범위를 벗어나면 무시한다.</summary>
        public void SelectTab(int index) => ApplyTab(index, false);

        /// <param name="force">이미 선택된 탭이어도 표시 상태를 다시 적용할지(초기화용).</param>
        private void ApplyTab(int index, bool force)
        {
            if (index < 0 || index >= m_tabs.Count)
            {
                Debug.LogWarning($"[UITabWindow] 탭 인덱스 범위 밖: {index} (탭 {m_tabs.Count}개)", this);
                return;
            }
            if (force == false && index == m_selectedIndex)
            {
                return;   // 같은 탭 재클릭 — 재바인딩 비용만 드므로 무시
            }

            Tab selected = m_tabs[index];
            if (selected == null || selected.IsValid == false)
            {
                Debug.LogWarning($"[UITabWindow] 탭 {index} 설정 누락 — 전환 취소", this);
                return;
            }

            // 나머지를 먼저 숨긴 뒤 선택 탭을 켠다(두 창이 함께 보이는 프레임 방지).
            for (int i = 0; i < m_tabs.Count; i++)
            {
                Tab tab = m_tabs[i];
                if (i == index || tab == null || tab.IsValid == false)
                {
                    continue;
                }

                tab.Panel.Hide();
                if (m_lockSelectedButton)
                {
                    tab.Button.interactable = true;
                }
            }

            selected.Panel.Show();
            if (m_lockSelectedButton)
            {
                selected.Button.interactable = false;
            }

            m_selectedIndex = index;
        }

        private void OnValidate()
        {
            m_defaultTabIndex = Mathf.Clamp(m_defaultTabIndex, 0, Mathf.Max(0, m_tabs.Count - 1));
        }
    }
}
