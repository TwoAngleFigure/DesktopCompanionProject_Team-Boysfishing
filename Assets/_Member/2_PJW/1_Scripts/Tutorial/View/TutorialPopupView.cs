using System;
using System.Collections.Generic;
using DesktopCompanion.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 튜토리얼 도움말 팝업. 스텝별 문구를 인스펙터로 보유하며(System은 문구를 모른다),
    /// 표시 중에 들어온 스텝은 큐에 쌓아 닫을 때 이어서 보여준다.
    /// 닫힘은 System에 보고하지 않는다 — 표시 여부는 발동 시점에 이미 기록된다.
    ///
    /// 창 설정은 인스펙터에서 다음으로 맞춘다:
    ///  - ParticipateInLayout = false (자동 배치 제외)
    ///  - ClosableByShortcut  = false (우클릭·ESC로 닫히지 않게)
    ///  - HideMode = CanvasGroup, OpenOnStart = false (스텝마다 Bind/Unbind 반복 방지)
    /// </summary>
    public class TutorialPopupView : UIWindowBase
    {
        /// <summary>스텝 하나에 대응하는 도움말 문구.</summary>
        [Serializable]
        private class StepEntry
        {
            public TutorialStep Step;
            public string Title;
            [TextArea(3, 8)] public string Body;
        }

        [Header("Content")]
        [Tooltip("스텝별 도움말 문구. 여기 없는 스텝은 무시된다")]
        [SerializeField] private List<StepEntry> m_entries = new();

        [Header("Widgets")]
        [SerializeField] private TMP_Text m_titleText;
        [SerializeField] private TMP_Text m_bodyText;
        [Tooltip("닫기(X) 버튼")]
        [SerializeField] private Button m_closeButton;

        private readonly Queue<TutorialStep> m_pendingSteps = new();
        private TutorialSystem m_tutorialSystem;
        private bool m_isShowing;

        public override void Bind()
        {
            m_tutorialSystem = SystemManager.GetSystem<TutorialSystem>();

            if (m_tutorialSystem == null)
            {
                Debug.LogWarning("[TutorialPopupView] TutorialSystem 미발견 — 도움말이 뜨지 않는다.", this);
            }
            else
            {
                m_tutorialSystem.OnStepTriggered += HandleStepTriggered;
            }

            if (m_closeButton != null)
            {
                m_closeButton.onClick.AddListener(HandleClose);
            }
        }

        public override void Unbind()
        {
            if (m_tutorialSystem != null)
            {
                m_tutorialSystem.OnStepTriggered -= HandleStepTriggered;
                m_tutorialSystem = null;
            }

            if (m_closeButton != null)
            {
                m_closeButton.onClick.RemoveListener(HandleClose);
            }

            m_pendingSteps.Clear();
            m_isShowing = false;
        }

        private void HandleStepTriggered(TutorialStep step)
        {
            if (FindEntry(step) == null)
            {
                return;   // 문구가 없는 스텝은 무시한다(스텝만 먼저 추가된 경우 대비)
            }

            m_pendingSteps.Enqueue(step);

            if (m_isShowing == false)
            {
                ShowNext();
            }
        }

        private void HandleClose()
        {
            Hide();
            m_isShowing = false;
            ShowNext();   // 표시 중에 밀려 있던 스텝이 있으면 이어서 보여준다
        }

        private void ShowNext()
        {
            while (m_pendingSteps.Count > 0)
            {
                StepEntry entry = FindEntry(m_pendingSteps.Dequeue());
                if (entry == null)
                {
                    continue;
                }

                if (m_titleText != null)
                {
                    m_titleText.text = entry.Title;
                }
                if (m_bodyText != null)
                {
                    m_bodyText.text = entry.Body;
                }

                m_isShowing = true;
                Show();
                return;
            }
        }

        private StepEntry FindEntry(TutorialStep step)
        {
            for (int i = 0; i < m_entries.Count; i++)
            {
                StepEntry entry = m_entries[i];
                if (entry != null && entry.Step == step)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
