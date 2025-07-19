using AIGraph;
using GameData;
using GTFO.API;
using HarmonyLib;
using LevelGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Il2Collection = Il2CppSystem.Collections.Generic;

namespace ItemSpawnFix.Redistribute
{
    [HarmonyPatch]
    public static class RedistributeUtils
    {
        public static ExpeditionFunction DistributeFunction { get; set; } = ExpeditionFunction.None;
        private readonly static List<Transform> _seenAligns = new();
        private readonly static Dictionary<IntPtr, List<(LG_ResourceContainer_Storage storage, StorageTracker slots)>> _nodeToStorages = new();

        public static void Init()
        {
            LevelAPI.OnBuildStart += ClearSeenData;
            LevelAPI.OnBuildDone += ClearSeenData;
        }

        private static void ClearSeenData()
        {
            _seenAligns.Clear();
        }

        [HarmonyPatch(typeof(LG_PopulateFunctionMarkersInZoneJob), nameof(LG_PopulateFunctionMarkersInZoneJob.BuildBothFunctionAndPropMarkerAndRemoveSurplus))]
        [HarmonyPrefix]
        private static void Pre_BuildMarker(LG_FunctionMarkerBuilder builder)
        {
            DistributeFunction = builder.GetFunction();
        }

        [HarmonyPatch(typeof(LG_PopulateFunctionMarkersInZoneJob), nameof(LG_PopulateFunctionMarkersInZoneJob.BuildBothFunctionAndPropMarkerAndRemoveSurplus))]
        [HarmonyPostfix]
        private static void Post_BuildMarker()
        {
            DistributeFunction = ExpeditionFunction.None;
        }

        private static StorageTracker? _currentTracker;
        [HarmonyPatch(typeof(LG_ResourceContainer_Storage), nameof(LG_ResourceContainer_Storage.Setup))]
        [HarmonyPostfix]
        private static void Post_Setup(LG_ResourceContainer_Storage __instance)
        {
            if (DistributeFunction != ExpeditionFunction.ResourceContainerWeak) return;

            var node = __instance.m_core.SpawnNode;
            if (node == null) return;

            if (!_nodeToStorages.TryGetValue(node.Pointer, out var storages))
                _nodeToStorages.Add(node.Pointer, storages = new());
            storages.Add((__instance, _currentTracker = new(__instance)));
        }

