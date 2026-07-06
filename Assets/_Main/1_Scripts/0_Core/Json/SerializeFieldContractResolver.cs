using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace DesktopCompanion.Core
{
    /// <summary>
    /// Unity 직렬화 규칙(public 필드 + private [SerializeField])대로 Newtonsoft가
    /// 필드를 읽고 쓰게 하는 ContractResolver. 콘텐츠·세이브 양 채널 공용(§15.1).
    /// 프로퍼티는 무시하고 "필드만" 직렬화하므로 Vector2.normalized 같은
    /// 자기참조 프로퍼티 무한루프도 자연히 회피된다.
    /// </summary>
    public class SerializeFieldContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = new List<JsonProperty>();
            var seen = new HashSet<string>();

            // private 필드는 상속 계층에 숨어 있으므로 베이스 타입까지 직접 순회한다.
            for (Type t = type; t != null && t != typeof(object) && t != typeof(UnityEngine.Object); t = t.BaseType)
            {
                var fields = t.GetFields(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                foreach (FieldInfo field in fields)
                {
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                    {
                        continue;   // private는 [SerializeField]만 포함
                    }
                    if (field.Name.Contains("<"))
                    {
                        continue;   // 자동 프로퍼티 backing field 제외
                    }
                    if (!seen.Add(field.Name))
                    {
                        continue;   // 파생 타입의 동명 필드 우선
                    }

                    JsonProperty property = CreateProperty(field, memberSerialization);
                    property.Readable = true;
                    property.Writable = true;
                    properties.Add(property);
                }
            }
            return properties;
        }
    }
}
