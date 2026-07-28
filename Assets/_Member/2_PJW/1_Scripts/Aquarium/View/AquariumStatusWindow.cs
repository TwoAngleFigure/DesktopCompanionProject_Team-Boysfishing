using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 재료 생산 경과 + 수족관 정보 창(계획 27 W3).
    /// 위=재료별 원형 게이지 목록(정렬 바), 아래=현재 등급/다음 등급 대비 + 강화 비용·버튼.
    /// ※ '사용/최대' 수용량은 물고기 목록 창(AquariumFishWindow)에만 표시한다 — 여기서는 등급별 수용 한도만 보여준다.
    /// 1초 정산은 <see cref="AquariumStatusViewModel.ProgressTick"/>으로 들어와 게이지 값만 갱신한다.
    /// 재료 슬롯의 상세는 hover 팝업이 담당한다(계획 28) — 이 창이 그 상세의 공급자다.
    /// </summary>
    public class AquariumStatusWindow : UIWindowBase, IItemTooltipSource
    {
        [Header("재료 생산 경과")]
        [SerializeField] private SortBarView m_sortBar;
        [SerializeField] private Transform m_materialRoot;
        [SerializeField] private AquariumMaterialGaugeRow m_gaugeRowPrefab;

        [Header("수족관 정보 — 현재 등급")]
        [Tooltip("현재 등급 이름(AquariumUpgradeData.Name). 이름이 비면 'Lv.N'으로 폴백")]
        [SerializeField] private TMP_Text m_currentLevelText;
        [Tooltip("현재 등급의 수용 한도 (예: 수용량 5)")]
        [SerializeField] private TMP_Text m_currentCapacityText;

        [Header("수족관 정보 — 다음 등급")]
        [Tooltip("다음 등급 이름 (최대 등급이면 '-')")]
        [SerializeField] private TMP_Text m_nextLevelText;
        [Tooltip("다음 등급의 수용 한도 (최대 등급이면 '-')")]
        [SerializeField] private TMP_Text m_nextCapacityText;

        [Header("강화")]
        [Tooltip("강화 비용(골드 보유/필요 + 재료 보유/필요)")]
        [SerializeField] private TMP_Text m_costText;
        [SerializeField] private TMP_Text m_blockReasonText;
        [SerializeField] private Button m_upgradeButton;

        [SerializeField] private Button m_closeButton;

        private readonly AquariumStatusViewModel m_vm = new();
        private readonly List<AquariumMaterialGaugeRow> m_rows = new();
        private List<AquariumMaterialVD> m_current = new();   // 진행도 갱신 시 다시 읽을 현재 목록

        private AquariumSystem m_aquarium;   // 툴팁의 아쿠아리움 지표 조립용

        public override void Bind()
        {
            m_aquarium = SystemManager.GetSystem<AquariumSystem>();

            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.Materials.Bind(RefreshMaterials);
            m_vm.ProgressTick.Bind(RefreshProgress);
            m_vm.Info.Bind(RefreshInfo);

            if (m_sortBar != null)
            {
                m_sortBar.Initialize(AquariumSort.MaterialSortLabels,
                    (int)m_vm.SortKey, m_vm.SortDirection,
                    (key, direction) => m_vm.SetSort((AquariumMaterialSortKey)key, direction));
            }

            if (m_upgradeButton != null) m_upgradeButton.onClick.AddListener(Upgrade);
            if (m_closeButton != null) m_closeButton.onClick.AddListener(Close);
        }

        public override void Unbind()
        {
            m_vm.Materials.Unbind(RefreshMaterials);
            m_vm.ProgressTick.Unbind(RefreshProgress);
            m_vm.Info.Unbind(RefreshInfo);
            m_vm.Unbind();

            if (m_sortBar != null) m_sortBar.Release();
            if (m_upgradeButton != null) m_upgradeButton.onClick.RemoveListener(Upgrade);
            if (m_closeButton != null) m_closeButton.onClick.RemoveListener(Close);

            m_aquarium = null;
        }

        /// <summary>
        /// 아쿠아리움 재료 생산 풀은 Entity가 없고 dataId만 있으므로 정의 기반 경로로 만든다.
        /// 재료 툴팁은 헤더만 표시하므로 시스템 지표는 넘기지 않는다.
        /// </summary>
        public ItemTooltipData BuildTooltip(ItemSlotVD vd)
        {
            if (vd == null || m_aquarium == null)
            {
                return null;
            }

            ItemData_Materials data = m_aquarium.GetMaterialData(vd.DataId);
            return data != null ? ItemTooltipBuilder.FromData(data) : null;
        }

        private void Upgrade() => m_vm.UpgradeCommand.Execute();

        private void RefreshMaterials(List<AquariumMaterialVD> list)
        {
            if (list == null || m_gaugeRowPrefab == null || m_materialRoot == null)
            {
                return;
            }
            m_current = list;

            while (m_rows.Count < list.Count) m_rows.Add(Instantiate(m_gaugeRowPrefab, m_materialRoot));

            AquariumMaterialSortKey sortKey = m_vm.SortKey;
            for (int i = 0; i < list.Count; i++)
            {
                AquariumMaterialVD vd = list[i];
                ItemSlotVD slotVD = m_aquarium != null
                    ? ItemSlotVD.FromData(m_aquarium.GetMaterialData(vd.MaterialId))
                    : null;

                Sprite icon = null;
                if (string.IsNullOrEmpty(vd.IconKey) == false) AssetProvider.TryGet(vd.IconKey, out icon);

                m_rows[i].gameObject.SetActive(true);
                m_rows[i].Set(vd, slotVD, icon, this, sortKey);
            }
            for (int i = list.Count; i < m_rows.Count; i++) m_rows[i].gameObject.SetActive(false);
        }

        // 1초 정산 — VD 객체는 그대로 두고 값만 바뀌었으므로 게이지·텍스트만 다시 그린다.
        private void RefreshProgress(int tick)
        {
            AquariumMaterialSortKey sortKey = m_vm.SortKey;
            for (int i = 0; i < m_current.Count && i < m_rows.Count; i++)
            {
                m_rows[i].SetProgress(m_current[i], sortKey);
            }
        }

        private void RefreshInfo(AquariumInfoVD vd)
        {
            if (vd == null)
            {
                return;
            }

            // 현재 등급 — '사용/최대'가 아니라 등급이 주는 수용 '한도'다(사용량은 물고기 목록 창 담당).
            if (m_currentLevelText != null) m_currentLevelText.text = vd.LevelName;
            if (m_currentCapacityText != null) m_currentCapacityText.text = $"수용량 {vd.MaxCapacity}";

            // 다음 등급 — 최대 등급이면 두 칸 모두 '-'
            if (m_nextLevelText != null) m_nextLevelText.text = vd.HasNext ? vd.NextLevelName : "-";
            if (m_nextCapacityText != null) m_nextCapacityText.text = vd.HasNext ? $"수용량 {vd.NextMaxCapacity}" : "-";

            if (m_costText != null)
            {
                m_costText.text = vd.HasNext
                    ? $"골드 {vd.GoldOwned:N0} / {vd.GoldCost:N0}"
                      + (string.IsNullOrEmpty(vd.MaterialSummary) ? string.Empty : $"\n{vd.MaterialSummary}")
                    : "최대 등급";
            }
            if (m_blockReasonText != null) m_blockReasonText.text = vd.CanUpgrade ? string.Empty : vd.BlockReason;
            if (m_upgradeButton != null) m_upgradeButton.interactable = vd.CanUpgrade;
        }
    }
}
