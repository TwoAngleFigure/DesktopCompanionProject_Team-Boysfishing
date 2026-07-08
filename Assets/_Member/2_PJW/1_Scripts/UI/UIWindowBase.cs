using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 윈도우형 UI(인벤토리 등)만 상속하는 베이스. 일반 UI(HUD 등)는 UIViewBase를 그대로 쓴다.
    /// 활성(=GameObject on/off)에 연동해 UIManager의 활성 윈도우 스택에 참여한다.
    /// 우클릭 닫기(UIManager.CloseTopWindow)는 이 스택의 top(가장 최근 열린 창)을 닫는다.
    /// </summary>
    public abstract class UIWindowBase : UIViewBase
    {
        protected override void OnEnable()
        {
            base.OnEnable();                    // UIViewBase: 자가 등록
            UIManager.PushActiveWindow(this);   // 최근 열림 = 스택 top
        }

        protected override void OnDisable()
        {
            UIManager.RemoveActiveWindow(this); // 스택에서 제거
            base.OnDisable();                   // UIViewBase: Unbind + Unregister
        }

        /// <summary>이 윈도우를 닫는다(비활성화 → OnDisable에서 스택 제거).</summary>
        public void Close() => gameObject.SetActive(false);
    }
}
