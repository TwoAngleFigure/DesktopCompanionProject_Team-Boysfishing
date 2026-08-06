using System;
using System.Collections.Generic;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 튜토리얼 영구 상태 DTO. 이미 표시한 스텝과 종료 여부만 담는다.
    /// 스텝은 StringEnumConverter로 이름이 기록되어 enum 값 재배치에 영향받지 않는다.
    /// </summary>
    [Serializable]
    public class TutorialSave
    {
        public bool completed;
        public List<TutorialStep> shownSteps = new();
    }
}
