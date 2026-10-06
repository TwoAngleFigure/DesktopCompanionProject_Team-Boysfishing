namespace DesktopCompanion.Views
{
    /// <summary>
    /// 자체 로직이 없는 순수 표시용 창. ViewModel도 위젯 바인딩도 없다.
    ///
    /// UIWindowBase는 abstract라 컴포넌트로 직접 붙일 수 없다. 일반 UI 패널을 창처럼
    /// Show()/Hide()로 제어하고 싶을 때(예: <see cref="UITabWindow"/>의 탭 패널) 이것을 붙인다.
    ///
    /// 다른 창 안에 들어가는 패널이라면 인스펙터에서 두 가지를 반드시 꺼야 한다:
    ///  - Participate In Layout — 켜두면 자동 배치가 앵커·피벗·위치를 덮어써 화면 우측으로 끌려간다.
    ///  - Closable By Shortcut  — 켜두면 우클릭·ESC가 바깥 창 대신 이 패널을 먼저 닫는다.
    /// </summary>
    public sealed class SimpleWindow : UIWindowBase
    {
        public override void Bind() { }

        public override void Unbind() { }
    }
}
