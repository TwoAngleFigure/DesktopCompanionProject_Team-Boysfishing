using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    public class WorldMapUIView : UIWindowBase
    {
        [Header("동적 생성 인프라 설정")]
        [SerializeField] private GameObject m_nodePrefab;
        [SerializeField] private Transform m_nodeContainer;

        [Header("플레이어 아이콘 (내 배)")] // ✨ 배 UI 연결 필드 추가
        [SerializeField] private RectTransform m_shipIconRect;

        [Header("UI 제어")]
        public Button m_closeButton;

        private readonly WorldMapViewModel m_vm = new();
        private readonly List<GameObject> m_instantiatedNodes = new();

        private bool m_isOpenByPlayer = false;

        public void OpenByPlayer()
        {
            m_isOpenByPlayer = true;
            gameObject.SetActive(true);
            Show();
        }

        public void CloseByPlayer()
        {
            var pointerData = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            Debug.LogWarning($"[WorldMapUIView] CloseByPlayer 호출됨! currentSelectedGameObject: {(pointerData != null ? pointerData.name : "null")}");

            m_isOpenByPlayer = false;
            base.Hide();
            gameObject.SetActive(false);
        }

        new public void Hide()
        {
            if (m_isOpenByPlayer)
            {
                return;
            }
            base.Hide();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
                cg.interactable = true;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Debug.LogWarning($"[WorldMapUIView] 미니맵이 꺼졌습니다! 꺼진 원인(스택 트레이스) 추적:\n{System.Environment.StackTrace}");
        }

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            GenerateMapNodes();

            if (m_closeButton != null)
            {
                // 🎯 100% 범인 해결: m_closeButton에 잘못 연결된 중지 버튼(Btn_HUDCancel)의 닫기 바인딩 무력화!
                if (m_closeButton.name.Contains("Cancel") || m_closeButton.name.Contains("cancel"))
                {
                    Debug.LogWarning($"[WorldMapUIView] m_closeButton에 중지 버튼({m_closeButton.name})이 잘못 바인딩되어 있어 닫기 이벤트를 제외합니다.");
                }
                else
                {
                    m_closeButton.onClick.AddListener(() => CloseByPlayer());
                }
            }
        }

        public override void Unbind()
        {
            if (m_closeButton != null) m_closeButton.onClick.RemoveAllListeners();

            ClearMapNodes();
            m_vm.Unbind();
        }

        // ✨ 실시간으로 배 아이콘의 위치를 업데이트하는 로직 추가
        private void Update()
        {
            if (m_vm == null || m_shipIconRect == null) return;

            if (m_vm.IsSystemReady)
            {
                // ViewModel을 통해 논리적 좌표를 가져와 배 UI 아이콘의 anchoredPosition에 대입
                m_shipIconRect.anchoredPosition = m_vm.CurrentShipPosition;

                if (m_shipIconRect.parent != m_nodeContainer)
                {
                    m_shipIconRect.SetParent(m_nodeContainer, false);
                }

                m_shipIconRect.SetAsLastSibling();
            }
        }

        private void GenerateMapNodes()
        {
            ClearMapNodes();

            if (m_nodePrefab == null || m_nodeContainer == null)
            {
                Debug.LogError("[WorldMapUIView] 프리팹 또는 컨테이너가 인스펙터에 지정되지 않았습니다.");
                return;
            }

            IReadOnlyList<StageData> stageDatas = m_vm.GetAllStageDatas();

            foreach (var stage in stageDatas)
            {
                if (stage == null) continue;

                GameObject nodeObj = Instantiate(m_nodePrefab, m_nodeContainer);
                m_instantiatedNodes.Add(nodeObj);

                var nodeItem = nodeObj.GetComponent<MapNodeItemView>();
                if (nodeItem != null)
                {
                    nodeItem.Setup(
                        stage.ID,
                        stage.Name,
                        stage.MapPosition,
                        stageId => m_vm.SelectAndMoveCommand?.Execute(stageId)
                    );
                }
            }
        }

        private void ClearMapNodes()
        {
            foreach (var node in m_instantiatedNodes)
            {
                if (node != null) Destroy(node);
            }
            m_instantiatedNodes.Clear();
        }
    }
}