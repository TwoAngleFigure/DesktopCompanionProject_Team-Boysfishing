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

        // 정합 조건: BaseDotsWide == WorldWidth × BlocksPerUnit  (640 == 35.556 × 18)
        // 위 네 상수를 바꿀 때는 이 등식이 유지되는지 확인할 것.
    }
}
