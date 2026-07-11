namespace DesktopCompanion.Views
{
    /// <summary>화면 모드: 전체(모니터 전체) / 창(보트 크롭 이동 영역).</summary>
    public enum ScreenMode { Full, Window }

    /// <summary>출력 스케일(창 크기 배율). 카메라가 보는 영역은 유지, 출력만 확대.</summary>
    public enum ViewScale { X1, X1_5, X2 }

    public static class ViewScaleExtensions
    {
        public static float ToFactor(this ViewScale scale) => scale switch
        {
            ViewScale.X1 => 1f,
            ViewScale.X1_5 => 1.5f,
            ViewScale.X2 => 2f,
            _ => 1f,
        };
    }
}
