using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ItemSpawnFix.Patches;
using ItemSpawnFix.Redistribute;

namespace ItemSpawnFix
{
    [BepInPlugin("Dinorush." + MODNAME, MODNAME, "1.0.2")]
    internal sealed class EntryPoint : BasePlugin
    {
        public const string MODNAME = "ItemSpawnFix";

        public override void Load()
        {
            Configuration.Init();
            RedistributeUtils.Init();
            LG_DistributionPatches.Init();
            LG_PopulatePatches.Init();
            new Harmony(MODNAME).PatchAll();
            Log.LogMessage("Loaded " + MODNAME);
        }
    }
}