        [HarmonyPatch(typeof(LG_ResourceContainer_Storage))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.SpawnResourcePack))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.SpawnCommodity))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.SpawnConsumable))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.PlaceKeyCard))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.PlaceSmallGenericPickup))]
        [HarmonyPatch(nameof(LG_ResourceContainer_Storage.SpawnArtifact))]
        [HarmonyPostfix]
        private static void Post_SpawnItem(LG_ResourceContainer_Storage __instance, Transform align)
        {
            if (DistributeFunction != ExpeditionFunction.ResourceContainerWeak || _currentTracker == null) return;

            var node = __instance.m_core.SpawnNode;
            if (node == null) return;

            var list = _nodeToStorages[node.Pointer];
            if (_currentTracker.RemoveAndCheckSpace(align))
                list.RemoveAt(list.Count - 1);
        }

        public static bool TryRedistributeItems(AIG_CourseNode node, Il2Collection.List<ResourceContainerSpawnData> items, out List<ResourceContainerSpawnData> remainingItems)
        {
            remainingItems = new();
            remainingItems.EnsureCapacity(items.Count);
            foreach (var pack in items)
                remainingItems.Add(pack);

            if (!_nodeToStorages.TryGetValue(node.Pointer, out var validContainers) || validContainers.Count == 0)
            {
                if (Configuration.ShowDebugMessages)
                    DinoLogger.Error($"No valid containers to place {GetPackListString(remainingItems)}!");
                return false;
            }

            if (Configuration.ShowDebugMessages)
                DinoLogger.Log($"Found {validContainers.Count} containers not yet filled ({validContainers.Sum(pair => pair.slots.Count)} available slots)");

            _currentTracker = null; // Prevent patch from modifying the list while we're using it
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int> shuffle = Enumerable.Range(0, validContainers.Count).ToArray();
            Builder.SessionSeedRandom.ShuffleArray(shuffle);
            List<int> removeIndices = new();
            for (int i = 0; i < shuffle.Length && remainingItems.Count > 0; i++)
            {
                (var storage, var slots) = validContainers[shuffle[i]];
                while (slots.Count > 0 && remainingItems.Count > 0)
                {
                    if (!slots.RemoveRandomAndCheckSpace(out var slot))
                        removeIndices.Add(shuffle[i]);
                    SpawnItem(storage, slot, remainingItems[^1]);
                    remainingItems.RemoveAt(remainingItems.Count - 1);
                }
            }

            removeIndices.Sort((a, b) => b.CompareTo(a));
            foreach (var index in removeIndices)
                validContainers.RemoveAt(index);

            if (remainingItems.Count > 0)
            {
                if (Configuration.ShowDebugMessages)
                    DinoLogger.Error($"No remaining containers to place {GetPackListString(remainingItems)}!");
                return false;
            }
            return true;
        }

        private static void SpawnItem(LG_ResourceContainer_Storage storage, StorageSlot storageSlot, ResourceContainerSpawnData data)
        {
            int seed = Builder.SessionSeedRandom.Range(0, int.MaxValue);
            switch (data.m_type)
            {
                case eResourceContainerSpawnType.Health:
                case eResourceContainerSpawnType.AmmoWeapon:
                case eResourceContainerSpawnType.AmmoTool:
                case eResourceContainerSpawnType.Disinfection:
                    if (storageSlot.ResourcePack != null)
                        storage.SpawnResourcePack(data, storageSlot.ResourcePack, seed);
                    break;
                case eResourceContainerSpawnType.CommoditySmall:
                    if (storageSlot.CommoditySmall != null)
                        storage.SpawnCommodity(data, storageSlot.CommoditySmall, seed);
                    break;
                case eResourceContainerSpawnType.CommodityMedium:
                    if (storageSlot.CommodityMedium != null)
                        storage.SpawnCommodity(data, storageSlot.CommodityMedium, seed);
                    break;
                case eResourceContainerSpawnType.CommodityLarge:
                    if (storageSlot.CommodityLarge != null)
                        storage.SpawnCommodity(data, storageSlot.CommodityLarge, seed);
                    break;
                case eResourceContainerSpawnType.Consumable:
                    if (storageSlot.Consumable != null)
                        storage.SpawnConsumable(data, storageSlot.Consumable, seed);
                    break;
                case eResourceContainerSpawnType.Keycard:
                    if (storageSlot.Keycard != null)
                        storage.PlaceKeyCard(data, storageSlot.Keycard, seed);
                    break;
                case eResourceContainerSpawnType.SmallGenericPickup:
                    if (storageSlot.Keycard != null)
                        storage.PlaceSmallGenericPickup(data, storageSlot.Keycard, seed);
                    break;
                case eResourceContainerSpawnType.Artifact:
                    if (storageSlot.Keycard != null)
                        storage.SpawnArtifact(data, storageSlot.Keycard, seed);
                    break;
            }
        }

        public static string GetPackListString(List<ResourceContainerSpawnData> packs)
        {
            StringBuilder sb = new("[");
            for (int i = 0; i < packs.Count - 1; i++)
                sb.Append($"({packs[i].m_type}:{packs[i].m_ammo}),");

            if (packs.Count > 0)
                sb.Append($"({packs[^1].m_type}:{packs[^1].m_ammo})");

            sb.Append(']');
            return sb.ToString();
        }

        public static string GetPackListString(Il2Collection.List<ResourceContainerSpawnData> packs)
        {
            StringBuilder sb = new("[");
            for (int i = 0; i < packs.Count - 1; i++)
                sb.Append($"({packs[i].m_type}:{packs[i].m_ammo}),");

            if (packs.Count > 0)
                sb.Append($"({packs[^1].m_type}:{packs[^1].m_ammo})");

            sb.Append(']');
            return sb.ToString();
        }
    }
}
