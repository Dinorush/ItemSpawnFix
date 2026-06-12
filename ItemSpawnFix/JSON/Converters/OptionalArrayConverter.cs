using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ItemSpawnFix.JSON.Converters
{
    public sealed class OptionalArrayConverter<T> : JsonConverter<T[]>
    {
        public override bool HandleNull => true;

        public override T[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return Array.Empty<T>();

            if (reader.TokenType != JsonTokenType.StartArray)
                return new T[] { JsonSerializer.Deserialize<T>(ref reader, options)! };

            List<T> builder = new();
            // Full object case
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return builder.ToArray();

                builder.Add(JsonSerializer.Deserialize<T>(ref reader, options)!);
            }

            throw new JsonException("Expected EndArray token, ran out of tokens");
        }

        public override void Write(Utf8JsonWriter writer, T[]? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.Length == 0)
            {
                ISFJson.Serialize(writer, default(T), options);
                return;
            }

            if (value.Length == 1)
            {
                ISFJson.Serialize(writer, value[0], options);
                return;
            }

            writer.WriteStartArray();
            foreach (var item in value)
                ISFJson.Serialize(writer, item, options);
            writer.WriteEndArray();
        }
    }
}
