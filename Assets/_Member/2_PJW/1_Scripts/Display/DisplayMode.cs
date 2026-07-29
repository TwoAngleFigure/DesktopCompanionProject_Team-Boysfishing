using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>화면 모드. Full은 모니터 전체, Window는 크롭·이동 가능한 영역이다.</summary>
    public enum ScreenMode { Full, Window }

    /// <summary>
    /// Window 모드 출력 배율의 허용 범위와 클램프. 카메라가 보는 영역은 유지하고 출력 크기만 확대·축소한다.
    /// </summary>
    public static class ViewScaleRange
    {
        public const float Min = 0.5f;
        public const float Max = 1.5f;
        public const float Default = 1f;

        public static float Clamp(float value) => Mathf.Clamp(value, Min, Max);
    }
}
