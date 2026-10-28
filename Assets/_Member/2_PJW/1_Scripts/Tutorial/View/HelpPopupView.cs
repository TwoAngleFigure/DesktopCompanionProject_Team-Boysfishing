using System;
using System.Collections.Generic;
using DesktopCompanion.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 도움말 팝업. 항목이 해금되면 즉시 뜬다. 이미 떠 있으면 새로 해금된 쪽이 앞으로 나오고
    /// 보던 페이지는 '다음'으로 밀린다. 한 번에 여러 개가 해금되면 방송된 순서대로 이어 붙는다.
    /// 페이지 내용은 항목별 프리팹 인스턴스가 통째로 갖고 이 창은 켜고 끄기만 한다.
    ///
    /// 창 설정은 인스펙터에서 다음으로 맞춘다:
    ///  - HideMode = CanvasGroup, OpenOnStart = false
    ///    ※ Deactivate 모드면 닫혀 있는 동안 Bind가 풀려 해금 방송을 못 받는다 — 팝업이 영영 뜨지 않는다.
    ///  - ParticipateInLayout = false (자동으로 뜨는 창이라 다른 창을 밀어내지 않게)
    /// </summary>
    public class HelpPopupView : UIWindowBase
    {
        /// <summary>팝업이 보여줄 항목 하나. 내용은 페이지 프리팹이 갖고 여기는 대응만 적는다.</summary>
        [Serializable]
        private class PopupEntry
        {
            public HelpTopic Topic;

            [Tooltip("페이지 영역에 미리 배치해 둔 도움말 요소 프리팹 인스턴스")]
            public GameObject Page;

            [Tooltip("이 항목을 '도움말 닫기'로 닫을 때 이어서 해금할 항목(없으면 None)")]
            public HelpTopic NextOnClose;
        }

        [Header("Content")]
        [Tooltip("여기 없는 항목은 팝업으로 뜨지 않는다(도감에만 남는다)")]
        [SerializeField] private List<PopupEntry> m_entries = new();

        [Header("Widgets")]
        [Tooltip("우측 상단 패널 닫기(X). 대기열을 버리고 닫는다")]
        [SerializeField] private Button m_closeButton;

        [Tooltip("대기열이 남아 있을 때만 보이는 '다음'")]
        [SerializeField] private Button m_nextButton;

        [Tooltip("마지막 페이지에서만 보이는 '도움말 닫기'")]
        [SerializeField] private Button m_finishButton;

        private readonly List<HelpTopic> m_pending = new();    // [0]이 다음에 보여줄 장
        private readonly List<HelpTopic> m_incoming = new();   // 방금 들어온 묶음(재사용 버퍼)
        private TutorialSystem m_tutorialSystem;
        private HelpTopic m_currentTopic = HelpTopic.None;

        public override void Bind()
        {
            m_tutorialSystem = SystemManager.GetSystem<TutorialSystem>();

            if (m_closeButton != null)
            {
                m_closeButton.onClick.AddListener(HandleCloseClicked);
            }
            if (m_nextButton != null)
            {
                m_nextButton.onClick.AddListener(HandleNextClicked);
            }
            if (m_finishButton != null)
            {
                m_finishButton.onClick.AddListener(HandleFinishClicked);
            }

            ApplyPage(HelpTopic.None);   // 씬에 켜 둔 페이지가 있어도 닫힌 상태로 맞춘다

            if (m_tutorialSystem == null)
            {
                Debug.LogWarning("[HelpPopupView] TutorialSystem 미발견 — 도움말이 뜨지 않는다.", this);
                return;
            }

            m_tutorialSystem.OnTopicsUnlocked += HandleTopicsUnlocked;
        }

        public override void Unbind()
        {
            if (m_tutorialSystem != null)
            {
                m_tutorialSystem.OnTopicsUnlocked -= HandleTopicsUnlocked;
                m_tutorialSystem = null;
            }

            if (m_closeButton != null)
            {
                m_closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
            if (m_nextButton != null)
            {
                m_nextButton.onClick.RemoveListener(HandleNextClicked);
            }
            if (m_finishButton != null)
            {
                m_finishButton.onClick.RemoveListener(HandleFinishClicked);
            }

            m_pending.Clear();
            m_currentTopic = HelpTopic.None;
        }

        // ── 대기열 ──

        /// <summary>
        /// 새로 해금된 묶음을 받아 맨 앞에 꽂고 즉시 첫 장을 띄운다.
        ///
        /// 규칙이 둘이다.
        ///  - 방금 해금된 것이 <b>먼저</b> 나온다. 보던 페이지는 그 뒤로 밀린다(새 소식이 우선).
        ///  - 한 묶음 안에서는 <b>적힌 순서</b>를 지킨다(창 둘이 함께 열리는 경우 등).
        /// </summary>
        private void HandleTopicsUnlocked(IReadOnlyList<HelpTopic> topics)
        {
            m_incoming.Clear();

            for (int i = 0; i < topics.Count; i++)
            {
                if (FindEntry(topics[i]) != null)
                {
                    m_incoming.Add(topics[i]);   // 페이지가 없는 항목은 팝업으로 띄우지 않는다
                }
            }

            if (m_incoming.Count == 0)
            {
                return;
            }

            // 우클릭·ESC 등 이 창을 거치지 않는 경로로 닫혔을 수 있다. X와 같이 취급해 상태를 정리한다.
            if (m_currentTopic != HelpTopic.None && IsShown == false)
            {
                m_pending.Clear();
                m_currentTopic = HelpTopic.None;
            }

            if (m_currentTopic != HelpTopic.None)
            {
                m_pending.Insert(0, m_currentTopic);   // 보던 페이지는 새 묶음 바로 뒤로
                m_currentTopic = HelpTopic.None;
            }

            m_pending.InsertRange(0, m_incoming);
            ShowNextPage();
        }

        private void ShowNextPage()
        {
            if (m_pending.Count == 0)
            {
                return;
            }

            HelpTopic topic = m_pending[0];
            m_pending.RemoveAt(0);

            m_currentTopic = topic;
            ApplyPage(topic);
            m_tutorialSystem.MarkRead(topic);   // 표시된 순간이 열람이다
            UpdateButtons();

            if (IsShown == false)
            {
                Show();   // 이미 떠 있으면 다시 열지 않는다 — 페이지를 넘길 때마다 열림 연출이 재생되지 않게
            }
        }

        private void UpdateButtons()
        {
            bool hasNext = m_pending.Count > 0;

            if (m_nextButton != null)
            {
                m_nextButton.gameObject.SetActive(hasNext);
            }
            if (m_finishButton != null)
            {
                m_finishButton.gameObject.SetActive(hasNext == false);
            }
        }

        // ── 버튼 ──

        private void HandleNextClicked() => ShowNextPage();

        /// <summary>X — 지금 그만 보겠다는 의사표시다. 남은 페이지를 버리고 연쇄도 하지 않는다.</summary>
        private void HandleCloseClicked() => ClosePopup(false);

        /// <summary>도움말 닫기 — 끝까지 읽고 닫았으므로 이어지는 항목이 있으면 해금한다.</summary>
        private void HandleFinishClicked() => ClosePopup(true);

        private void ClosePopup(bool chainNext)
        {
            HelpTopic closedTopic = m_currentTopic;

            m_pending.Clear();
            m_currentTopic = HelpTopic.None;

            // 페이지는 끄지 않는다 — 닫힘 연출이 도는 동안 내용물이 먼저 사라지면 빈 창만 페이드된다.
            // 창 전체가 CanvasGroup으로 함께 흐려지고, 다음에 열릴 때 ApplyPage가 정리한다.
            Hide();

            if (chainNext == false || m_tutorialSystem == null)
            {
                return;
            }

            PopupEntry entry = FindEntry(closedTopic);
            if (entry != null && entry.NextOnClose != HelpTopic.None)
            {
                // 해금이 곧 표시다 — 이 호출로 HandleTopicUnlocked가 다시 돌아 팝업이 열린다.
                m_tutorialSystem.Unlock(entry.NextOnClose);
            }
        }

        // ── 페이지 ──

        /// <summary>지정한 항목의 페이지만 켜고 나머지는 끈다. None이면 전부 끈다.</summary>
        private void ApplyPage(HelpTopic topic)
        {
            for (int i = 0; i < m_entries.Count; i++)
            {
                PopupEntry entry = m_entries[i];
                if (entry?.Page != null)
                {
                    entry.Page.SetActive(entry.Topic == topic);
                }
            }
        }

        private PopupEntry FindEntry(HelpTopic topic)
        {
            if (topic == HelpTopic.None)
            {
                return null;
            }

            for (int i = 0; i < m_entries.Count; i++)
            {
                PopupEntry entry = m_entries[i];
                if (entry != null && entry.Topic == topic)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
