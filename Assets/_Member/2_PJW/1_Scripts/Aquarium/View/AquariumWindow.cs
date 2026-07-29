using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 아쿠아리움 탱크 창. <see cref="AquariumTankRenderer"/>가 상시 렌더하는 탱크 RT를
    /// RawImage에 바인딩해 표시하는 컨슈머다. 창을 닫아도 생산과 헤엄은 계속된다.
    /// 표시 전용이라 ViewModel을 갖지 않는다.
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
