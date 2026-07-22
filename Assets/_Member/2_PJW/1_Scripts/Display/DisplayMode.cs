using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>화면 모드: 전체(모니터 전체) / 창(보트 크롭 이동 영역).</summary>
    public enum ScreenMode { Full, Window }

    /// <summary>
    /// 출력 스케일(Window 모드 창 크기 배율)의 허용 범위. 카메라가 보는 영역은 유지, 출력만 확대·축소.
    ///
    /// ※ 비정수 배율에서는 픽셀 블록이 반픽셀 경계에 걸려 가장자리가 뭉개질 수 있다(RT 필터가 Bilinear).
    ///   픽셀 정합을 위한 배율 스냅은 보류 상태다 — 필요해지면 여기서 단계값을 강제한다.
    /// </summary>
    public static class ViewScaleRange
    {
        public const float Min = 0.5f;
        public const float Max = 1.5f;
        public const float Default = 1f;

        public static float Clamp(float value) => Mathf.Clamp(value, Min, Max);
    }
}
