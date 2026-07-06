using System;
using System.Collections.Generic;
using DesktopCompanion.Data;

namespace DesktopCompanion.Core
{
    /// <summary>
    /// GameData 구체 타입을 이름으로 인덱싱하는 공용 헬퍼.
    /// 런타임 로더(JsonDataLoader)와 에디터 변환 툴(GameDataConverter)이 공유한다.
    /// </summary>
    public static class GameDataTypes
    {
        public static Dictionary<string, Type> BuildTypeMap()
        {
            var map = new Dictionary<string, Type>();
            foreach (Type type in typeof(GameData).Assembly.GetTypes())
            {
                if (typeof(GameData).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    map[type.Name] = type;
                }
            }
            return map;
        }
    }
}
