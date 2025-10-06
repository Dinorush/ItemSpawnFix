using System;
using System.Text.Json;

namespace ItemSpawnFix.JSON
{
    public static class ISFJson
    {
        private static readonly JsonSerializerOptions _setting = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            IgnoreReadOnlyProperties = true,
        };

        public static T? Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _setting);
        }

        public static T? Deserialize<T>(ref Utf8JsonReader reader)
        {
            return JsonSerializer.Deserialize<T>(ref reader, _setting);
        }

        public static object? Deserialize(Type type, string json)
        {
            return JsonSerializer.Deserialize(json, type, _setting);
        }

        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, _setting);
        }

        public static void Serialize<T>(Utf8JsonWriter writer, T value)
        {
            JsonSerializer.Serialize(writer, value, _setting);
        }

        public static void Serialize<T>(Utf8JsonWriter writer, string name, T value)
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, value, _setting);
        }
    }
}
