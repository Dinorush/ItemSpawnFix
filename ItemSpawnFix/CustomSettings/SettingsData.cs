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
                },
                SetResourceSpawns = new[]
                {
                    new ResourceSpawnData()
                    {
                        PackType = eResourceContainerSpawnType.AmmoWeapon,
                        AreaIndex = new int[] { 0, 1 }
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
                },
                SetConsumableSpawns = new[]
                {
                    new ConsumableSpawnData()
                }
            }
        };

        public LevelTarget[] Levels { get; set; } = Array.Empty<LevelTarget>();
        public uint RundownID = 0;
        public bool RaiseObjectSpawnPriority { get; set; } = false;
        public bool AllowRedistributeObjects { get; set; } = false;
        public ConsumableSpawnData[] SetConsumableSpawns { get; set; } = Array.Empty<ConsumableSpawnData>();
        public ResourceSpawnData[] SetResourceSpawns { get; set; } = Array.Empty<ResourceSpawnData>();
    }
}
