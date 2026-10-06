using UnityEngine;
using DesktopCompanion.Rendering;

namespace DesktopCompanion.Views
{
    /// <summary>화면 모드. Full은 모니터 전체, Window는 크롭·이동 가능한 영역이다.</summary>
    public enum ScreenMode { Full, Window }

    /// <summary>
    /// 프레임 상한 선택지. 흔한 모니터 주사율에 절전용 저프레임을 더한 목록이다.
    /// 값을 늘리려면 Options 배열에만 추가하면 된다(라벨·인덱스는 파생된다).
    /// </summary>
    public static class FpsOptions
    {
        /// <summary>FrameRateController의 인스펙터 기본값과 같은 값이다.</summary>
        public const int Default = 30;

        public static readonly int[] Options = { 15, 30, 45, 60, 75, 90, 120, 144, 165, 240 };

        public static string[] Labels()
        {
            var labels = new string[Options.Length];
            for (int i = 0; i < Options.Length; i++)
            {
                labels[i] = $"{Options[i]} FPS";
            }
            return labels;
        }

        /// <summary>값에 가장 가까운 항목의 인덱스. 목록에 없는 옛 저장값도 안전하게 매핑된다.</summary>
        public static int NearestIndex(int value)
        {
            int best = 0;
            int bestDelta = int.MaxValue;
            for (int i = 0; i < Options.Length; i++)
            {
                int delta = Mathf.Abs(Options[i] - value);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>목록에 있는 값으로 맞춘다.</summary>
        public static int Snap(int value) => Options[NearestIndex(value)];
    }

    /// <summary>
    /// 배율 목록을 드롭다운에 태우기 위한 공통 변환. ViewScaleRange·UiScaleRange가 함께 쓴다.
    /// </summary>
    public static class ScaleOptions
    {
        /// <summary>[min, max]를 step 간격으로 훑은 배율 목록. 오름차순이다.</summary>
        public static float[] Build(float min, float max, float step)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt((max - min) / step) + 1);
            var options = new float[count];
            for (int i = 0; i < count; i++)
            {
                options[i] = min + step * i;
            }
            return options;
        }

        /// <summary>"0.75배" 형태의 표시 라벨.</summary>
        public static string[] Labels(float[] options)
        {
            var labels = new string[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                labels[i] = $"{options[i]:0.##}배";
            }
            return labels;
        }

        /// <summary>값에 가장 가까운 항목의 인덱스. 목록에 없는 옛 저장값도 안전하게 매핑된다.</summary>
        public static int NearestIndex(float[] options, float value)
        {
            int best = 0;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < options.Length; i++)
            {
                float delta = Mathf.Abs(options[i] - value);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = i;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// Window 모드 출력 배율의 허용 범위와 선택지. 카메라가 보는 영역은 유지하고 출력 크기만 확대·축소한다.
    /// </summary>
    public static class ViewScaleRange
    {
        public const float Min = 0.5f;
        public const float Max = 1.5f;
        public const float Step = 0.25f;
        public const float Default = 1f;

        /// <summary>드롭다운에 노출하는 배율 목록.</summary>
        public static readonly float[] Options = ScaleOptions.Build(Min, Max, Step);

        public static float Clamp(float value) => Mathf.Clamp(value, Min, Max);

        public static string[] Labels() => ScaleOptions.Labels(Options);

        public static int NearestIndex(float value) => ScaleOptions.NearestIndex(Options, value);
    }

    /// <summary>
    /// UI 배율(Constant Pixel Size 캔버스의 scaleFactor)의 허용 범위와 스냅 규칙.
    /// 아트 1픽셀이 PixelGridDesign.UnitsPerDot(=4)유닛이므로 배율은 1/4 단위로만 움직여야
    /// 아트 1픽셀이 4·3·2 화면픽셀처럼 정수로 떨어져 픽셀 격자가 흐트러지지 않는다.
    /// </summary>
    public static class UiScaleRange
    {
        /// <summary>아트 1픽셀이 화면픽셀 1칸만큼 변하는 최소 단위.</summary>
        public const float Step = 1f / PixelGridDesign.UnitsPerDot;
        public const float Min = 0.5f;
        // 고해상도 모니터에서는 배율 1(아트 1픽셀 = 4화면픽셀)이 월드 도트보다 작아 보인다.
        // 예: 3840×2160의 월드 도트는 6화면픽셀이므로 1.5배가 있어야 눈높이가 맞는다.
        public const float Max = 1.5f;
        public const float Default = 1f;

        /// <summary>드롭다운에 노출하는 배율 목록. Snap이 만들어 낼 수 있는 값 전체와 일치한다.</summary>
        public static readonly float[] Options = ScaleOptions.Build(Min, Max, Step);

        /// <summary>Step 단위로 반올림한 뒤 허용 범위로 자른다.</summary>
        public static float Snap(float value)
            => Mathf.Clamp(Mathf.Round(value / Step) * Step, Min, Max);

        public static string[] Labels() => ScaleOptions.Labels(Options);

        public static int NearestIndex(float value) => ScaleOptions.NearestIndex(Options, value);
    }
}
