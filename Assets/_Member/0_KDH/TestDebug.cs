using UnityEngine;
using System.Reflection;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using DesktopCompanion.Systems;
using DesktopCompanion.Core;
using DesktopCompanion.Data;

public class TestDebug : MonoBehaviour
{
    [Header("스페이스바로 이동할 목표 맵 ID (예: 600002)")]
    [SerializeField] private int m_targetMapIdToMove = 600002;

    [Header("테스트 설정")]
    [SerializeField] private int m_testPlayerLicense = 3;
    [SerializeField] private float m_testPlayerSpeed = 50f;

    private StageSystem m_stageSystem;

    private void Start()
    {
        Invoke(nameof(InitDebugger), 1.5f);
    }

    private void InitDebugger()
    {
        var gameManager = FindAnyObjectByType<DesktopCompanion.Core.GameManager>();

        if (gameManager != null)
        {
            FieldInfo fieldInfo = typeof(DesktopCompanion.Core.GameManager).GetField("m_systemManager", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                var systemManager = fieldInfo.GetValue(gameManager) as SystemManager;

                if (systemManager != null)
                {
                    m_stageSystem = systemManager.GetSystem<StageSystem>();

                    if (m_stageSystem != null)
                    {
                        m_stageSystem.OnStageChanged += HandleStageChanged;
                        m_stageSystem.OnTravelStarted += HandleTravelStarted;

                        var currentData = m_stageSystem.CurrentStageData;
                        if (currentData != null)
                        {
                            Debug.Log($"[TestDebug] 초기 맵 배정 완료! 현재 위치: {currentData.Name} (ID: {currentData.ID})");
                            TestPoolsWithLicense(1);
                        }
                        return;
                    }
                }
            }
            Debug.LogError("[TestDebug] StageSystem을 찾을 수 없습니다. 시스템 등록을 확인하세요.");
        }
        else
        {
            Debug.LogError("[TestDebug] 씬에서 GameManager를 찾을 수 없습니다.");
        }
    }

    private void Update()
    {
        if (m_stageSystem == null) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            if (!m_stageSystem.IsTraveling)
            {
                Debug.Log($"[TestDebug] {m_targetMapIdToMove}번 맵으로 이동 시도...");
                m_stageSystem.MoveToStage(m_targetMapIdToMove, m_testPlayerLicense, m_testPlayerSpeed);
            }
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            TestPoolsWithLicense(1);
        }

        if (keyboard.digit2Key.wasPressedThisFrame)
        {
            TestPoolsWithLicense(2);
        }

        if (m_stageSystem.IsTraveling)
        {
            m_stageSystem.Tick(Time.deltaTime);
        }
    }

    private void TestPoolsWithLicense(int simulatedLicenseGrade)
    {
        if (m_stageSystem == null || m_stageSystem.CurrentStageData == null) return;

        string mapName = m_stageSystem.CurrentStageData.Name;
        List<TierPool> availablePools = m_stageSystem.GetAvailableTierPools(simulatedLicenseGrade);

        Debug.Log($"[풀 검증] 현재 지역: {mapName} / 가상 유저 라이센스: {simulatedLicenseGrade}렙");
        Debug.Log($"-> 획득한 낚시 풀 개수: {availablePools.Count}개");

        foreach (var pool in availablePools)
        {
            Debug.Log($"   ㄴ 열린 풀: {pool.Tier}티어 (요구 라이센스: {pool.RequiredLicense})");
        }

        if (availablePools.Count == 0)
        {
            Debug.LogWarning("   ㄴ 현재 라이센스로 낚시할 수 있는 풀이 없습니다!");
        }
    }

    private void HandleTravelStarted(int targetMapId, float duration)
    {
        Debug.Log($"[TestDebug-이벤트] 이동 시작! 목표 ID: {targetMapId} / 예상 소요 시간: {duration:F2}초");
    }

    private void HandleStageChanged(int newStageDataId)
    {
        var currentData = m_stageSystem.CurrentStageData;
        Debug.Log($"[TestDebug-이벤트] 목적지 도착 완료! 현재 지역: {currentData?.Name}");
    }
}