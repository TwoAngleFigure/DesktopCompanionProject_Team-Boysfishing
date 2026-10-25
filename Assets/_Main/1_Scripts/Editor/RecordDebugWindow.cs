using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// 플레이 모드에서 RecordSystem을 직접 호출하고 집계·이벤트·조건 판정을 확인하는 디버그 창.
    /// 출처 시스템의 기록 호출이 연결되기 전에 기록 시스템 단독으로 검증하기 위한 도구다.
    /// </summary>
    public class RecordDebugWindow : EditorWindow
    {
        private const int MaxLogLines = 30;

        private RecordSystem m_recordSystem;
        private RecordSource m_source = RecordSource.FishCaught;
        private int m_targetId = 100001;
        private int m_amount = 1;
        private ItemType m_itemType = ItemType.Materials;
        private ItemQuality m_quality = ItemQuality.ThreeStar;
        private float m_size = 50f;

        private string m_conditionText = "FishCaught>=3";
        private RecordCondition m_watchedCondition;
        private string m_watchedText;

        private readonly List<string> m_logs = new();
        private Vector2 m_valuesScroll;
        private Vector2 m_logScroll;

        [MenuItem("Tools/DesktopCompanion/기록 디버그")]
        public static void Open() => GetWindow<RecordDebugWindow>("기록 디버그");

        private void OnEnable() => EditorApplication.playModeStateChanged += HandlePlayModeChanged;

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            Unbind();
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            if (EditorApplication.isPlaying == false)
            {
                EditorGUILayout.HelpBox("Play 모드에서만 사용할 수 있다.", MessageType.Info);
                return;
            }

            BindIfNeeded();
            if (m_recordSystem == null)
            {
                EditorGUILayout.HelpBox("RecordSystem을 찾을 수 없다. 부팅이 끝났는지, GameManager에 등록됐는지 확인한다.", MessageType.Warning);
                return;
            }

            DrawRecordSection();
            EditorGUILayout.Space();
            DrawConditionSection();
            EditorGUILayout.Space();
            DrawValuesSection();
            EditorGUILayout.Space();
            DrawLogSection();
        }

        // ── 기록 호출 ──

        private void DrawRecordSection()
        {
            EditorGUILayout.LabelField("기록 호출", EditorStyles.boldLabel);
            m_source = (RecordSource)EditorGUILayout.EnumPopup("출처", m_source);

            bool hasTarget = m_source != RecordSource.GoldEarned && m_source != RecordSource.GoldSpent;
            bool hasAmount = m_source != RecordSource.FishCaught && m_source != RecordSource.FishingFailed
                && m_source != RecordSource.StageArrived;
            bool hasItemType = m_source == RecordSource.ItemDropped || m_source == RecordSource.ItemCrafted
                || m_source == RecordSource.ItemBought || m_source == RecordSource.ItemSold;

            if (hasTarget)
            {
                m_targetId = EditorGUILayout.IntField("대상 ID", m_targetId);
            }

            if (hasItemType)
            {
                m_itemType = (ItemType)EditorGUILayout.EnumPopup("아이템 종류", m_itemType);
            }

            if (hasAmount)
            {
                m_amount = EditorGUILayout.IntField("증가량", m_amount);
            }

            if (m_source == RecordSource.FishCaught)
            {
                m_quality = (ItemQuality)EditorGUILayout.EnumPopup("품질", m_quality);
                m_size = EditorGUILayout.FloatField("크기", m_size);
            }

            if (GUILayout.Button("기록"))
            {
                Record();
            }
        }

        private void Record()
        {
            switch (m_source)
            {
                case RecordSource.FishCaught:
                    m_recordSystem.RecordFishCaught(new FishCaughtInfo(m_targetId, m_quality, m_size));
                    break;
                case RecordSource.FishingFailed:
                    m_recordSystem.RecordFishingFailed(m_targetId);
                    break;
                case RecordSource.ItemDropped:
                    m_recordSystem.RecordItemDropped(new ItemRecordInfo(m_itemType, m_targetId, m_amount));
                    break;
                case RecordSource.ItemCrafted:
                    m_recordSystem.RecordItemCrafted(new ItemCraftInfo(0, m_itemType, m_targetId, m_amount));
                    break;
                case RecordSource.ItemBought:
                    m_recordSystem.RecordItemBought(new ItemTradeInfo(m_itemType, m_targetId, m_amount, 0));
                    break;
                case RecordSource.ItemSold:
                    m_recordSystem.RecordItemSold(new ItemTradeInfo(m_itemType, m_targetId, m_amount, 0));
                    break;
                case RecordSource.GoldEarned:
                    m_recordSystem.RecordGoldEarned(m_amount);
                    break;
                case RecordSource.GoldSpent:
                    m_recordSystem.RecordGoldSpent(m_amount);
                    break;
                case RecordSource.StageArrived:
                    m_recordSystem.RecordStageArrived(m_targetId);
                    break;
                default:
                    AddLog($"지원하지 않는 출처: {m_source}");
                    break;
            }
        }

        // ── 조건 판정 ──

        private void DrawConditionSection()
        {
            EditorGUILayout.LabelField("조건 판정", EditorStyles.boldLabel);
            m_conditionText = EditorGUILayout.TextField("조건", m_conditionText);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 판정"))
                {
                    RecordCondition condition = RecordConditions.Parse(m_conditionText, "기록 디버그");
                    AddLog($"판정 '{m_conditionText}' → {(condition.IsInvalid ? "무효" : RecordConditions.IsMet(condition, m_recordSystem.GetValue).ToString())}");
                }

                if (GUILayout.Button("달성 감시 시작"))
                {
                    m_watchedCondition = RecordConditions.Parse(m_conditionText, "기록 디버그");
                    m_watchedText = m_conditionText;
                    AddLog($"감시 시작 '{m_watchedText}' (현재 {RecordConditions.IsMet(m_watchedCondition, m_recordSystem.GetValue)})");
                }

                if (GUILayout.Button("감시 중지"))
                {
                    m_watchedCondition = null;
                    AddLog("감시 중지");
                }
            }

            if (m_watchedCondition != null)
            {
                EditorGUILayout.LabelField("감시 중", m_watchedText);
            }
        }

        // ── 현재 집계 ──

        private void DrawValuesSection()
        {
            EditorGUILayout.LabelField($"현재 집계 ({m_recordSystem.Values.Count})", EditorStyles.boldLabel);
            m_valuesScroll = EditorGUILayout.BeginScrollView(m_valuesScroll, GUILayout.Height(140));
            foreach (KeyValuePair<RecordKey, long> pair in m_recordSystem.Values.OrderBy(pair => pair.Key.ToString()))
            {
                EditorGUILayout.LabelField(pair.Key.ToString(), pair.Value.ToString());
            }

            EditorGUILayout.EndScrollView();
        }

        // ── 이벤트 로그 ──

        private void DrawLogSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("이벤트 로그", EditorStyles.boldLabel);
                if (GUILayout.Button("지우기", GUILayout.Width(60)))
                {
                    m_logs.Clear();
                }
            }

            m_logScroll = EditorGUILayout.BeginScrollView(m_logScroll, GUILayout.Height(160));
            for (int i = m_logs.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.LabelField(m_logs[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void AddLog(string line)
        {
            m_logs.Add(line);
            if (m_logs.Count > MaxLogLines)
            {
                m_logs.RemoveAt(0);
            }

            Repaint();
        }

        // ── 바인딩 ──

        // 플레이 세션마다 RecordSystem 인스턴스가 새로 생기므로 바뀌었으면 다시 구독한다.
        private void BindIfNeeded()
        {
            RecordSystem found = FindRecordSystem();
            if (found == m_recordSystem)
            {
                return;
            }

            Unbind();
            m_recordSystem = found;
            if (m_recordSystem == null)
            {
                return;
            }

            m_recordSystem.OnRecordsChanged += HandleRecordsChanged;
            m_recordSystem.OnFishCaught += HandleFishCaught;
        }

        private void Unbind()
        {
            if (m_recordSystem != null)
            {
                m_recordSystem.OnRecordsChanged -= HandleRecordsChanged;
                m_recordSystem.OnFishCaught -= HandleFishCaught;
            }

            m_recordSystem = null;
            m_watchedCondition = null;
        }

        private void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                Unbind();
            }
        }

        private void HandleRecordsChanged(IReadOnlyList<RecordChange> changes)
        {
            AddLog("OnRecordsChanged: " + string.Join(", ", changes.Select(c => $"{c.Key} {c.OldValue}→{c.NewValue}")));

            if (m_watchedCondition != null && RecordConditions.IsAchievedBy(m_watchedCondition, changes, m_recordSystem.GetValue))
            {
                AddLog($"★ 달성: '{m_watchedText}'");
            }
        }

        private void HandleFishCaught(FishCaughtInfo info)
            => AddLog($"OnFishCaught: {info.FishId} {info.Quality} {info.Size:0.#}");

        // GameManager.m_systemManager(비공개)를 리플렉션으로 얻는다(SaveDataTool과 같은 방식).
        private static RecordSystem FindRecordSystem()
        {
            GameManager gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager == null)
            {
                return null;
            }

            FieldInfo field = typeof(GameManager).GetField("m_systemManager", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(gameManager) is SystemManager systemManager
                ? systemManager.GetSystem<RecordSystem>()
                : null;
        }
    }
}
