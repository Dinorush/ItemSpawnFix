using ItemSpawnFix.JSON.Converters;
using System;
using System.Text.Json.Serialization;

namespace ItemSpawnFix.CustomSettings
{
    [JsonConverter(typeof(SettingsDataConverter))]
    public sealed class SettingsData
    {
        public readonly static SettingsData Default = new();
        public readonly static SettingsData[] Template = new SettingsData[]
        {
            new()
            {
                Levels = new[] 
                { 
                    new LevelTarget() 
                    { 
                        LevelLayoutID = 420
                    } 
                }
            },
            new()
            {
                Levels = new[]
                {
                    new LevelTarget()
                    {
                        Tier = eRundownTier.TierA
                    }
                }
            },
            new()
            {
                Levels = new[]
                {
                    new LevelTarget()
                    {
                        LevelLayoutID = 4115
                    },
                    new LevelTarget()
                    {
                        Tier = eRundownTier.TierB,
                        TierIndex = 0
                    }
                }
            }
        };

        public LevelTarget[] Levels { get; set; } = Array.Empty<LevelTarget>();
        public bool RaiseObjectSpawnPriority { get; set; } = false;
        public bool AllowRedistributeObjects { get; set; } = false;
    }
}
