using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DesktopCompanion.Data;

namespace DesktopCompanion.Views
{
    public class WorldMapUIView : UIViewBase
    {
        [Header("동적 생성 인프라 설정")]
        [SerializeField] private GameObject m_nodePrefab; 
        [SerializeField] private Transform m_nodeContainer;

        [Header("UI 제어")]
        public Button m_closeButton;

        private readonly WorldMapViewModel m_vm = new();
        private readonly List<GameObject> m_instantiatedNodes = new();

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            GenerateMapNodes();

            if (m_closeButton != null)
            {
                m_closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
        }

        public override void Unbind()
        {
            if (m_closeButton != null) m_closeButton.onClick.RemoveAllListeners();

            ClearMapNodes();
            m_vm.Unbind();
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

            Debug.Log($"[WorldMapUIView] 총 {m_instantiatedNodes.Count}개의 월드맵 노드가 동적 생성 및 바인딩 완료되었습니다.");
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
    }
}