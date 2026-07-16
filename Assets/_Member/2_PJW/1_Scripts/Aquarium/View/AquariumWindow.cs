using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 개폐 창(UIWindowBase). 탱크 RT 표시 + 물고기별 남은 시간/포인트 사이드 리스트
    /// + 총 생산 재료 아이콘 + 미러(배치/회수) 창 열기.
    /// 탱크 렌더는 <see cref="AquariumTankRenderer"/>가 상시 담당하며, 이 창은 그 RT를 표시만 한다.
    /// </summary>
    public class AquariumWindow : UIWindowBase
    {
        [Header("Tank (RT 컨슈머)")]
        [SerializeField] private RawImage m_tankImage;
        [SerializeField] private AquariumTankRenderer m_tankRenderer;

        [Header("물고기 상태 리스트")]
        [SerializeField] private Transform m_fishListRoot;
        [SerializeField] private AquariumFishRow m_fishRowPrefab;

        [Header("총 생산 재료")]
        [SerializeField] private Transform m_materialListRoot;
        [SerializeField] private AquariumMaterialRow m_materialRowPrefab;

        [Header("기타")]
        [SerializeField] private TMP_Text m_capacityText;
        [SerializeField] private Button m_openMirrorButton;
        [SerializeField] private AquariumMirrorWindow m_mirrorWindow;
        [SerializeField] private Button m_closeButton;

        private readonly AquariumWindowViewModel m_vm = new();
        private readonly List<AquariumFishRow> m_fishRows = new();
        private readonly List<AquariumMaterialRow> m_materialRows = new();

        public override void Bind()
        {
            if (m_tankImage != null && m_tankRenderer != null)
            {
                m_tankImage.texture = m_tankRenderer.TankTexture;
            }

            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();
            m_vm.FishList.Bind(RefreshFish);
            m_vm.Materials.Bind(RefreshMaterials);
            m_vm.CapacityText.Bind(RefreshCapacity);

            if (m_openMirrorButton != null) m_openMirrorButton.onClick.AddListener(OpenMirror);
            if (m_closeButton != null) m_closeButton.onClick.AddListener(Close);
        }

        public override void Unbind()
        {
            m_vm.FishList.Unbind(RefreshFish);
            m_vm.Materials.Unbind(RefreshMaterials);
            m_vm.CapacityText.Unbind(RefreshCapacity);
            m_vm.Unbind();

            if (m_openMirrorButton != null) m_openMirrorButton.onClick.RemoveListener(OpenMirror);
            if (m_closeButton != null) m_closeButton.onClick.RemoveListener(Close);
        }

        private void RefreshCapacity(string text)
        {
            if (m_capacityText != null) m_capacityText.text = text;
        }

        private void RefreshFish(List<AquariumFishVD> list)
        {
            if (list == null) return;
            EnsureRows(m_fishRows, m_fishRowPrefab, m_fishListRoot, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                m_fishRows[i].gameObject.SetActive(true);
                m_fishRows[i].Set(list[i]);
            }
            for (int i = list.Count; i < m_fishRows.Count; i++) m_fishRows[i].gameObject.SetActive(false);
        }

        private void RefreshMaterials(List<AquariumMaterialVD> list)
        {
            if (list == null) return;
            EnsureRows(m_materialRows, m_materialRowPrefab, m_materialListRoot, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                Sprite icon = null;
                if (!string.IsNullOrEmpty(list[i].IconKey)) AssetProvider.TryGet(list[i].IconKey, out icon);
                m_materialRows[i].gameObject.SetActive(true);
                m_materialRows[i].Set(list[i], icon);
            }
            for (int i = list.Count; i < m_materialRows.Count; i++) m_materialRows[i].gameObject.SetActive(false);
        }

        private void OpenMirror()
        {
            if (m_mirrorWindow != null) m_mirrorWindow.gameObject.SetActive(true);
        }

        private static void EnsureRows<T>(List<T> rows, T prefab, Transform root, int count) where T : Component
        {
            if (prefab == null || root == null) return;
            while (rows.Count < count) rows.Add(Instantiate(prefab, root));
        }
    }
}
