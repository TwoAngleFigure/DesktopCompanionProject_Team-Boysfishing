using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 인벤토리 미러 개폐 창(UIWindowBase). 인벤토리 물고기 배치 + 아쿠아리움 물고기 회수 +
    /// 선택 물고기 상세(이름·성급·크기·생산재료·시간당 포인트).
    /// </summary>
    public class AquariumMirrorWindow : UIWindowBase
    {
        [Header("배치 목록(인벤토리)")]
        [SerializeField] private Transform m_deployRoot;

        [Header("회수 목록(아쿠아리움)")]
        [SerializeField] private Transform m_placedRoot;

        [Header("공용")]
        [SerializeField] private AquariumMirrorRow m_rowPrefab;

        [Header("상세 패널")]
        [SerializeField] private Image m_detailIcon;
        [SerializeField] private TMP_Text m_detailName;
        [SerializeField] private TMP_Text m_detailInfo;

        [SerializeField] private Button m_closeButton;

        private readonly AquariumMirrorViewModel m_vm = new();
        private readonly List<AquariumMirrorRow> m_deployRows = new();
        private readonly List<AquariumMirrorRow> m_placedRows = new();

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();
            m_vm.Deployable.Bind(RefreshDeploy);
            m_vm.Placed.Bind(RefreshPlaced);
            m_vm.Selected.Bind(RefreshDetail);

            if (m_closeButton != null) m_closeButton.onClick.AddListener(Close);
        }

        public override void Unbind()
        {
            m_vm.Deployable.Unbind(RefreshDeploy);
            m_vm.Placed.Unbind(RefreshPlaced);
            m_vm.Selected.Unbind(RefreshDetail);
            m_vm.Unbind();

            if (m_closeButton != null) m_closeButton.onClick.RemoveListener(Close);
        }

        private void RefreshDeploy(List<AquariumMirrorFishVD> list)
            => Fill(m_deployRows, m_deployRoot, list, "배치", vd => vd.DataId, id => m_vm.AddFishCommand.Execute(id));

        private void RefreshPlaced(List<AquariumMirrorFishVD> list)
            => Fill(m_placedRows, m_placedRoot, list, "회수", vd => vd.Index, i => m_vm.RemoveFishCommand.Execute(i));

        private void Fill(List<AquariumMirrorRow> rows, Transform root, List<AquariumMirrorFishVD> list,
                          string actionText, Func<AquariumMirrorFishVD, int> payload, Action<int> onAction)
        {
            if (m_rowPrefab == null || root == null || list == null) return;

            while (rows.Count < list.Count) rows.Add(Instantiate(m_rowPrefab, root));

            for (int i = 0; i < list.Count; i++)
            {
                var vd = list[i];
                if (vd == null) { rows[i].gameObject.SetActive(false); continue; }

                Sprite icon = null;
                if (!string.IsNullOrEmpty(vd.IconKey)) AssetProvider.TryGet(vd.IconKey, out icon);

                rows[i].gameObject.SetActive(true);
                rows[i].Set(vd, icon, actionText, payload(vd), onAction, OnSelect);
            }
            for (int i = list.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
        }

        private void OnSelect(int dataId) => m_vm.SelectFishCommand.Execute(dataId);

        private void RefreshDetail(AquariumMirrorFishVD vd)
        {
            bool has = vd != null;

            if (m_detailIcon != null)
            {
                Sprite icon = null;
                if (has && !string.IsNullOrEmpty(vd.IconKey)) AssetProvider.TryGet(vd.IconKey, out icon);
                m_detailIcon.enabled = icon != null;
                m_detailIcon.sprite = icon;
            }
            if (m_detailName != null) m_detailName.text = has ? vd.Name : "-";
            if (m_detailInfo != null)
            {
                m_detailInfo.text = has
                    ? $"성급 {Stars(vd.Star)}\n크기 {vd.Size:0.##}\n생산재료 {vd.MaterialName}\n시간당 {vd.PointsPerHour:0} pt"
                    : string.Empty;
            }
        }

        private static string Stars(int n) => n <= 0 ? "-" : new string('★', Mathf.Clamp(n, 1, 5));
    }
}
