namespace DesktopCompanion.Rendering
{
    /// <summary>
    /// 픽셀 격자·시야의 설계 상수(계획 23·25).
    /// 표시 계층(DisplayModeController)과 픽셀라이저(BFPixelizerFeature)가
    /// 같은 기준을 쓰도록 여기서만 정의한다.
    ///
    /// 640×360을 기준 도트 해상도로 잡은 이유: 360이 720·1080·1440·2160의 공약수라
    /// 주요 세로 해상도 전부에서 블록이 정수 배율(×2·×3·×4·×6)로 떨어진다.
    /// 960×540은 1440p에서, 480×270은 720p·1440p에서 정수가 깨진다(계획 22 분석).
    ///
    /// ※ 인스펙터 노출 대신 상수로 둔 이유: WorldWidth와 BlocksPerUnit은 서로 유도 관계라
    ///   두 곳에 나눠 적으면 어긋난다. 한 파일만 고치면 양쪽이 자동으로 일관된다.
    /// </summary>
    public static class PixelGridDesign
    {
        /// <summary>기준 화면 해상도. 아래 값들이 이 비율·크기에서 정의된다.</summary>
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;

        /// <summary>기준 해상도에서의 카메라 orthographicSize.</summary>
        public const float ReferenceOrthographicSize = 10f;

        /// <summary>기준 프레임의 가상 도트 해상도.</summary>
        public const int BaseDotsWide = 640;
        public const int BaseDotsHigh = 360;

        /// <summary>기준 화면 종횡비. 렌더 밴드의 하한이 된다(계획 25). = 1.7778</summary>
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
