using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 여러 UIWindowBase를 탭으로 묶는 컨테이너 창. 탭 버튼을 누르면 그 탭에 묶인 창들만 표시하고 나머지는 숨긴다.
    /// 탭 하나에 창을 여러 개 지정할 수 있고, 같은 창을 여러 탭에 걸쳐 지정해도 된다.
    /// 컨테이너 자신이 UIManager의 활성 스택·자동 배치·닫기 입력에 참여하며,
    /// 자식 창은 이 컨테이너가 Show()/Hide()로 제어한다(각 창의 HideMode는 유지된다).
    ///
    /// 자식 창은 Participate In Layout / Closable By Shortcut을 꺼야 한다.
    /// </summary>
    public class UITabWindow : UIWindowBase
    {
        /// <summary>탭 하나의 구성. 전환 버튼과 그 버튼이 표시할 창 목록이다.</summary>
        [Serializable]
        public class Tab
        {
            [Tooltip("이 탭으로 전환하는 버튼")]
            [SerializeField] private Button m_button;

            [Tooltip("이 탭에서 함께 보일 창들. 나열 순서대로 Show한다(마지막 것이 활성 스택 top)")]
            [SerializeField] private List<UIWindowBase> m_panels = new();

            [Tooltip("(구버전 호환) 단일 창 필드. 값이 남아 있으면 창 목록 맨 앞으로 이관된다")]
            [SerializeField] private UIWindowBase m_panel;

            public Button Button => m_button;

            /// <summary>이 탭에서 보일 창 목록. 이관 전이면 구 단일 필드는 포함되지 않으므로 <see cref="MigrateLegacyPanel"/>를 먼저 부른다.</summary>
            public IReadOnlyList<UIWindowBase> Panels => m_panels;

            /// <summary>버튼과 창이 최소 하나씩 지정됐는지.</summary>
            public bool IsValid => m_button != null && HasPanel();

            /// <summary>구버전의 단일 창 필드를 목록 맨 앞으로 옮긴다. 이미 목록에 있으면 버린다.</summary>
            public void MigrateLegacyPanel()
            {
                if (m_panel == null)
                {
                    return;
                }

                if (m_panels.Contains(m_panel) == false)
                {
                    m_panels.Insert(0, m_panel);   // 기존 동작과 표시 순서를 맞추기 위해 맨 앞
                }
                m_panel = null;
            }

            /// <summary>이 탭의 창을 모두 연다.</summary>
            public void ShowPanels()
            {
                for (int i = 0; i < m_panels.Count; i++)
                {
                    if (m_panels[i] != null) m_panels[i].Show();
                }
            }

            /// <summary>이 탭의 창을 모두 닫는다.</summary>
            public void HidePanels()
            {
                for (int i = 0; i < m_panels.Count; i++)
                {
                    if (m_panels[i] != null) m_panels[i].Hide();
                }
            }

            private bool HasPanel()
            {
                for (int i = 0; i < m_panels.Count; i++)
                {
                    if (m_panels[i] != null) return true;
                }
                return m_panel != null;   // 아직 이관 전인 구버전 데이터
            }
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

        // 탭 버튼의 연출. 버튼이 계층을 뒤져 자기 창을 찾게 두면 탭 창이 중첩된 곳에서 바깥 창을 잡으므로,
        // 탭 목록을 가진 이쪽이 직접 쥐고 선택 상태를 넣어 준다.
        private ButtonStateOwner[] m_stateOwners;

        private int m_selectedIndex = -1;

        /// <summary>현재 선택된 탭 인덱스. 아직 선택된 적이 없으면 -1이다.</summary>
        public int SelectedIndex => m_selectedIndex;

        public int TabCount => m_tabs.Count;

        /// <summary>
        /// 선택 탭이 바뀔 때 발행한다. 인자는 새 선택 인덱스다.
        /// 탭 버튼 연출처럼 선택 상태를 '표시'만 하는 쪽이 구독한다(상태 소유자는 이 창이다).
        /// </summary>
        public event Action<int> SelectedChanged;

        public override void Bind()
        {
            m_handlers = new UnityAction[m_tabs.Count];
            m_stateOwners = new ButtonStateOwner[m_tabs.Count];

            for (int i = 0; i < m_tabs.Count; i++)
            {
                Tab tab = m_tabs[i];
                if (tab == null)
                {
                    continue;
                }

                tab.MigrateLegacyPanel();   // OnValidate가 저장되지 않은 경우에도 런타임 동작을 보장한다

                if (tab.IsValid == false)
                {
                    Debug.LogWarning($"[UITabWindow] 탭 {i} 설정 누락(버튼 또는 창 미할당)", this);
                    continue;
                }

                int index = i;   // 클로저가 루프 변수를 잡지 않도록 복사
                m_handlers[i] = () => SelectTab(index);
                tab.Button.onClick.AddListener(m_handlers[i]);

                // 소유자가 직접 알려 준다. 버튼이 계층을 거슬러 올라가 찾게 두면, 탭 창이 중첩된 곳에서
                // 자기 창이 아니라 더 가까운 바깥 창을 잡는다(§TabBookmarkButton).
                if (tab.Button.TryGetComponent(out TabBookmarkButton bookmark))
                {
                    bookmark.Attach(this, index);
                }

                if (tab.Button.TryGetComponent(out ButtonStateOwner stateOwner))
                {
                    m_stateOwners[i] = stateOwner;
                }
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

                if (tab.Button.TryGetComponent(out TabBookmarkButton bookmark))
                {
                    bookmark.Detach(this);
                }
            }
            m_handlers = null;
            m_stateOwners = null;
        }

        /// <summary>탭을 전환한다. 인덱스가 범위를 벗어나면 무시한다.</summary>
        public void SelectTab(int index) => ApplyTab(index, false);

        /// <summary>
        /// 버튼이 몇 번째 탭인지 돌려준다. 목록에 없으면 -1.
        /// 탭 버튼이 자기 인덱스를 스스로 알아내는 데 쓴다 — 인스펙터에 손으로 적으면 탭 순서를 바꿀 때 어긋난다.
        /// </summary>
        public int IndexOf(Button button)
        {
            if (button == null)
            {
                return -1;
            }

            for (int i = 0; i < m_tabs.Count; i++)
            {
                if (m_tabs[i] != null && m_tabs[i].Button == button)
                {
                    return i;
                }
            }
            return -1;
        }

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

                tab.HidePanels();
                if (m_lockSelectedButton)
                {
                    tab.Button.interactable = true;
                }
            }

            // 창을 탭끼리 공유해도 되도록, 전부 숨긴 뒤에 선택 탭을 켠다.
            selected.ShowPanels();
            if (m_lockSelectedButton)
            {
                selected.Button.interactable = false;
            }

            m_selectedIndex = index;

            // 선택 상태를 탭 버튼 연출에 넣는다. 창이 목록을 갖고 있으므로 버튼이 찾아 나설 필요가 없다.
            if (m_stateOwners != null)
            {
                for (int i = 0; i < m_stateOwners.Length; i++)
                {
                    if (m_stateOwners[i] != null)
                    {
                        m_stateOwners[i].SetActive(i == index);
                    }
                }
            }

            SelectedChanged?.Invoke(index);   // 탭 버튼 연출 등 표시자에게 알린다
        }

        private void OnValidate()
        {
            m_defaultTabIndex = Mathf.Clamp(m_defaultTabIndex, 0, Mathf.Max(0, m_tabs.Count - 1));

            // 구버전의 단일 창 할당을 창 목록으로 옮긴다. 인스펙터를 한 번 건드리면 데이터가 정리된다.
            for (int i = 0; i < m_tabs.Count; i++)
            {
                if (m_tabs[i] != null) m_tabs[i].MigrateLegacyPanel();
            }
        }
    }
}
