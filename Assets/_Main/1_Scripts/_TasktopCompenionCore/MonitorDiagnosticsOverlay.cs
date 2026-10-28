// 진단 전용 도구. 릴리스 빌드에는 포함되지 않는다(개발 빌드/에디터에서만 컴파일).
#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using System.Text;
using UnityEngine;

namespace DesktopCompanion
{
    /// <summary>
    /// 다중 모니터 진단 오버레이(개발 빌드 전용).
    ///
    /// 모니터 열거·창 배치·DPI 관련 문제를 빌드 현장에서 바로 판별하기 위한 도구다.
    /// 릴리스 빌드에서는 파일 전체가 컴파일되지 않으므로 F9~F12도 노출되지 않는다.
    ///
    /// 표시 항목:
    ///  - OS가 보는 모니터 수(SM_CMONITORS)와 실제 열거 결과의 대조
    ///  - 모니터별 좌표·해상도·DPI 배율
    ///  - 창이 올라온 모니터와 크기 일치 여부(테두리 잔존 / 크기 경합 자동 판정)
    ///  - 요청한 창 rect vs 실제 rect(DPI 자동 재조정 검출)
    ///
    /// 조작(클릭관통 창에서도 동작하도록 GetAsyncKeyState 전역 키 입력 사용):
    ///  - F9  : 오버레이 토글
    ///  - F10 : 재측정
    ///  - F11 : 결과를 실행파일 옆 MonitorDiagnostics.txt 로 저장
    ///  - F12 : 창 스타일·DWM 수동 재적용(대조용)
    ///
    /// 씬/프리팹 배치 없이 RuntimeInitializeOnLoadMethod로 자동 생성된다.
    /// </summary>
    public class MonitorDiagnosticsOverlay : MonoBehaviour
    {
        private const int VK_F9 = 0x78;
        private const int VK_F10 = 0x79;
        private const int VK_F11 = 0x7A;
        private const int VK_F12 = 0x7B;

        private bool m_visible = true;
        private string m_report = "(측정 전)";
        private string m_savedPath;
        private Vector2 m_scroll;

        private bool m_prevF9, m_prevF10, m_prevF11, m_prevF12;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[MonitorDiagnosticsOverlay]");
            go.AddComponent<MonitorDiagnosticsOverlay>();
            DontDestroyOnLoad(go);
        }

        private void Start()
        {
            Measure();
            Debug.Log(m_report);
        }

        private void Update()
        {
            // 클릭관통 창은 포커스/마우스 메시지를 못 받을 수 있어 전역 키 상태를 직접 읽는다.
            if (EdgeDown(VK_F9, ref m_prevF9)) m_visible = !m_visible;
            if (EdgeDown(VK_F10, ref m_prevF10)) { Measure(); Debug.Log(m_report); }
            if (EdgeDown(VK_F11, ref m_prevF11)) SaveToFile();

            // [대조용] 스타일·DWM을 수동 재적용한다.
            // 수정이 옳다면 이제 F12 없이도 정상이어야 한다. F12로만 고쳐지면 순서가 여전히 틀린 것.
            if (EdgeDown(VK_F12, ref m_prevF12))
            {
                var win = FindAnyObjectByType<TransparentWindow>();
                if (win != null)
                {
                    win.ReapplyWindowStyles();
                    Debug.Log("[MonitorDiagnostics] 창 스타일 재적용 실행");
                }
                Measure();
            }
        }

        private static bool EdgeDown(int vKey, ref bool prev)
        {
            bool now = (Win32Native.GetAsyncKeyState(vKey) & 0x8000) != 0;
            bool down = now && prev == false;
            prev = now;
            return down;
        }

        // ── 측정 ──

        private void Measure()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 모니터 열거 진단 ===");
            sb.AppendLine($"시각        : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"실행 환경   : {(Application.isEditor ? "Editor(Mono)" : "Build")}  Unity {Application.unityVersion}");
            sb.AppendLine($"플랫폼      : {Application.platform}  /  64bit={IntPtr.Size == 8}");
            sb.AppendLine();

            // Win32 계층이 보는 값(마샬링과 무관 — 대조 기준선)
            int smMonitors = Win32Native.GetSystemMetrics(Win32Native.SM_CMONITORS);
            sb.AppendLine("[기준선] 마샬링과 무관한 Win32 직접 조회");
            sb.AppendLine($"  SM_CMONITORS      : {smMonitors}   ← OS가 보는 실제 모니터 수");
            sb.AppendLine($"  주 모니터 크기    : {Win32Native.GetSystemMetrics(Win32Native.SM_CXSCREEN)}x{Win32Native.GetSystemMetrics(Win32Native.SM_CYSCREEN)}");
            sb.AppendLine($"  Unity Display.displays : {Display.displays.Length}");
            sb.AppendLine();

            // 수정된 열거 경로(static + MonoPInvokeCallback)
            var monitors = Win32Native.GetMonitors();
            sb.AppendLine("[열거 결과] GetMonitors() — 디스플레이 번호 순 정렬");
            sb.AppendLine($"  monitors={monitors.Count}");
            for (int i = 0; i < monitors.Count; i++)
            {
                var m = monitors[i];
                sb.AppendLine($"    index[{i}] → Monitor {m.displayNumber}  {m.deviceName}{(m.isPrimary ? "  [주]" : "")}");
                sb.AppendLine($"              ({m.rect.left},{m.rect.top})-({m.rect.right},{m.rect.bottom})  {m.Width}x{m.Height}");

                // 모니터마다 배율이 다르면 Win32 좌표와 Unity 픽셀이 어긋난다.
                uint dpi = Win32Native.GetMonitorDpi(m.rect.left + m.Width / 2, m.rect.top + m.Height / 2);
                sb.AppendLine($"              DPI={dpi} ({(dpi > 0 ? dpi / 96f : 0f):0.##}배)");
            }
            sb.AppendLine();

            sb.AppendLine("[현재 창]");
            sb.AppendLine($"  Screen  : {Screen.width}x{Screen.height}  fullScreen={Screen.fullScreen}  mode={Screen.fullScreenMode}");

            // 창이 어느 모니터 위에 있는지 좌표로 판별하고, 크기가 그 모니터와 맞는지 대조한다.
            // 작으면 테두리가 남은 것, 크면 SetResolution/SetWindowPos 경합으로 과적용된 것.
            var host = FindHostMonitor(monitors);
            if (host.HasValue)
            {
                var m = host.Value;
                int dw = Screen.width - m.Width;
                int dh = Screen.height - m.Height;
                sb.AppendLine($"  올라온 모니터 : Monitor {m.displayNumber}  {m.Width}x{m.Height}");
                sb.AppendLine($"  크기 차이     : {dw:+#;-#;0} x {dh:+#;-#;0}  {SizeDiagnosis(dw, dh)}");
            }
            else
            {
                sb.AppendLine("  올라온 모니터 : 판별 실패(창 좌표 조회 불가)");
            }

            // 스타일 비트는 정상인데도 테두리가 남을 수 있다(SWP_FRAMECHANGED 누락).
            // 따라서 비트값보다 아래 "크기 차이"가 실제 판정 기준이다.
            var win = FindAnyObjectByType<TransparentWindow>();
            sb.AppendLine(win != null ? win.DumpWindowState() : "  TransparentWindow 없음");
            sb.AppendLine();

            sb.AppendLine("[판정] " + Verdict(smMonitors, monitors.Count));

            m_report = sb.ToString();
        }

