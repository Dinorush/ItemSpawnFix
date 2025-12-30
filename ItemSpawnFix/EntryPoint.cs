using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ItemSpawnFix.CustomSettings;
using ItemSpawnFix.Dependencies;
using ItemSpawnFix.Patches;
using ItemSpawnFix.Redistribute;

namespace ItemSpawnFix
{
    [BepInPlugin("Dinorush." + MODNAME, MODNAME, "1.2.3")]
    [BepInDependency("dev.gtfomodding.gtfo-api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(MTFOWrapper.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]
    internal sealed class EntryPoint : BasePlugin
    {
        public const string MODNAME = "ItemSpawnFix";

        public override void Load()
        {
            SettingsManager.Init();
            Configuration.Init();
            RedistributeUtils.Init();
            LG_DistributionPatches.Init();
            LG_PopulatePatches.Init();
            new Harmony(MODNAME).PatchAll();
            Log.LogMessage("Loaded " + MODNAME);
        }
    }
}