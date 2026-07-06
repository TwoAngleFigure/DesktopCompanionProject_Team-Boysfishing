using System;

namespace DesktopCompanion.Save
{
    /// <summary>
    /// 영구 상태를 가진 요소(주로 System)가 구현하는 저장 계약.
    /// 지금은 계약만 존재하며, 추후 SaveManager가 등록된 ISaveable을 순회해
    /// <see cref="SaveId"/> 키로 상태를 JSON 저장/복원한다.
    /// 이 계약을 처음부터 채워두면 Save/Load 도입 시 각 System의 기존 로직을
    /// 건드리지 않는다(additive).
    ///
    /// 규칙:
    /// - <see cref="CaptureState"/>: [Serializable] POCO(DTO)를 반환. **정의(Data)는 넣지 말고
    ///   dataId 참조 + 가변 상태만** 담는다(용량·버전 안전).
    /// - <see cref="RestoreState"/>: state를 <see cref="StateType"/>으로 캐스팅해 복원.
    /// - <see cref="StateType"/>: RestoreState에 넘어올 DTO의 구체 타입(JSON 역직렬화용).
    /// - <see cref="SaveId"/>: 세이브 내에서 이 요소를 구분하는 고유 키(예: "inventory").
    /// </summary>
    public interface ISaveable
    {
        string SaveId { get; }
        Type StateType { get; }
        object CaptureState();
        void RestoreState(object state);
    }
}
