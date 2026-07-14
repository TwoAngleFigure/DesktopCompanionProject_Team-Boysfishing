using UnityEngine;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>아쿠아리움 창의 물고기 상태 행(이름·남은 시간·재료까지 남은 포인트·시간당).</summary>
    public class AquariumFishRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_label;

        public void Set(AquariumFishVD vd)
        {
            if (m_label == null || vd == null) return;
            int t = Mathf.CeilToInt(Mathf.Max(0f, vd.RemainingSeconds));
            m_label.text = $"{vd.Name}   남은 {t / 60:00}:{t % 60:00}   {vd.MaterialName}까지 {vd.RemainingPoints}pt   {vd.PointsPerHour:0}/h";
        }
    }
}
