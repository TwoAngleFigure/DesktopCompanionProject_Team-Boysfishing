using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 버튼 클릭으로 대상 창을 토글한다. 열려 있으면 Hide, 닫혀 있으면 Show를 호출한다.
    /// 열림 여부는 HideMode와 무관하게 <see cref="UIWindowBase.IsShown"/>으로 판정한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class WindowToggleButton : MonoBehaviour
    {
        [Tooltip("토글할 대상 창(비활성 상태여도 참조 가능)")]
        [SerializeField] private UIWindowBase m_window;

        private Button m_button;

        private void Awake()
        {
            m_button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (m_button != null) m_button.onClick.AddListener(Toggle);
        }

        private void OnDisable()
        {
            if (m_button != null) m_button.onClick.RemoveListener(Toggle);
        }

        public void Toggle()
        {
            if (m_window == null)
            {
                Debug.LogWarning("[WindowToggleButton] 대상 창 미할당", this);
                return;
            }

            if (m_window.IsShown) m_window.Hide();   // 열려 있으면 닫기(재정렬 안 함)
            else m_window.Show();                    // 닫혀 있으면 열기(최신=좌측)
        }
    }
}
