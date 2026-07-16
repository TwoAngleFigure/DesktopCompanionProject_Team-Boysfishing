using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 창의 ViewModel. AquariumSystem 상태를 표시용 VD로 변환한다.
    /// 08의 1초 Tick 정산이 OnAquariumChanged를 발행하므로 창이 열린 동안 초 단위로 자동 갱신된다.
    /// </summary>
    public class AquariumWindowViewModel : UIViewModelBase
    {
        private AquariumSystem m_aquarium;

        public readonly BindableProperty<List<AquariumFishVD>> FishList = new(new List<AquariumFishVD>());
        public readonly BindableProperty<List<AquariumMaterialVD>> Materials = new(new List<AquariumMaterialVD>());
        public readonly BindableProperty<string> CapacityText = new(string.Empty);

        public override void Bind()
        {
            m_aquarium = SystemManager.GetSystem<AquariumSystem>();
            if (m_aquarium != null) m_aquarium.OnAquariumChanged += Refresh;
            Refresh();
        }

        public override void Unbind()
        {
            if (m_aquarium != null) m_aquarium.OnAquariumChanged -= Refresh;
            m_aquarium = null;
        }

        private void Refresh()
        {
            if (m_aquarium == null) return;

            var matStatuses = m_aquarium.GetMaterialStatuses();
            var matById = new Dictionary<int, (int points, int required)>();
            foreach (var m in matStatuses) matById[m.MaterialId] = (m.Points, m.Required);

            // 물고기별 상태
            var fish = new List<AquariumFishVD>();
            foreach (var s in m_aquarium.GetFishStatuses())
            {
                int remainPts = matById.TryGetValue(s.MaterialId, out var mp) ? Mathf.Max(0, mp.required - mp.points) : 0;
                fish.Add(new AquariumFishVD
                {
                    Index = s.Index,
                    Name = s.FishName,
                    MaterialName = s.MaterialName,
                    RemainingSeconds = s.RemainingSeconds,
                    RemainingPoints = remainPts,
                    PointsPerHour = m_aquarium.PointsPerHour(m_aquarium.GetFishData(s.FishDataId)),
                });
            }
            FishList.Value = fish;

            // 총 생산 재료
            var mats = new List<AquariumMaterialVD>();
            foreach (var m in matStatuses)
            {
                var data = m_aquarium.GetMaterialData(m.MaterialId);
                mats.Add(new AquariumMaterialVD
                {
                    MaterialName = m.MaterialName,
                    IconKey = data != null ? AssetKeys.Of(data, AssetUsage.Icon) : null,
                    Points = m.Points,
                    Required = m.Required,
                    Pending = m.Pending,
                });
            }
            Materials.Value = mats;

            CapacityText.Value = $"수용량 {m_aquarium.UsedCapacity} / {m_aquarium.MaxCapacity}  (Lv.{m_aquarium.UpgradeLevel})";
        }
    }
}
