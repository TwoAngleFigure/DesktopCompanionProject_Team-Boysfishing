using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>아쿠아리움 창의 총 생산 재료 행(아이콘 + 누적/요구 포인트 + 보류).</summary>
    public class AquariumMaterialRow : MonoBehaviour
    {
        [SerializeField] private Image m_icon;
        [SerializeField] private TMP_Text m_label;

        public void Set(AquariumMaterialVD vd, Sprite icon)
        {
            if (vd == null) return;
            if (m_icon != null)
            {
                m_icon.enabled = icon != null;
                m_icon.sprite = icon;
            }
            if (m_label != null)
            {
                string pending = vd.Pending > 0 ? $"   (보류 {vd.Pending})" : "";
                m_label.text = $"{vd.MaterialName}   {vd.Points}/{vd.Required}pt{pending}";
            }
        }
    }
}
