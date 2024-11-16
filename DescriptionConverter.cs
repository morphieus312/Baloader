using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

public class DescriptionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(string) || objectType == typeof(List<string>);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (token.Type == JTokenType.String)
        {
            return token.ToString();
        }
        else if (token.Type == JTokenType.Array)
        {
            return string.Join(" ", token.ToObject<List<string>>());
        }
        throw new JsonSerializationException("Unexpected token type: " + token.Type);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is string)
        {
            writer.WriteValue(value);
        }
        else if (value is List<string> list)
        {
            writer.WriteStartArray();
            foreach (var item in list)
            {
                writer.WriteValue(item);
            }
            writer.WriteEndArray();
        }
        else
        {
            throw new JsonSerializationException("Unexpected value type: " + value.GetType());
        }
    }
}
