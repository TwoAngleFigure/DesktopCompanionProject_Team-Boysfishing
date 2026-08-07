namespace DesktopCompanion.Rendering
{
    /// <summary>
    /// 픽셀 격자와 시야의 설계 상수. 표시 계층(DisplayModeController)과
    /// 픽셀라이저(BFPixelizerFeature)가 같은 기준을 쓰도록 여기서만 정의한다.
    /// 기준 도트 해상도 640×360은 720·1080·1440·2160에서 블록이 정수 배율로 떨어지는 값이다.
    /// </summary>
    public static class PixelGridDesign
    {
        /// <summary>기준 화면 해상도. 아래 값들이 이 비율·크기를 기준으로 정의된다.</summary>
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;

        /// <summary>기준 해상도에서의 카메라 orthographicSize.</summary>
        public const float ReferenceOrthographicSize = 10f;

        /// <summary>기준 프레임의 가상 도트 해상도.</summary>
        public const int BaseDotsWide = 640;
        public const int BaseDotsHigh = 360;

        /// <summary>기준 화면 종횡비. 렌더 밴드 종횡비의 하한이다. = 1.7778</summary>
        public static float ReferenceAspect => (float)ReferenceWidth / ReferenceHeight;

        /// <summary>모든 환경에서 보장되는 최소 가로 시야(월드 유닛). = 35.556</summary>
        public static float WorldWidth =>
            2f * ReferenceOrthographicSize * ReferenceWidth / ReferenceHeight;

        /// <summary>월드 1유닛당 도트 수. = 18</summary>
        public static float BlocksPerUnit =>
            BaseDotsHigh / (2f * ReferenceOrthographicSize);

        // ── UI 기준 ──
        //
        // UI 캔버스는 Constant Pixel Size를 쓴다. 아트 1픽셀 = UnitsPerDot 캔버스 유닛이고,
        // 캔버스 1유닛 = scaleFactor 화면픽셀이므로 아트 1픽셀은 항상 UnitsPerDot × scaleFactor 화면픽셀이다.
        // scaleFactor를 1/UnitsPerDot 단위로만 움직이면(UiScaleRange) 해상도와 무관하게 정수 크기가 유지된다.
        //
        // 참고: 기준 해상도 1920×1080(월드 도트 3px)에서 배율 1(UI 4px)이면 UI가 월드보다 크게 보인다.
        //       월드와 눈높이를 맞추려면 사용자가 배율을 0.75로 내리면 된다.

        /// <summary>아트 1픽셀에 대응하는 UI 캔버스 유닛 수.</summary>
        public const int UnitsPerDot = 4;

        /// <summary>
        /// UI 캔버스의 Reference Pixels Per Unit.
        /// 스프라이트 임포트 PPU를 이 값 / UnitsPerDot(= 25)로 맞추면 Image의 Pixels Per Unit Multiplier가
        /// 1로 떨어지고, Set Native Size가 그대로 UnitsPerDot 배 크기를 만든다.
        /// </summary>
        public const float UiReferencePixelsPerUnit = 100f;

        // 정합 조건: BaseDotsWide == WorldWidth × BlocksPerUnit  (640 == 35.556 × 18)
        // 위 네 상수를 바꿀 때는 이 등식이 유지되는지 확인할 것.
    }
}
