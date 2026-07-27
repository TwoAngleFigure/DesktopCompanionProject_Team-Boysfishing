using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 탱크 창(계획 27 W1). 탱크 RT를 표시하는 것만 담당한다.
    /// 물고기 목록·재료 생산 경과·수족관 정보는 각각 <see cref="AquariumFishWindow"/>,
    /// <see cref="AquariumStatusWindow"/>로 분리됐다.
    /// 탱크 렌더는 <see cref="AquariumTankRenderer"/>가 상시 담당하며(창을 닫아도 생산·헤엄은 계속),
    /// 이 창은 그 RT를 바인딩만 하는 컨슈머다 — 월페이퍼 모드로 확장할 때 이 창만 교체하면 된다.
    /// 표시 전용이라 ViewModel이 없다.
    /// </summary>
    public class AquariumWindow : UIWindowBase
    {
        [Header("Tank (RT 컨슈머)")]
        [SerializeField] private RawImage m_tankImage;
        [SerializeField] private AquariumTankRenderer m_tankRenderer;

        [SerializeField] private Button m_closeButton;

        public override void Bind()
        {
            if (m_tankImage != null && m_tankRenderer != null)
            {
                m_tankImage.texture = m_tankRenderer.TankTexture;
            }

            if (m_closeButton != null) m_closeButton.onClick.AddListener(Close);
        }

        public override void Unbind()
        {
            if (m_closeButton != null) m_closeButton.onClick.RemoveListener(Close);
        }
    }
}
