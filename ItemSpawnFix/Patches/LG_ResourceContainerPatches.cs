using HarmonyLib;
using ItemSpawnFix.Redistribute;
using LevelGeneration;
using UnityEngine;

namespace ItemSpawnFix.Patches
{
    [HarmonyPatch(typeof(LG_ResourceContainer_Storage))]
    internal static class LG_ResourceContainerPatches
    {
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.Setup))]
        [HarmonyPostfix]
        private static void Post_Setup(LG_ResourceContainer_Storage __instance)
        {
            RedistributeUtils.OnContainerStorageSpawned(__instance);
        }

        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.SpawnResourcePack))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.SpawnCommodity))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.SpawnConsumable))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.PlaceKeyCard))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.PlaceSmallGenericPickup))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.SpawnArtifact))]
        [HarmonyPostfix]
        private static void Post_SpawnItem(LG_ResourceContainer_Storage __instance, Transform align)
        {
            RedistributeUtils.OnContainerItemSpawned(__instance, align);
        }
    }
}
