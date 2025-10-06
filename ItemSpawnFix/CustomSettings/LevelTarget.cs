using ItemSpawnFix.JSON.Converters;
using System.Text.Json.Serialization;

namespace ItemSpawnFix.CustomSettings
{
    [JsonConverter(typeof(LevelTargetConverter))]
    public sealed class LevelTarget
    {
        public uint LevelLayoutID { get; set; } = 0;
        public uint RundownID { get; set; } = 0;
        public eRundownTier Tier { get; set; } = eRundownTier.Surface;
        public int TierIndex { get; set; } = -1;

        public bool IsMatch(uint layoutID, uint rundownID, eRundownTier tier, int tierIndex)
        {
            if (layoutID == LevelLayoutID)
            {
                return true;
            }
            else if (RundownID == 0 || RundownID == rundownID)
            {
                if (Tier == tier && (TierIndex == -1 || TierIndex == tierIndex))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
