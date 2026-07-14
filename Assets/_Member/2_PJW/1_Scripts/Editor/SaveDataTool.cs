using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DesktopCompanion.Core;
using DesktopCompanion.Save;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// 세이브 파일 관리 툴(Tools 메뉴). 테스트 데이터 정리·수동 저장용.
    /// 파일 경로는 SaveManager 기본값(Application.persistentDataPath/save.json)과 동일.
    /// </summary>
    public static class SaveDataTool
    {
        private const string FileName = "save.json";
        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        // ── 삭제 ──
        [MenuItem("Tools/DesktopCompanion/세이브 삭제", priority = 200)]
        public static void DeleteSave()
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("세이브 삭제", $"세이브 파일이 없습니다.\n{path}", "확인");
                return;
            }

            string warn = EditorApplication.isPlaying
                ? "\n\n⚠️ 플레이 중입니다 — 종료 시 자동 저장이 다시 생성합니다. 정지 후 삭제를 권장합니다."
                : "";
            if (!EditorUtility.DisplayDialog("세이브 삭제",
                    $"이 세이브를 삭제할까요? (되돌릴 수 없음)\n{path}{warn}", "삭제", "취소"))
            {
                return;
            }

            try
            {
                File.Delete(path);
                Debug.Log($"[SaveDataTool] 세이브 삭제됨: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveDataTool] 삭제 실패: {e.Message}");
            }
        }

        // ── 저장(플레이 중, 실행 상태를 즉시 기록) ──
        [MenuItem("Tools/DesktopCompanion/세이브 즉시 저장 (Play 중)", priority = 201)]
        public static void SaveNow()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("세이브 저장",
                    "Play 모드에서만 저장할 수 있습니다(실행 중인 시스템 상태를 기록).", "확인");
                return;
            }

            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm == null)
            {
                Debug.LogError("[SaveDataTool] GameManager를 찾을 수 없습니다.");
                return;
            }

            // GameManager.m_saveManager(비공개)를 리플렉션으로 얻어 Save() 호출(TestDebug와 동일 패턴).
            var field = typeof(GameManager).GetField("m_saveManager", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field?.GetValue(gm) is SaveManager sm)
            {
                sm.Save();
                Debug.Log($"[SaveDataTool] 즉시 저장 완료: {SavePath}");
            }
            else
            {
                Debug.LogError("[SaveDataTool] SaveManager 접근 실패(부팅 전이거나 필드명이 바뀜).");
            }
        }

        // ── 폴더 열기 ──
        [MenuItem("Tools/DesktopCompanion/세이브 폴더 열기", priority = 202)]
        public static void OpenSaveFolder()
        {
            string dir = Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(File.Exists(SavePath) ? SavePath : dir);
        }

        // 세이브가 있을 때만 삭제/저장 메뉴 활성화 처리는 하지 않음(경로 안내를 위해 항상 활성).
    }
}
