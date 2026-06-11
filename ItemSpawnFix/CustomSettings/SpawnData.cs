using GameData;
using ItemSpawnFix.Redistribute;
using LevelGeneration;
using System.Diagnostics.CodeAnalysis;

namespace ItemSpawnFix.CustomSettings
{
    public abstract class BaseSpawnData
    {
        public eDimensionIndex DimensionIndex { get; set; } = eDimensionIndex.Reality;
        public LG_LayerType Layer { get; set; } = LG_LayerType.MainLayer;
        public eLocalZoneIndex LocalIndex { get; set; } = 0;
        public int AreaIndex { get; set; } = 0;
        public bool PreferEmpty { get; set; } = true;
        public int Count { get; set; } = 1;

        public abstract bool TryGetContainerData([MaybeNullWhen(false)] out ResourceContainerSpawnData data);
    }

    public class ConsumableSpawnData : BaseSpawnData
    {
        public uint ItemID { get; set; } = 0;

        public override bool TryGetContainerData([MaybeNullWhen(false)] out ResourceContainerSpawnData data)
        {
            if (ItemID == 0)
            {
                data = null;
                return false;
            }

            data = new()
            {
                m_type = eResourceContainerSpawnType.Consumable,
                m_comType = RedistributeUtils.CustomCommodityType,
                m_genericItemId = ItemID
            };
            return true;
        }
    }

    public class ResourceSpawnData : BaseSpawnData
    {
        public eResourceContainerSpawnType PackType { get; set; } = eResourceContainerSpawnType.Health;
        public float Amount { get; set; } = 0;

        public override bool TryGetContainerData([MaybeNullWhen(false)] out ResourceContainerSpawnData data)
        {
            if (Amount == 0)
            {
                data = null;
                return false;
            }

            var balancing = RundownManager.ActiveExpeditionBalanceData;
            var packMod = PackType switch
            {
                eResourceContainerSpawnType.Health => balancing.HealthPerZone,
                eResourceContainerSpawnType.AmmoWeapon => balancing.WeaponAmmoPerZone,
                eResourceContainerSpawnType.AmmoTool => balancing.ToolAmmoPerZone,
                eResourceContainerSpawnType.Disinfection => balancing.DisinfectionPerZone,
                _ => -1f
            };

            if (packMod < 0)
            {
                DinoLogger.Error($"Unable to do set resource spawn! Type is invalid ({PackType})!");
                data = null;
                return false;
            }
            
            data = new()
            {
                m_type = PackType,
                m_ammo = Amount * packMod
            };
            return true;
        }
    }
}
