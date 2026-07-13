using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// [테스트 UI] 아쿠아리움 검증용 IMGUI 패널(씬 배선 불필요 — GameObject에 이 컴포넌트만 추가).
    /// UIViewBase 자가 등록으로 부팅 시 SystemManager/EntityManager를 주입받는다.
    ///
    /// 기능:
    ///  - 인벤토리를 참조해 '배치 가능한 물고기' 목록(종별) + 생산 재료/생산량 표시, [배치] 버튼.
    ///  - 아쿠아리움에 배치된 물고기 목록 + 다음 생산까지 남은 시간, [회수] 버튼.
    ///  - 아쿠아리움이 생산 중인 모든 재료 목록(누적/요구 포인트, 보류) 표시.
    ///  - 수용량(used/max)·레벨·업그레이드 버튼.
    ///
    /// ※ 배치는 인벤토리를 '소비하지 않는다'(종 dataId 참조). 소비/반환이 필요하면 별도 연결.
    /// ※ IMGUI라 에디터 Play 검증용. 오버레이 빌드에서 클릭하려면 uGUI 변환 필요(클릭관통은 GraphicRaycaster 감지).
    /// </summary>
    public class AquariumTestUI : UIViewBase
    {
        private AquariumSystem m_aquarium;
        private InventorySystem m_inventory;

        private Vector2 m_scrollDeploy;
        private Vector2 m_scrollPlaced;
        private Vector2 m_scrollMats;

        public override void Bind()
        {
            m_aquarium = SystemManager.GetSystem<AquariumSystem>();
            m_inventory = SystemManager.GetSystem<InventorySystem>();
        }

        public override void Unbind()
        {
            m_aquarium = null;
            m_inventory = null;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 400, Screen.height - 24), GUI.skin.box);

            GUILayout.Label("<b>아쿠아리움 테스트</b>", RichLabel());
            if (m_aquarium == null)
            {
                GUILayout.Label("부팅 대기 중… (GameManager/UIManager 필요)");
                GUILayout.EndArea();
                return;
            }

            DrawCapacity();
            GUILayout.Space(6);
            DrawDeployable();
            GUILayout.Space(6);
            DrawPlaced();
            GUILayout.Space(6);
            DrawMaterials();

            GUILayout.EndArea();
        }

        // ── 수용량·업그레이드 ──
        private void DrawCapacity()
        {
            GUILayout.Label($"<b>수용량</b> {m_aquarium.UsedCapacity} / {m_aquarium.MaxCapacity}  (Lv.{m_aquarium.UpgradeLevel})", RichLabel());
            var next = m_aquarium.NextUpgrade;
            if (next != null)
            {
                string costs = FormatCosts(next);
                if (GUILayout.Button($"업그레이드 → Lv.{m_aquarium.UpgradeLevel + 1} (최대 {next.MaxCapacity}) 비용: {costs}"))
                {
                    bool ok = m_aquarium.TryUpgrade();
                    Debug.Log($"[AquariumTestUI] TryUpgrade → {ok}");
                }
            }
            else
            {
                GUILayout.Label("최대 레벨");
            }
        }

        // ── 배치 가능한 물고기(인벤토리 참조) ──
        private void DrawDeployable()
        {
            GUILayout.Label("<b>배치 가능한 물고기 (인벤토리)</b>", RichLabel());
            if (m_inventory == null)
            {
                GUILayout.Label("InventorySystem 없음");
                return;
            }

            // 종(dataId)별로 집계 — 집계 시점에 종 정의(ItemData_Fish)를 함께 보관
            var counts = new Dictionary<int, int>();
            var datas = new Dictionary<int, ItemData_Fish>();
            var order = new List<int>();
            EntityHandle[] slots = m_inventory.GetSlots(ItemType.Fish);
            for (int i = 0; i < slots.Length; i++)
            {
                var fish = EntityManager.Get<Entity_Fish>(slots[i]);   // 빈/유실 핸들은 null
                if (fish == null) continue;
                int id = fish.DataId;
                if (!counts.ContainsKey(id)) { counts[id] = 0; datas[id] = fish.ItemData; order.Add(id); }
                counts[id]++;
            }

            if (order.Count == 0)
            {
                GUILayout.Label("보유 물고기 없음 — InventoryDebugBridge로 추가하세요.");
                return;
            }

            m_scrollDeploy = GUILayout.BeginScrollView(m_scrollDeploy, GUILayout.Height(150));
            foreach (int id in order)
            {
                ItemData_Fish data = datas[id];
                if (data == null) continue;
                string matName = data.AquariumMaterial != null ? data.AquariumMaterial.Name : "-";

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{data.Name} x{counts[id]}  → {matName} +{data.AquariumProduceAmount} (cap {data.AquariumCapacity})",
                    GUILayout.Width(300));
                if (GUILayout.Button("배치", GUILayout.Width(60)))
                {
                    bool ok = m_aquarium.AddFish(id);
                    Debug.Log($"[AquariumTestUI] AddFish({id}) → {ok}{(ok ? "" : " (수용량 초과?)")}");
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        // ── 배치된 물고기 + 남은 시간 ──
        private void DrawPlaced()
        {
            GUILayout.Label("<b>아쿠아리움 물고기</b>", RichLabel());
            List<AquariumSystem.FishSlotStatus> list = m_aquarium.GetFishStatuses();
            if (list.Count == 0)
            {
                GUILayout.Label("배치된 물고기 없음");
                return;
            }

            int removeIndex = -1;   // 클릭은 지연 적용(레이아웃 그룹 균형 유지 후 처리)
            m_scrollPlaced = GUILayout.BeginScrollView(m_scrollPlaced, GUILayout.Height(150));
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{s.FishName} → {s.MaterialName} +{s.ProduceAmount}  남은 {FormatTime(s.RemainingSeconds)}",
                    GUILayout.Width(300));
                if (GUILayout.Button("회수", GUILayout.Width(60))) removeIndex = s.Index;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            if (removeIndex >= 0)
            {
                m_aquarium.RemoveFishAt(removeIndex);
                Debug.Log($"[AquariumTestUI] RemoveFishAt({removeIndex})");
            }
        }

        // ── 생산 재료 목록 ──
        private void DrawMaterials()
        {
            GUILayout.Label("<b>생산 재료</b>", RichLabel());
            List<AquariumSystem.MaterialStatus> mats = m_aquarium.GetMaterialStatuses();
            if (mats.Count == 0)
            {
                GUILayout.Label("생산 중인 재료 없음");
                return;
            }

            m_scrollMats = GUILayout.BeginScrollView(m_scrollMats, GUILayout.Height(120));
            foreach (var m in mats)
            {
                string pending = m.Pending > 0 ? $"  <color=#e0a000>보류 {m.Pending}</color>" : "";
                GUILayout.Label($"{m.MaterialName}: {m.Points}/{m.Required} pt{pending}", RichLabel());
            }
            GUILayout.EndScrollView();
        }

        // ── 헬퍼 ──
        private static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int t = Mathf.CeilToInt(seconds);
            return $"{t / 60:00}:{t % 60:00}";
        }

        private string FormatCosts(AquariumUpgradeData data)
        {
            var costs = data.MaterialCosts;
            if (costs == null || costs.Length == 0) return data.UpgradeCost > 0 ? $"{data.UpgradeCost}G" : "무료";
            var parts = new List<string>();
            foreach (var c in costs)
            {
                if (c?.Material == null) continue;
                parts.Add($"{c.Material.Name}x{c.Count}");
            }
            if (data.UpgradeCost > 0) parts.Add($"{data.UpgradeCost}G");
            return string.Join(", ", parts);
        }

        private static GUIStyle s_rich;
        private static GUIStyle RichLabel()
        {
            if (s_rich == null) s_rich = new GUIStyle(GUI.skin.label) { richText = true };
            return s_rich;
        }
    }
}
