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
    /// ※ 계획 27의 정식 창(AquariumFishWindow / AquariumStatusWindow) 프리팹이 완성되면 제거한다.
    ///    그 전까지 시스템 계층(개체 단위 배치/회수·선검사·강화)을 프리팹 없이 검증하는 용도다.
    ///
    /// 기능:
    ///  - 인벤토리 물고기를 '개체 단위'로 나열(성급·크기·분당 포인트) + [배치] 버튼(불가 사유 표시).
    ///  - 배치된 물고기 목록 + 다음 생산까지 남은 시간 + [회수] 버튼(회수 선검사 사유 표시).
    ///  - 생산 중인 모든 재료(누적/요구 포인트, 시간당 개수, 보류).
    ///  - 수용량(used/max)·등급·강화 버튼(골드/재료 비용·부족 사유).
    ///
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
            GUILayout.BeginArea(new Rect(12, 12, 460, Screen.height - 24), GUI.skin.box);

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

        // ── 수용량·강화 ──
        private void DrawCapacity()
        {
            GUILayout.Label($"<b>수용량</b> {m_aquarium.UsedCapacity} / {m_aquarium.MaxCapacity}  (Lv.{m_aquarium.UpgradeLevel})", RichLabel());

            AquariumSystem.AquariumUpgradeInfo info = m_aquarium.GetUpgradeInfo();
            if (info.HasNext == false)
            {
                GUILayout.Label("최대 등급");
                return;
            }

            GUI.enabled = info.CanUpgrade;
            string nextName = string.IsNullOrWhiteSpace(info.NextName) ? $"Lv.{info.NextLevel}" : info.NextName;
            if (GUILayout.Button($"강화 → {nextName} (최대 {info.NextMaxCapacity})  비용: {FormatCosts(info)}"))
            {
                bool ok = m_aquarium.TryUpgrade();
                Debug.Log($"[AquariumTestUI] TryUpgrade → {ok}");
            }
            GUI.enabled = true;

            if (info.CanUpgrade == false)
            {
                GUILayout.Label($"<color=#e06060>{info.BlockReason}</color>", RichLabel());
            }
        }

        // ── 인벤토리 물고기(개체 단위) ──
        private void DrawDeployable()
        {
            GUILayout.Label("<b>인벤토리 물고기 (개체)</b>", RichLabel());
            if (m_inventory == null)
            {
                GUILayout.Label("InventorySystem 없음");
                return;
            }

            EntityHandle[] slots = m_inventory.GetSlots(ItemType.Fish);
            EntityHandle addTarget = default;
            bool hasAddTarget = false;
            int shown = 0;

            m_scrollDeploy = GUILayout.BeginScrollView(m_scrollDeploy, GUILayout.Height(170));
            for (int i = 0; i < slots.Length; i++)
            {
                if (EntityManager.Get<Entity_Fish>(slots[i]) == null) continue;

                AquariumSystem.AquariumFishInfo info = m_aquarium.BuildFishInfo(slots[i]);
                if (info.IsValid == false) continue;
                shown++;

                bool canPlace = m_aquarium.CanPlaceFish(slots[i], out string reason);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{info.Name} T{info.Tier} {info.Rarity} {Stars((int)info.Star)} " +
                                $"크기 {info.Size:0.##} → {info.MaterialName} {info.PointsPerMinute:0.#}pt/분",
                    GUILayout.Width(360));

                GUI.enabled = canPlace;
                if (GUILayout.Button("배치", GUILayout.Width(60)))
                {
                    addTarget = slots[i];
                    hasAddTarget = true;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                if (canPlace == false)
                {
                    GUILayout.Label($"   <color=#e06060>{reason}</color>", RichLabel());
                }
            }
            GUILayout.EndScrollView();

            if (shown == 0)
            {
                GUILayout.Label("보유 물고기 없음 — InventoryDebugBridge로 추가하세요.");
            }
            if (hasAddTarget)   // 클릭은 지연 적용(레이아웃 그룹 균형 유지 후 처리)
            {
                Debug.Log($"[AquariumTestUI] AddFish → {m_aquarium.AddFish(addTarget)}");
            }
        }

        // ── 배치된 물고기 + 남은 시간 ──
        private void DrawPlaced()
        {
            GUILayout.Label("<b>수족관 물고기</b>", RichLabel());
            List<AquariumSystem.AquariumFishInfo> list = m_aquarium.GetPlacedFishInfos();
            if (list.Count == 0)
            {
                GUILayout.Label("배치된 물고기 없음");
                return;
            }

            EntityHandle removeTarget = default;
            bool hasRemoveTarget = false;

            m_scrollPlaced = GUILayout.BeginScrollView(m_scrollPlaced, GUILayout.Height(170));
            for (int i = 0; i < list.Count; i++)
            {
                AquariumSystem.AquariumFishInfo info = list[i];
                bool canRetrieve = m_aquarium.CanRetrieveFish(info.Handle, out string reason);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{info.Name} {Stars((int)info.Star)} 크기 {info.Size:0.##} → " +
                                $"{info.MaterialName} {info.PointsPerMinute:0.#}pt/분  남은 {FormatTime(info.RemainingSeconds)}",
                    GUILayout.Width(360));

                GUI.enabled = canRetrieve;
                if (GUILayout.Button("회수", GUILayout.Width(60)))
                {
                    removeTarget = info.Handle;
                    hasRemoveTarget = true;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                if (canRetrieve == false)
                {
                    GUILayout.Label($"   <color=#e06060>{reason}</color>", RichLabel());
                }
            }
            GUILayout.EndScrollView();

            if (hasRemoveTarget)
            {
                Debug.Log($"[AquariumTestUI] RemoveFish → {m_aquarium.RemoveFish(removeTarget)}");
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
                GUILayout.Label($"{m.MaterialName}: {m.Points}/{m.Required} pt  ({m.PerHour:0.#}개/h){pending}", RichLabel());
            }
            GUILayout.EndScrollView();
        }

        // ── 헬퍼 ──
        private static string Stars(int count) => count <= 0 ? "-" : new string('★', Mathf.Clamp(count, 1, 5));

        private static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private static string FormatCosts(AquariumSystem.AquariumUpgradeInfo info)
        {
            var parts = new List<string>();
            if (info.GoldCost > 0) parts.Add($"{info.GoldOwned}/{info.GoldCost}G");

            if (info.Materials != null)
            {
                for (int i = 0; i < info.Materials.Count; i++)
                {
                    AquariumSystem.MaterialRequirement req = info.Materials[i];
                    parts.Add($"{req.MaterialName} {req.Owned}/{req.Required}");
                }
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "무료";
        }

        private static GUIStyle s_rich;
        private static GUIStyle RichLabel()
        {
            if (s_rich == null) s_rich = new GUIStyle(GUI.skin.label) { richText = true };
            return s_rich;
        }
    }
}
