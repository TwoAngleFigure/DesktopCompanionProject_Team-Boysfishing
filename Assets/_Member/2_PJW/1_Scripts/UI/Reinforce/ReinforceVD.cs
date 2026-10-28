using System.Collections.Generic;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 강화창 1회분 표시 스냅샷. 문자열을 조립하지 않고 값 그대로 담으며, 문장 구성은 View가 담당한다.
    /// 보유 골드는 여기 넣지 않는다 — 판매 등으로 자주 바뀌는 값이라 섞으면 그때마다 행 전체를 다시 그리게 된다.
    /// </summary>
    public class ReinforceVD
    {
        /// <summary>강화 슬롯에 장비가 올라가 있는지. false면 창이 빈 상태 안내만 표시한다.</summary>
        public bool HasEquipment;

        public EntityHandle Handle;
        public string Name;
        public int Tier = 1;
        public int UpgradeLevel;
        public int MaxUpgradeLevel;
        public EquipmentMountingArea MountingArea;

        /// <summary>다음 강화 단계가 없다 = 최대 강화. 비용·증가분 구역을 감춘다.</summary>
        public bool IsMaxLevel;

        public readonly List<ReinforceStatVD> Stats = new();
        public readonly List<ReinforceMaterialVD> Materials = new();

        public int GoldCost;

        /// <summary>재료가 전부 충분한지. 골드 충족 여부는 View가 CurrentGold와 합쳐 판단한다.</summary>
        public bool MaterialsEnough = true;
    }

    /// <summary>
    /// 능력치 1행. 강화 단계 정의가 '도달 시 누적치'이므로 <see cref="Delta"/>는 다음 단계 값과 현재 값의 차다.
    /// </summary>
    public class ReinforceStatVD
    {
        public PlayerStat Stat;
        public ModifierOperation Operation;
        public float Current;
        public float Delta;
    }

    /// <summary>
    /// 강화 재료 1칸. 정의를 그대로 들고 있어 슬롯 표시와 툴팁을 View에서 바로 만들 수 있다.
    /// </summary>
    public class ReinforceMaterialVD
    {
        public ItemData_Materials Data;
        public int Owned;
        public int Required;

        public bool IsEnough => Owned >= Required;
    }
}
