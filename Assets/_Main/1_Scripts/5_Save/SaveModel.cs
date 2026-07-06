using System.Collections.Generic;

namespace DesktopCompanion.Save
{
    /// <summary>
    /// 세이브 파일의 루트 구조(§15.3). 유저별 상태만 담는다(정의/콘텐츠는 절대 넣지 않음 — D13).
    /// entries: ISaveable.SaveId → 해당 요소 상태의 직렬화 문자열.
    /// </summary>
    public class SaveModel
    {
        public int schemaVersion = SaveManager.SchemaVersion;   // 마이그레이션 대비
        public Dictionary<string, string> entries = new();
    }
}
