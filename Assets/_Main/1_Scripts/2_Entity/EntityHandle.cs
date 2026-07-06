using System;

namespace DesktopCompanion.Entities
{
    /// <summary>
    /// Entity 인스턴스의 고유 런타임 핸들(GUID 기반).
    /// 발급은 <see cref="New"/> 한 곳에서만 하며, 실제 호출은 EntityManager가 담당한다.
    /// 문자열화(직렬화/저장)는 저장 경계에서만 수행한다.
    /// (Unity의 UnityEngine.EntityId와 이름 충돌을 피하기 위해 EntityHandle로 명명.)
    /// </summary>
    public readonly struct EntityHandle : IEquatable<EntityHandle>
    {
        public readonly Guid Value;

        public EntityHandle(Guid value) => Value = value;

        public static EntityHandle New() => new EntityHandle(Guid.NewGuid());

        public bool Equals(EntityHandle other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is EntityHandle other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N"); // 대시 없는 32자

        public static bool TryParse(string s, out EntityHandle id)
        {
            bool ok = Guid.TryParse(s, out var g);
            id = new EntityHandle(g);
            return ok;
        }
    }
}
