using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 도움말 영구 상태 DTO. 해금 순서가 목록 표시 순서이자 알림 우선순위이므로 List로 저장한다.
    /// 항목은 StringEnumConverter로 이름이 기록되어 enum 값 재배치에 영향받지 않는다.
    /// </summary>
    [Serializable]
    public class TutorialSave
    {
        public List<HelpTopic> unlockedTopics = new();
        public List<HelpTopic> readTopics = new();
    }
}
