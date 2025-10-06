using ItemSpawnFix.CustomSettings;
using ItemSpawnFix.Utils;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ItemSpawnFix.JSON.Converters
{
    public sealed class LevelTargetConverter : JsonConverter<LevelTarget>
    {
        public override LevelTarget? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            LevelTarget target = new();

            if (ParseTarget(ref reader, target, options))
                return target;

            throw new JsonException("Expected level target to be a tier, tierindex, or level layout ID");
        }

        private static bool ParseTarget(ref Utf8JsonReader reader, LevelTarget target, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return true;

            if (reader.TokenType == JsonTokenType.String)
            {
                string text = reader.GetString()!;

                // Try to parse it as "TierX"
                if (text.Length >= 5)
                    target.Tier = text[..5].ToEnum(eRundownTier.Surface);
                // Try to parse it as "X"
                else
                    target.Tier = ("Tier" + text[0]).ToEnum(eRundownTier.Surface);

                int indexStart = text.Length >= 5 ? 5 : 1;
                if (target.Tier != eRundownTier.Surface && text.Length > indexStart)
                {
                    if (int.TryParse(text[indexStart..], out int index))
                        target.TierIndex = index - 1;
                    else // More in the text than tier, but not a number - must not be in TierIndex format
                        target.Tier = eRundownTier.Surface;
                }

                if (target.Tier == eRundownTier.Surface) // Partial data ID case
                    target.LevelLayoutID = JsonSerializer.Deserialize<uint>(ref reader, options);
                return true;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                target.LevelLayoutID = reader.GetUInt32();
                return true;
            }
            return false;
        }

        public override void Write(Utf8JsonWriter writer, LevelTarget? value, JsonSerializerOptions options)
        {
            if (value == null) return;

            if (value.LevelLayoutID == 0 && value.Tier == eRundownTier.Surface)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.LevelLayoutID != 0)
                writer.WriteNumberValue(value.LevelLayoutID);
            else
                writer.WriteStringValue(value.Tier.ToString() + (value.TierIndex >= 0 ? value.TierIndex + 1 : ""));
        }
    }
}
