using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;

namespace DesktopCompanion.Core
{
    /// <summary>
    /// 최상위 composition root. 부팅 시 하위 매니저를 생성·주입하고 Entity 팩토리를 등록한다.
    /// 부팅 이후에는 하위의 도메인 로직을 직접 호출하지 않는다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("테스트 (수동 Play 검증, D9)")]
        [SerializeField] private bool m_useTestData;
        [SerializeField] private List<GameData> m_testData = new();

        private DataManager m_dataManager;
        private EntityManager m_entityManager;
        private SystemManager m_systemManager;
        private SaveManager m_saveManager;
        private DesktopCompanion.Views.AssetProvider m_assetProvider;

        private void Awake()
        {
            // 1) Data — 소스 우선순위: 테스트 주입 → StreamingAssets JSON → Resources SO (D14)
            m_dataManager = new DataManager();
            m_dataManager.Load(m_useTestData ? m_testData : null);

            // 2) Entity — DataManager 주입 + 팩토리 등록
            m_entityManager = new EntityManager(m_dataManager);
            RegisterEntityFactories();

            // 3) System — 의존성 주입, System 등록 후 일괄 초기화(기본 상태 구성)
            m_systemManager = new SystemManager(m_entityManager, m_dataManager);
            RegisterSystems();
            m_systemManager.InitializeAll();

            // 4) Save — ISaveable 자동 등록 후 로드(저장된 가변 상태 덮어쓰기, §15.3)
            m_saveManager = new SaveManager();
            foreach (var system in m_systemManager.AllSystems)
            {
                if (system is ISaveable saveable)
                {
                    m_saveManager.Register(saveable);
                }
            }
            m_saveManager.TryLoad();

            // 5) (후속) UI / World — SystemManager 주입
        }

        // 부팅 2부(비동기, D18): 에셋 프리로드 → 뷰 초기화. Awake(동기 조립)와 분리.
        private async void Start()
        {
            try
            {
                m_assetProvider = new DesktopCompanion.Views.AssetProvider();
                await m_assetProvider.PreloadAsync();
                OnBootCompleted();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] 부팅(프리로드) 실패: {e}");
            }
        }

        private void OnBootCompleted()
        {
            Debug.Log("[GameManager] 부팅 완료 — 데이터·시스템·세이브·에셋 준비됨");
            // (후속) UIManager/WorldManager 초기화 지점 — m_assetProvider·m_systemManager 주입
        }

        private void OnApplicationQuit()
        {
            m_saveManager?.Save();   // 종료 시 자동 저장(D15)
        }

        // 팀원이 만든 System을 여기서 등록한다. SystemManager가 EntityManager/자기 자신을 주입한다.
        // (구체 System 타입을 아는 곳은 composition root인 GameManager뿐)
        private void RegisterSystems()
        {
            // ★ Save/Load 검증용 임시 System — 검증 완료 후 이 줄과 SaveTestSystem.cs 제거 가능
            m_systemManager.Register(new DesktopCompanion.Systems.SaveTestSystem());

            m_systemManager.Register(new DesktopCompanion.Systems.InventorySystem());
            // ★ Inventory 검증용 디버거 System - 검증 완료 후 이 줄과 InventoryDebugSystem.cs 제거 가능
            m_systemManager.Register(new DesktopCompanion.Systems.InventoryDebugSystem());
        }

        // Data 타입 ↔ Entity 매핑 등록. 새 계열은 여기 한 줄 추가(EntityManager 본체는 불변).
        private void RegisterEntityFactories()
        {
            // 아이템 계열
            m_entityManager.RegisterFactory(typeof(ItemData_Fish),
                (id, data) => new Entity_Fish(id, (ItemData_Fish)data));
            m_entityManager.RegisterFactory(typeof(ItemData_Equipment),
                (id, data) => new Entity_Equipment(id, (ItemData_Equipment)data));
            m_entityManager.RegisterFactory(typeof(ItemData_Materials),
                (id, data) => new Entity_Materials(id, (ItemData_Materials)data));
            m_entityManager.RegisterFactory(typeof(ItemData_Consumables),
                (id, data) => new Entity_Consumables(id, (ItemData_Consumables)data));

            // 전투 계열 (일시 상태 — 세이브 대상 아님)
            m_entityManager.RegisterFactory(typeof(BattleFishData),
                (id, data) => new Entity_BattleFish(id, (BattleFishData)data));

            // 플레이어 (세이브 핵심 대상)
            m_entityManager.RegisterFactory(typeof(PlayerData),
                (id, data) => new Entity_Player(id, (PlayerData)data));

            // ※ StageData/LicenseData는 등록하지 않음 → Entity화되지 않는 설정 데이터(System이 직접 조회).
        }
    }
}
