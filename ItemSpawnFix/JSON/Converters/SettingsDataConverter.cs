using ItemSpawnFix.CustomSettings;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ItemSpawnFix.JSON.Converters
{
    public sealed class SettingsDataConverter : JsonConverter<SettingsData>
    {
        public override SettingsData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            SettingsData data = new();

            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected Trigger Coordinator to be either a string or object");

            // Full object case
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return data;

                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected PropertyName token");

                string property = reader.GetString()!;
                reader.Read();
                switch (property.ToLowerInvariant())
                {
                    case "levels":
                    case "level":
                        if (reader.TokenType == JsonTokenType.StartArray)
                            data.Levels = JsonSerializer.Deserialize<LevelTarget[]>(ref reader, options)!;
                        else if (reader.TokenType != JsonTokenType.Null)
                            data.Levels = new LevelTarget[] { JsonSerializer.Deserialize<LevelTarget>(ref reader, options)! };
                        break;
                    case "raiseobjectspawnpriority":
                        data.RaiseObjectSpawnPriority = reader.GetBoolean();
                        break;
                    case "allowredistributeobjects":
                        data.AllowRedistributeObjects = reader.GetBoolean();
                        break;
                            
                }
            }

            throw new JsonException("Expected EndObject token");
        }

        public override void Write(Utf8JsonWriter writer, SettingsData? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName(nameof(value.Levels));
            if (value.Levels.Length == 0)
                writer.WriteNullValue();
            else if (value.Levels.Length == 1)
                JsonSerializer.Serialize(writer, value.Levels[0], options);
            else
                JsonSerializer.Serialize(writer, value.Levels, options);
            writer.WriteBoolean(nameof(value.RaiseObjectSpawnPriority), value.RaiseObjectSpawnPriority);
            writer.WriteBoolean(nameof(value.AllowRedistributeObjects), value.AllowRedistributeObjects);
            writer.WriteEndObject();
        }
    }
}
