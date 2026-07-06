using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DesktopCompanion.Data;

namespace DesktopCompanion.Core
{
    /// <summary>
    /// GameData 간 직접 참조를 JSON에서 { "type": "...", "id": n }으로 표현하는 컨버터(D14·§15.2).
    /// 클래스는 직접 참조를 유지하고, 읽기 시 리졸버(사전 등록된 인스턴스 레지스트리)로 해소한다.
    /// 2-pass 로드(JsonDataLoader) 전제: 1차에서 전 인스턴스 등록 → 2차 Populate 때 이 컨버터가 즉시 해소.
    /// </summary>
    public class GameDataRefConverter : JsonConverter
    {
        private readonly Func<string, int, GameData> m_resolver;   // (typeName, id) → 인스턴스

        public GameDataRefConverter(Func<string, int, GameData> resolver) => m_resolver = resolver;

        public override bool CanConvert(Type objectType)
            => typeof(GameData).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            JObject reference = JObject.Load(reader);
            string typeName = reference.Value<string>("type");
            int id = reference.Value<int>("id");

            GameData resolved = m_resolver?.Invoke(typeName, id);
            if (resolved == null)
            {
                Debug.LogError($"[GameDataRefConverter] 참조 해소 실패: type={typeName} id={id}");
            }
            else if (!objectType.IsInstanceOfType(resolved))
            {
                Debug.LogError($"[GameDataRefConverter] 참조 타입 불일치: {typeName}#{id} → 기대 {objectType.Name}");
                return null;
            }
            return resolved;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            var data = (GameData)value;
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue(data.GetType().Name);
            writer.WritePropertyName("id");
            writer.WriteValue(data.ID);
            writer.WriteEndObject();
        }
    }
}
