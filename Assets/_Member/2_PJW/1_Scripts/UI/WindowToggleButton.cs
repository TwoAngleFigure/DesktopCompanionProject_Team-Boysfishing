using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 버튼으로 대상 창을 토글한다: 열려 있으면 Hide, 닫혀 있으면 Show.
    /// 이미 열린 창의 버튼을 다시 눌러도 재-Show(스택 재삽입→재정렬)가 일어나지 않아
    /// 창 순서가 흐트러지지 않는다. HideMode(SetActive/CanvasGroup) 무관하게 IsShown으로 판정.
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
