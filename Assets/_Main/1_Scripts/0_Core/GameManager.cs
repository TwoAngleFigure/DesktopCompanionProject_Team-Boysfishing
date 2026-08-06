using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;
using DesktopCompanion.Views;

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

        [Header("View")]
        [SerializeField] private WorldManager m_worldManager;
        [SerializeField] private UIManager m_uiManager;

        private DataManager m_dataManager;
        private EntityManager m_entityManager;
        private SystemManager m_systemManager;
        private SaveManager m_saveManager;
        private AssetProvider m_assetProvider;

        private void Awake()
        {
            // 1) Data — StreamingAssets JSON → Resources SO 로드(D14).
            //    로드 완료 후, 테스트 모드면 테스트 SO를 적용한다: 같은 (타입·ID)는 교체, 없는 ID는 추가(D9).
            m_dataManager = new DataManager();
            m_dataManager.Load();
            if (m_useTestData)
            {
                m_dataManager.OverrideWithTestData(m_testData);
            }

            // 2) Entity — DataManager 주입 + 팩토리 등록
            m_entityManager = new EntityManager(m_dataManager);
            RegisterEntityFactories();

            // 3) System — 등록 후 Phase 1(Initialize): 각 System의 원시 상태 기본값만 구성
            m_systemManager = new SystemManager(m_entityManager, m_dataManager);
            RegisterSystems();
            m_systemManager.InitializePhase1();

            // 4) World/UI Manager 탐색 및 할당
            m_worldManager = FindAnyObjectByType<WorldManager>();
            m_uiManager = FindAnyObjectByType<UIManager>();

            // 5) Save — ISaveable 자동 등록 후 로드(원시 상태를 저장값으로 덮어쓰기, §15.3).
            //    반드시 Phase 2 이전에 복원해야 파생·교차 계산이 저장값을 반영한다(R1).
            m_saveManager = new SaveManager();
            foreach (var system in m_systemManager.AllSystems)
            {
                if (system is ISaveable saveable)
                {
                    m_saveManager.Register(saveable);
                }
            }
            m_saveManager.TryLoad();

            // 6) System — Phase 2(PostInitialize): 타 System 조회·구독·파생 계산(복원값 반영)
            m_systemManager.InitializePhase2();
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

            // View 초기화 지점 — m_systemManager·m_assetProvider 주입.
            // WorldManager가 등록된 WorldViewBase 유닛들에 의존성을 내려주고 Bind()를 호출한다.
            if (m_worldManager != null)
            {
                m_worldManager.Initialize(m_systemManager, m_entityManager, m_assetProvider);
            }
            else
            {
                Debug.LogWarning("[GameManager] WorldManager 미할당 — 씬에서 참조를 연결하세요.");
            }

            if (m_uiManager != null)
            {
                m_uiManager.Initialize(m_systemManager, m_entityManager, m_assetProvider);
            }
            else
            {
                Debug.LogWarning("[GameManager] UIManager 미할당 — 씬에서 참조를 연결하세요.");
            }
        }

        // 유일한 MonoBehaviour의 프레임 루프를 ITickable System에 중계한다(System은 plain C# 유지).
        private void Update()
        {
            m_systemManager?.TickAll(Time.deltaTime);
        }

        private void OnApplicationQuit()
        {
            m_saveManager?.Save();   // 종료 시 자동 저장(D15)
        }

        // 팀원이 만든 System을 여기서 등록한다. SystemManager가 EntityManager/자기 자신을 주입한다.
        // (구체 System 타입을 아는 곳은 composition root인 GameManager뿐)
        // 초기화 순서 = Register 호출 순서: PlayerSystem → InventorySystem → FishingSystem → StageSystem → AquariumSystem.
        // (2단계 초기화로 순서 민감도는 낮지만, 같은 phase 내 tie-break를 위해 결정적으로 고정)
        private void RegisterSystems()
        {
            m_systemManager.Register(new DesktopCompanion.Systems.PlayerSystem());

            m_systemManager.Register(new DesktopCompanion.Systems.CurrencySystem());

            m_systemManager.Register(new DesktopCompanion.Systems.InventorySystem());

            m_systemManager.Register(new DesktopCompanion.Systems.ShopSystem());

            m_systemManager.Register(new DesktopCompanion.Systems.FishCollectionSystem());

            m_systemManager.Register(new DesktopCompanion.Systems.FishingSettingSystem());

            m_systemManager.Register(new DesktopCompanion.Systems.FishingSystem());

            m_systemManager.Register(new DesktopCompanion.Systems.StageSystem());

            m_systemManager.Register(new DesktopCompanion.Systems.AquariumSystem());   // Inventory 이후(생산물 지급 의존)

            m_systemManager.Register(new DesktopCompanion.Systems.MixtureSystem());
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
