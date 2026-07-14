using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>표시 설정 값(모드·스케일·모니터·창 위치). 창 위치는 정규화(0~1) rect 좌하단 기준.</summary>
    public struct DisplaySettingsData
    {
        public ScreenMode Mode;
        public ViewScale Scale;
        public int MonitorIndex;
        public Vector2 WindowRectPos; // 정규화 rect 위치(x,y)
    }

    /// <summary>
    /// 표시 설정을 PlayerPrefs에 저장/로드한다(머신별 사용자 설정 — 게임 세이브와 무관).
    /// </summary>
    public static class DisplaySettings
    {
        private const string K_Mode = "display.mode";
        private const string K_Scale = "display.scale";
        private const string K_Monitor = "display.monitor";
        private const string K_PosX = "display.window.posX";
        private const string K_PosY = "display.window.posY";

        public static DisplaySettingsData Load() => new DisplaySettingsData
        {
            Mode = (ScreenMode)PlayerPrefs.GetInt(K_Mode, (int)ScreenMode.Full),
            Scale = (ViewScale)PlayerPrefs.GetInt(K_Scale, (int)ViewScale.X1),
            MonitorIndex = PlayerPrefs.GetInt(K_Monitor, 0),
            // 기본 위치: 우하단(보트 쪽)
            WindowRectPos = new Vector2(
                PlayerPrefs.GetFloat(K_PosX, 2f / 3f),
                PlayerPrefs.GetFloat(K_PosY, 0f)),
        };

        public static void Save(DisplaySettingsData data)
        {
            PlayerPrefs.SetInt(K_Mode, (int)data.Mode);
            PlayerPrefs.SetInt(K_Scale, (int)data.Scale);
            PlayerPrefs.SetInt(K_Monitor, data.MonitorIndex);
            PlayerPrefs.SetFloat(K_PosX, data.WindowRectPos.x);
            PlayerPrefs.SetFloat(K_PosY, data.WindowRectPos.y);
            PlayerPrefs.Save();
        }
    }
}
