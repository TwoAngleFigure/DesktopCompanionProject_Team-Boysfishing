using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class WorldMapUIView : UIWindowBase
    {
        [Header("동적 생성 인프라 설정")]
        [SerializeField] private GameObject m_nodePrefab;
        [SerializeField] private Transform m_nodeContainer;

        [Header("플레이어 아이콘 (내 배)")]
        [SerializeField] private RectTransform m_shipIconRect;

        [Header("UI 제어")]
        public Button m_closeButton;

        [Header("스테이지별 육각형 클러스터(메인 육각형 + 보조 스탬프) 소속 해역 설정")]
        [Tooltip("각 스테이지 노드를 중심으로 하는 육각형 해역 및 보조 육각 스탬프 설정 리스트 (교집합 진입 시 중심점 직선거리 우선순위 판정)")]
        [SerializeField] private List<StageAreaRectConfig> m_stageAreaRects = new List<StageAreaRectConfig>();

        [Tooltip("체크 시 유니티 Scene 뷰에서 각 스테이지의 육각형 해역 및 보조 스탬프 범위를 기즈모로 표시합니다.")]
        [SerializeField] private bool m_drawAreaGizmos = true;

        public IReadOnlyList<StageAreaRectConfig> StageAreaRects => m_stageAreaRects;

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
            m_isOpenByPlayer = false;
            base.Hide();
            gameObject.SetActive(false);
        }

        new public void Hide()
        {
            if (m_isOpenByPlayer)
            {
                // Btn_CloseMap의 인스펙터 Persistent 이벤트에서 Hide()를 호출하는 경우도 정상 닫기 처리
                var selected = UnityEngine.EventSystems.EventSystem.current != null
                    ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                    : null;

                if (selected != null && (selected.name.Contains("Cancel") || selected.name.Contains("cancel")))
                {
                    return;
                }
            }

            m_isOpenByPlayer = false;
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

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            EnsureShipIcon();
            GenerateMapNodes();

            if (m_closeButton != null)
            {
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

        private void EnsureShipIcon()
        {
            if (m_shipIconRect != null) return;

            var rects = GetComponentsInChildren<RectTransform>(true);
            foreach (var r in rects)
            {
                if (r != null && r.name == "ShipIcon")
                {
                    m_shipIconRect = r;
                    break;
                }
            }

            if (m_shipIconRect == null && m_nodeContainer != null)
            {
                GameObject iconObj = new GameObject("ShipIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                m_shipIconRect = iconObj.GetComponent<RectTransform>();
                m_shipIconRect.SetParent(m_nodeContainer, false);
                m_shipIconRect.anchorMin = new Vector2(0.5f, 0.5f);
                m_shipIconRect.anchorMax = new Vector2(0.5f, 0.5f);
                m_shipIconRect.pivot = new Vector2(0.5f, 0.5f);
                m_shipIconRect.sizeDelta = new Vector2(30f, 30f);

                var img = iconObj.GetComponent<Image>();
                img.color = new Color(0.2f, 0.85f, 1f, 1f);
                img.raycastTarget = false;
            }
        }

        private void Update()
        {
            if (m_vm == null) return;
            if (m_shipIconRect == null) EnsureShipIcon();
            if (m_shipIconRect == null) return;

            if (m_vm.IsSystemReady)
            {
                m_shipIconRect.anchoredPosition = m_vm.CurrentShipPosition;

                if (m_nodeContainer != null && m_shipIconRect.parent != m_nodeContainer)
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

            if (m_shipIconRect != null)
            {
                m_shipIconRect.SetAsLastSibling();
            }
        }

        private void ClearMapNodes()
        {
            foreach (var node in m_instantiatedNodes)
            {
                if (node != null)
                    Destroy(node);
            }
            m_instantiatedNodes.Clear();
        }

        private void OnValidate()
        {
            StageSystem.PopulateDefaultStageAreaRects(ref m_stageAreaRects, null);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!m_drawAreaGizmos) return;
            StageSystem.PopulateDefaultStageAreaRects(ref m_stageAreaRects, null);

            Transform basis = m_nodeContainer != null ? m_nodeContainer : transform;
            Gizmos.matrix = basis.localToWorldMatrix;

            StageSystem.DrawStageHexAreaGizmos(m_stageAreaRects);

            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
