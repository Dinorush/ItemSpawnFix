using AssetShards;
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

        [HarmonyPatch(typeof(LG_ResourceContainer_Storage), nameof(LG_ResourceContainer_Storage.SpawnConsumable))]
        [HarmonyWrapSafe]
        [HarmonyPrefix]
        private static bool Pre_SpawnConsumable(LG_ResourceContainer_Storage __instance, ResourceContainerSpawnData pack, Transform align, int randomSeed)
        {
            if (pack.m_comType != RedistributeUtils.CustomCommodityType) return true;

            LG_PickupItem lG_PickupItem = GOUtil.SpawnChildAndGetComp<LG_PickupItem>(AssetShardManager.GetLoadedAsset<GameObject>(__instance.m_pickupAssetPath), align);
            lG_PickupItem.SetupAsConsumable(randomSeed, pack.m_genericItemId);
            __instance.SetSpawnNode(lG_PickupItem.gameObject, __instance.m_core.SpawnNode);
            __instance.DisableInteraction(lG_PickupItem.gameObject);
            return false;
        }
    }
}