        // 창 좌상단이 속한 모니터를 찾는다(가상 데스크톱 좌표 기준).
        private static Win32Native.MonitorInfo? FindHostMonitor(System.Collections.Generic.List<Win32Native.MonitorInfo> monitors)
        {
            var win = FindAnyObjectByType<TransparentWindow>();
            if (win == null || win.IsReady == false) return null;

            var pt = new Win32Native.POINT { x = 0, y = 0 };
            if (Win32Native.ClientToScreen(win.Hwnd, ref pt) == false) return null;

            for (int i = 0; i < monitors.Count; i++)
            {
                var r = monitors[i].rect;
                if (pt.x >= r.left && pt.x < r.right && pt.y >= r.top && pt.y < r.bottom)
                {
                    return monitors[i];
                }
            }
            return null;
        }

        private static string SizeDiagnosis(int dw, int dh)
        {
            // _edgeInset(1px) 축소는 정상이므로 소폭 음수는 허용한다.
            if (dw <= 0 && dw >= -2 && dh <= 0 && dh >= -2) return "← 정상";
            if (dw < -2 || dh < -2) return "← 테두리가 남아 클라이언트가 작음";
            return "← 과적용(창 크기/클라이언트 크기 경합)";
        }

        private static string Verdict(int smMonitors, int enumerated)
        {
            if (smMonitors <= 1)
            {
                return $"OS가 모니터를 {smMonitors}개로 보고 — 단일 모니터 환경이거나 OS 단계 문제.";
            }
            if (enumerated == smMonitors)
            {
                return $"★ 정상. 열거 {enumerated}개 = OS {smMonitors}개 — IL2CPP 역방향 P/Invoke 정상 동작.";
            }
            return $"불일치: 열거={enumerated}, OS={smMonitors} — 열거 실패 또는 폴백 동작 중. 재분석 필요.";
        }

        // ── 저장 ──

        private void SaveToFile()
        {
            try
            {
                // dataPath의 부모 = 빌드는 실행파일 폴더, 에디터는 프로젝트 루트.
                string dir = System.IO.Directory.GetParent(Application.dataPath).FullName;
                m_savedPath = System.IO.Path.Combine(dir, "MonitorDiagnostics.txt");
                System.IO.File.WriteAllText(m_savedPath, m_report, Encoding.UTF8);
                Debug.Log($"[MonitorDiagnostics] 저장: {m_savedPath}");
            }
            catch (Exception e)
            {
                m_savedPath = $"저장 실패: {e.Message}";
                Debug.LogError($"[MonitorDiagnostics] 저장 실패: {e}");
            }
        }

        // ── 표시 ──

        private void OnGUI()
        {
            if (m_visible == false)
            {
                GUI.Label(new Rect(10, 10, 600, 20), "[F9] 모니터 진단 오버레이 열기");
                return;
            }

            const float w = 720f;
            float h = Mathf.Min(Screen.height - 20f, 520f);
            GUILayout.BeginArea(new Rect(10, 10, w, h), GUI.skin.box);

            GUILayout.Label("모니터 진단  —  F9 닫기 / F10 재측정 / F11 저장 / F12 창 스타일 재적용");
            m_scroll = GUILayout.BeginScrollView(m_scroll);

            // 폭이 좁으면 줄바꿈으로 읽기 어려워 워드랩을 끈다.
            var style = new GUIStyle(GUI.skin.label) { wordWrap = false, richText = false };
            GUILayout.Label(m_report, style);

            GUILayout.EndScrollView();

            if (string.IsNullOrEmpty(m_savedPath) == false)
            {
                GUILayout.Label($"저장 경로: {m_savedPath}");
            }

            GUILayout.EndArea();
        }
    }
}

#endif
