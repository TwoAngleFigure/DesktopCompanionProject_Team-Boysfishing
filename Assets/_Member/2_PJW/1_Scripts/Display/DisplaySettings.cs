using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>표시 설정 값(모드·스케일·모니터·창 위치·크롭 범위). 창 위치는 좌하단 기준 정규화(0~1) 좌표다.</summary>
    public struct DisplaySettingsData
    {
        public ScreenMode Mode;
        public float Scale;          // Window 모드 출력 배율(ViewScaleRange)
        public int MonitorIndex;
        public Vector2 WindowRectPos; // 정규화 rect 위치(x,y)

        // Window 모드에서 월드 렌더의 어느 가로 구간을 보여줄지(0~1). 폭은 Right-Left로 파생된다.
        public float CropLeft;
        public float CropRight;
    }

    /// <summary>
    /// 표시 설정을 PlayerPrefs에 저장·로드한다. 게임 세이브와는 무관한 머신별 설정이다.
    /// </summary>
    public static class DisplaySettings
    {
        private const string K_Mode = "display.mode";
        // 배율이 열거형(int)에서 연속값(float)으로 바뀌어 키를 분리했다.
        // 예전 "display.scale"(int)은 더 이상 읽지 않는다.
        private const string K_Scale = "display.scaleFactor";
        private const string K_Monitor = "display.monitor";
        private const string K_PosX = "display.window.posX";
        private const string K_PosY = "display.window.posY";
        private const string K_CropLeft = "display.crop.left";
        private const string K_CropRight = "display.crop.right";

        // 기본 크롭: 우측 1/3(보트 쪽) — 크롭 범위 도입 전과 동일한 화면을 보여준다.
        private const float DefaultCropLeft = 2f / 3f;
        private const float DefaultCropRight = 1f;

        public static DisplaySettingsData Load() => new DisplaySettingsData
        {
            Mode = ReadEnum(K_Mode, ScreenMode.Full),
            Scale = ViewScaleRange.Clamp(PlayerPrefs.GetFloat(K_Scale, ViewScaleRange.Default)),
            MonitorIndex = PlayerPrefs.GetInt(K_Monitor, 0),
            // 기본 위치: 우하단(보트 쪽)
            WindowRectPos = new Vector2(
                PlayerPrefs.GetFloat(K_PosX, 2f / 3f),
                PlayerPrefs.GetFloat(K_PosY, 0f)),
            CropLeft = PlayerPrefs.GetFloat(K_CropLeft, DefaultCropLeft),
            CropRight = PlayerPrefs.GetFloat(K_CropRight, DefaultCropRight),
        };

        public static void Save(DisplaySettingsData data)
        {
            PlayerPrefs.SetInt(K_Mode, (int)data.Mode);
            PlayerPrefs.SetFloat(K_Scale, data.Scale);
            PlayerPrefs.SetInt(K_Monitor, data.MonitorIndex);
            PlayerPrefs.SetFloat(K_PosX, data.WindowRectPos.x);
            PlayerPrefs.SetFloat(K_PosY, data.WindowRectPos.y);
            PlayerPrefs.SetFloat(K_CropLeft, data.CropLeft);
            PlayerPrefs.SetFloat(K_CropRight, data.CropRight);
            PlayerPrefs.Save();
        }

        /// <summary>저장된 정수를 열거형으로 읽는다. 정의에 없는 값이면 기본값으로 폴백한다.</summary>
        private static T ReadEnum<T>(string key, T fallback) where T : struct, System.Enum
        {
            int raw = PlayerPrefs.GetInt(key, (int)(object)fallback);
            foreach (T defined in System.Enum.GetValues(typeof(T)))
            {
                if ((int)(object)defined == raw)
                {
                    return defined;
                }
            }
            return fallback;
        }
    }
}
