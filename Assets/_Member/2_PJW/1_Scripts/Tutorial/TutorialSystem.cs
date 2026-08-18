using System;
using System.Collections.Generic;
using DesktopCompanion.Save;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 도움말의 해금·열람 상태를 관리한다. 문구는 갖지 않고 항목만 다룬다(표시는 View 책임).
    /// 강제 표시가 없으므로 게임 상태를 바꾸는 코드가 하나도 없다 — 판정하고 기록할 뿐이다.
    /// </summary>
    public class TutorialSystem : SystemBase, ISaveable
    {
        private readonly List<HelpTopic> m_unlockedTopics = new();   // 해금 순서 보존
        private readonly HashSet<HelpTopic> m_readTopics = new();

        private FishingSystem m_fishingSystem;

        /// <summary>새 항목이 해금됐다. 도감 목록 갱신에 쓴다(항목 단위).</summary>
        public event Action<HelpTopic> OnTopicUnlocked;

        /// <summary>
        /// 한 사건으로 해금된 항목들. 목록 순서가 곧 표시 순서다.
        /// 팝업이 구독한다 — 함께 해금된 것끼리 순서를 지켜야 하므로 항목 단위가 아니라 묶음으로 받는다.
        /// </summary>
        public event Action<IReadOnlyList<HelpTopic>> OnTopicsUnlocked;

        /// <summary>항목을 열람했다. 도감 행의 뱃지 제거에 쓴다.</summary>
        public event Action<HelpTopic> OnTopicRead;

        /// <summary>미열람 항목 유무가 바뀌었다. 도감 메뉴 버튼의 미열람 점에 쓴다.</summary>
        public event Action<bool> OnUnreadChanged;

        /// <summary>해금된 항목을 해금 순서대로 돌려준다. 도움말 목록의 표시 순서다.</summary>
        public IReadOnlyList<HelpTopic> UnlockedTopics => m_unlockedTopics;

        public bool IsUnlocked(HelpTopic topic) => m_unlockedTopics.Contains(topic);
        public bool IsRead(HelpTopic topic) => m_readTopics.Contains(topic);

        /// <summary>해금됐지만 아직 안 읽은 항목이 하나라도 있는지.</summary>
        public bool HasUnread
        {
            get
            {
                for (int i = 0; i < m_unlockedTopics.Count; i++)
                {
                    if (m_readTopics.Contains(m_unlockedTopics[i]) == false)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // ── 해금·열람 ──

        /// <summary>
        /// 항목을 해금한다. 이미 해금된 항목이면 아무 일도 하지 않는다.
        /// 미열람이 없다가 생길 때만 알림을 방송해 연속 해금에도 중복 방송이 없다.
        /// </summary>
        public bool Unlock(HelpTopic topic)
        {
            if (UnlockOne(topic) == false)
            {
                return false;
            }

            OnTopicsUnlocked?.Invoke(new List<HelpTopic> { topic });
            return true;
        }

        /// <summary>
        /// 여러 항목을 한 사건으로 해금한다. 창 두 개가 함께 열리는 경우처럼
        /// 표시 순서를 지정해야 할 때 쓴다 — 목록에 적은 순서가 그대로 표시 순서다.
        /// </summary>
        public bool UnlockRange(IReadOnlyList<HelpTopic> topics)
        {
            if (topics == null)
            {
                return false;
            }

            List<HelpTopic> unlocked = null;
            for (int i = 0; i < topics.Count; i++)
            {
                if (UnlockOne(topics[i]) == false)
                {
                    continue;
                }

                unlocked ??= new List<HelpTopic>();
                unlocked.Add(topics[i]);
            }

            if (unlocked == null)
            {
                return false;
            }

            OnTopicsUnlocked?.Invoke(unlocked);
            return true;
        }

        /// <summary>해금 자체만 처리한다. 한 사건으로 묶어 방송하는 것은 호출자의 몫이다.</summary>
        private bool UnlockOne(HelpTopic topic)
        {
            if (topic == HelpTopic.None || IsUnlocked(topic))
            {
                return false;
            }

            bool hadUnread = HasUnread;
            m_unlockedTopics.Add(topic);

            Debug.Log($"[TutorialSystem] 도움말 해금: {topic}");
            OnTopicUnlocked?.Invoke(topic);

            if (hadUnread == false)
            {
                OnUnreadChanged?.Invoke(true);
            }

            return true;
        }

        /// <summary>항목을 열람 처리한다. 해금되지 않았거나 이미 읽었으면 무시한다.</summary>
        public void MarkRead(HelpTopic topic)
        {
            if (IsUnlocked(topic) == false || m_readTopics.Add(topic) == false)
            {
                return;
            }

            OnTopicRead?.Invoke(topic);

            if (HasUnread == false)
            {
                OnUnreadChanged?.Invoke(false);
            }
        }

        // ── 생명주기 ──

        public override void Initialize()
        {
            m_unlockedTopics.Clear();
            m_readTopics.Clear();   // 원시 기본값. 저장이 있으면 RestoreState가 덮어쓴다.
        }

        public override void PostInitialize()
        {
            m_fishingSystem = SystemManager.GetSystem<FishingSystem>();

            if (m_fishingSystem == null)
            {
                Debug.LogWarning("[TutorialSystem] FishingSystem 미발견 — 낚시 도움말이 해금되지 않는다.");
                return;
            }

            m_fishingSystem.OnStateChanged += HandleFishingStateChanged;
            m_fishingSystem.OnFishingResult += HandleFishingResult;
        }

        // ── 트리거 ──

        private void HandleFishingStateChanged(FishingState state)
        {
            switch (state)
            {
                case FishingState.Waiting:
                    Unlock(HelpTopic.Fishing);
                    break;

                case FishingState.Battling:
                    Unlock(HelpTopic.Battle);
                    break;
            }
        }

        private void HandleFishingResult(FishingResultType result)
        {
            if (result != FishingResultType.Success)
            {
                return;
            }

            Unlock(HelpTopic.Catch);
        }

        // ── 저장 ──

        public string SaveId => "tutorial";
        public Type StateType => typeof(TutorialSave);

        public object CaptureState()
        {
            TutorialSave save = new TutorialSave();
            save.unlockedTopics.AddRange(m_unlockedTopics);
            save.readTopics.AddRange(m_readTopics);
            return save;
        }

        public void RestoreState(object state)
        {
            if (state is not TutorialSave save)
            {
                return;
            }

            m_unlockedTopics.Clear();
            m_readTopics.Clear();

            if (save.unlockedTopics != null)
            {
                // 저장된 해금 순서를 그대로 복원한다(목록 표시 순서와 같다).
                foreach (HelpTopic topic in save.unlockedTopics)
                {
                    if (topic != HelpTopic.None && IsUnlocked(topic) == false)
                    {
                        m_unlockedTopics.Add(topic);
                    }
                }
            }

            if (save.readTopics == null)
            {
                return;
            }

            foreach (HelpTopic topic in save.readTopics)
            {
                m_readTopics.Add(topic);
            }
        }
    }
}
