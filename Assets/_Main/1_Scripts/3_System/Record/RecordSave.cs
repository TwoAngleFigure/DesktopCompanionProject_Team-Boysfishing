using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 기록 영구 상태 DTO. 집계 키별 누적값만 담는다.
    /// 출처는 문자열로 저장한다 — enum으로 두면 출처 이름이 바뀌거나 삭제됐을 때 역직렬화 예외로 기록 전체 복원이 실패한다.
    /// </summary>
    [Serializable]
    public class RecordSave
    {
        public List<Entry> entries = new();

        [Serializable]
        public class Entry
        {
            public string source;
            public int targetId;
            public long value;
        }
    }
}
