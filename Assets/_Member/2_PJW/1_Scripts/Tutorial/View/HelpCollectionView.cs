using System;
using System.Collections.Generic;
using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 도움말 도감. 해금된 항목만 목록에 행으로 만들고, 고른 항목의 페이지를 켠다.
    /// 팝업에서 X로 건너뛴 내용을 되찾는 경로이며, 팝업과 같은 요소 프리팹을 쓰되 인스턴스는 따로 둔다.
    /// </summary>
    public class HelpCollectionView : UIWindowBase
    {
        /// <summary>도감이 다루는 항목 하나. 내용은 페이지 프리팹이 갖고 여기는 대응만 적는다.</summary>
        [Serializable]
        private class CollectionEntry
        {
            public HelpTopic Topic;

            [Tooltip("목록 행에 표시할 이름")]
            public string Label;

            [Tooltip("페이지 영역에 미리 배치해 둔 도움말 요소 프리팹 인스턴스")]
            public GameObject Page;
        }

        [Header("Content")]
        [Tooltip("여기 없는 항목은 해금돼도 목록에 오르지 않는다")]
        [SerializeField] private List<CollectionEntry> m_entries = new();

        [Header("List")]
        [SerializeField] private HelpTopicRow m_rowPrefab;
        [SerializeField] private Transform m_rowParent;

        [Header("Page")]
        [Tooltip("고른 항목이 없을 때 보여줄 안내(선택)")]
        [SerializeField] private GameObject m_emptyPage;

        private readonly Dictionary<HelpTopic, HelpTopicRow> m_rows = new();
        private TutorialSystem m_tutorialSystem;

        public override void Bind()
        {
            m_tutorialSystem = SystemManager.GetSystem<TutorialSystem>();

            if (m_tutorialSystem == null)
            {
                Debug.LogWarning("[HelpCollectionView] TutorialSystem 미발견 — 목록이 비어 있다.", this);
                ApplyPage(HelpTopic.None);
                return;
            }

            m_tutorialSystem.OnTopicUnlocked += HandleTopicUnlocked;
            m_tutorialSystem.OnTopicRead += HandleTopicRead;

            RebuildRows();
            ApplyPage(HelpTopic.None);   // 열 때는 항상 고르지 않은 상태로 시작한다
        }

        public override void Unbind()
        {
            if (m_tutorialSystem != null)
            {
                m_tutorialSystem.OnTopicUnlocked -= HandleTopicUnlocked;
                m_tutorialSystem.OnTopicRead -= HandleTopicRead;
                m_tutorialSystem = null;
            }

            ClearRows();
        }

        /// <summary>목록에서 항목을 고르면 페이지를 켜고 열람 처리한다.</summary>
        public void SelectTopic(HelpTopic topic)
        {
            if (m_tutorialSystem == null || m_tutorialSystem.IsUnlocked(topic) == false)
            {
                return;
            }

            ApplyPage(topic);
            m_tutorialSystem.MarkRead(topic);
        }

        // ── 목록 ──

        /// <summary>
        /// 해금된 항목의 행을 만든다. 순서는 해금 순서가 아니라 <b>인스펙터 배열 순서</b>다 —
        /// 목록은 읽는 차례(개요 → 낚시 → …)대로 보여야 하고, 그 차례를 아는 쪽은 인스펙터다.
        /// </summary>
        private void RebuildRows()
        {
            ClearRows();

            for (int i = 0; i < m_entries.Count; i++)
            {
                CollectionEntry entry = m_entries[i];
                if (entry != null && m_tutorialSystem.IsUnlocked(entry.Topic))
                {
                    AddRow(entry.Topic);
                }
            }
        }

        private void AddRow(HelpTopic topic)
        {
            int entryIndex = IndexOfEntry(topic);
            if (entryIndex < 0 || m_rowPrefab == null || m_rowParent == null)
            {
                return;
            }
            if (m_rows.ContainsKey(topic))
            {
                return;
            }

            HelpTopicRow row = Instantiate(m_rowPrefab, m_rowParent);
            row.Set(topic, m_entries[entryIndex].Label, m_tutorialSystem.IsRead(topic) == false, SelectTopic);
            m_rows.Add(topic, row);

            // 창이 열려 있는 동안 해금된 항목도 제자리에 끼워 넣는다(맨 뒤에 붙지 않게).
            row.transform.SetSiblingIndex(SiblingIndexFor(entryIndex));
        }

        /// <summary>자기보다 앞선 항목 중 이미 행이 있는 개수 = 자기가 들어갈 자리.</summary>
        private int SiblingIndexFor(int entryIndex)
        {
            int index = 0;
            for (int i = 0; i < entryIndex; i++)
            {
                CollectionEntry entry = m_entries[i];
                if (entry != null && m_rows.ContainsKey(entry.Topic))
                {
                    index++;
                }
            }

            return index;
        }

        private void ClearRows()
        {
            foreach (KeyValuePair<HelpTopic, HelpTopicRow> pair in m_rows)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            m_rows.Clear();
        }

        // ── 페이지 ──

        /// <summary>지정한 항목의 페이지만 켜고 나머지는 끈다. None이면 안내만 남긴다.</summary>
        private void ApplyPage(HelpTopic topic)
        {
            for (int i = 0; i < m_entries.Count; i++)
            {
                CollectionEntry entry = m_entries[i];
                if (entry?.Page != null)
                {
                    entry.Page.SetActive(entry.Topic == topic);
                }
            }

            if (m_emptyPage != null)
            {
                m_emptyPage.SetActive(topic == HelpTopic.None);
            }
        }

        // ── 이벤트 ──

        private void HandleTopicUnlocked(HelpTopic topic) => AddRow(topic);

        private void HandleTopicRead(HelpTopic topic)
        {
            if (m_rows.TryGetValue(topic, out HelpTopicRow row) && row != null)
            {
                row.SetUnreadBadge(false);
            }
        }

        private int IndexOfEntry(HelpTopic topic)
        {
            for (int i = 0; i < m_entries.Count; i++)
            {
                CollectionEntry entry = m_entries[i];
                if (entry != null && entry.Topic == topic)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
