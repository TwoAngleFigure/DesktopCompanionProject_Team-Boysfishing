using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DesktopCompanion.Core;

namespace DesktopCompanion.Save
{
    /// <summary>
    /// 세이브 실행의 주인(§15.3, D15). 등록된 ISaveable을 순회해
    /// Save: CaptureState → JSON 파일 기록 / Load: JSON 읽기 → RestoreState.
    /// 트리거: 수동 Save() + GameManager의 OnApplicationQuit 자동.
    /// 무엇을 저장할지는 각 System의 Capture가 결정한다(선별 저장).
    /// DataManager(콘텐츠 채널)와는 무관 — 유저 상태 전용.
    /// </summary>
    public class SaveManager
    {
        public const int SchemaVersion = 1;

        private readonly List<ISaveable> m_saveables = new();
        private readonly string m_filePath;
        private readonly JsonSerializerSettings m_settings;

        public SaveManager(string filePath = null)
        {
            m_filePath = filePath ?? Path.Combine(Application.persistentDataPath, "save.json");
            m_settings = new JsonSerializerSettings
            {
                ContractResolver = new SerializeFieldContractResolver(),
                Converters = { new StringEnumConverter() },
            };
        }

        public bool HasSaveFile => File.Exists(m_filePath);

        /// <summary>영구 상태를 가진 요소 등록. GameManager(조립 루트)가 부팅 시 호출.</summary>
        public void Register(ISaveable saveable)
        {
            foreach (ISaveable existing in m_saveables)
            {
                if (existing.SaveId == saveable.SaveId)
                {
                    Debug.LogError($"[SaveManager] 중복 SaveId: {saveable.SaveId}");
                    return;
                }
            }
            m_saveables.Add(saveable);
        }

        /// <summary>등록된 ISaveable 순회 → 상태 캡처 → JSON 파일 기록.</summary>
        public void Save()
        {
            try
            {
                var model = new SaveModel();
                foreach (ISaveable saveable in m_saveables)
                {
                    object state = saveable.CaptureState();
                    if (state == null)
                    {
                        continue;
                    }
                    model.entries[saveable.SaveId] = JsonConvert.SerializeObject(state, m_settings);
                }

                string json = JsonConvert.SerializeObject(model, Formatting.Indented, m_settings);
                File.WriteAllText(m_filePath, json);
                Debug.Log($"[SaveManager] 저장 완료: {m_filePath} (entries={model.entries.Count})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 저장 실패: {e.Message}");
            }
        }

        /// <summary>세이브 파일이 있으면 각 ISaveable에 상태 복원. 없으면 false.</summary>
        public bool TryLoad()
        {
            if (!HasSaveFile)
            {
                return false;
            }

            try
            {
                var model = JsonConvert.DeserializeObject<SaveModel>(File.ReadAllText(m_filePath), m_settings);
                if (model == null)
                {
                    Debug.LogError("[SaveManager] 세이브 파일 파싱 실패");
                    return false;
                }
                if (model.schemaVersion > SchemaVersion)
                {
                    Debug.LogError($"[SaveManager] 지원하지 않는 schemaVersion={model.schemaVersion} (지원: {SchemaVersion})");
                    return false;
                }

                foreach (ISaveable saveable in m_saveables)
                {
                    if (!model.entries.TryGetValue(saveable.SaveId, out string entryJson))
                    {
                        continue;   // 신규 System 등 — 저장분 없음은 정상
                    }
                    try
                    {
                        object state = JsonConvert.DeserializeObject(entryJson, saveable.StateType, m_settings);
                        saveable.RestoreState(state);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SaveManager] '{saveable.SaveId}' 복원 실패: {e.Message}");
                    }
                }
                Debug.Log($"[SaveManager] 로드 완료: {m_filePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 로드 실패: {e.Message}");
                return false;
            }
        }
    }
}
