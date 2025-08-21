using GameData;
using GTFO.API;
using HarmonyLib;
using LevelGeneration;
using SNetwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Il2Collection = Il2CppSystem.Collections.Generic;

namespace ItemSpawnFix.Redistribute
{
    [HarmonyPatch(typeof(LG_ResourceContainer_Storage))]
    public static class RedistributeUtils
    {
        public static ExpeditionFunction DistributeFunction { get; set; } = ExpeditionFunction.None;
        private readonly static Dictionary<IntPtr, List<(LG_ResourceContainer_Storage storage, StorageTracker slots, LG_DistributeResourceContainer distItem)>> _zoneToStorages = new();
        private readonly static Dictionary<IntPtr, GameObject> _failedDistContainers = new();
        public static LG_DistributeResourceContainer? DistributeItem { get; set; } = null;
        private static StorageTracker? _currentTracker;

        public static void Init()
        {
            LevelAPI.OnBuildStart += ClearStoredBoxes;
            LevelAPI.OnBuildDone += ClearStoredBoxes;
        }

        private static void ClearStoredBoxes()
        {
            _zoneToStorages.Clear();
            _failedDistContainers.Clear();
        }

        internal static void OnContainerStorageSpawned(LG_ResourceContainer_Storage storage)
        {
            if (DistributeFunction != ExpeditionFunction.ResourceContainerWeak || DistributeItem == null) return;

            var node = storage.m_core.SpawnNode;
            if (node == null) return;

            var zone = node.m_zone;
            if (!_zoneToStorages.TryGetValue(zone.Pointer, out var storages))
                _zoneToStorages.Add(zone.Pointer, storages = new());
            storages.Add((storage, _currentTracker = new(storage), DistributeItem));
            DistributeItem = null;
        }

        internal static void OnContainerItemSpawned(LG_ResourceContainer_Storage storage, Transform align)
        {
            if (DistributeFunction != ExpeditionFunction.ResourceContainerWeak || _currentTracker == null) return;

            var node = storage.m_core.SpawnNode;
            if (node == null) return;

            var list = _zoneToStorages[node.m_zone.Pointer];
            if (!_currentTracker.RemoveAndCheckSpace(align))
                list.RemoveAt(list.Count - 1);
        }

        internal static void AddFailedDistItem(IntPtr distItemPtr, GameObject spawnedGO) => _failedDistContainers.Add(distItemPtr, spawnedGO);

        internal static void OnDistributionDone()
        { 
            foreach (var spawnedGO in _failedDistContainers.Values)
                SNet.DestroySelfManagedReplicatedObject(spawnedGO);
            _failedDistContainers.Clear();
        }

        public static bool TryRedistributeItems(LG_Zone zone, Il2Collection.List<ResourceContainerSpawnData> items, out List<ResourceContainerSpawnData> remainingItems)
        {
            remainingItems = new();
            remainingItems.EnsureCapacity(items.Count);
            foreach (var pack in items)
                remainingItems.Add(pack);

            if (!_zoneToStorages.TryGetValue(zone.Pointer, out var validContainers) || validContainers.Count == 0)
            {
                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"No valid containers to place {GetPackListString(remainingItems)}!");
                return false;
            }

            if (Configuration.ShowDebugMessages)
                DinoLogger.Log($"Found {validContainers.Count} containers not yet filled ({validContainers.Sum(pair => pair.slots.Count)} available slots)");

            var oldTracker = _currentTracker;
            _currentTracker = null; // Prevent patch from modifying the list while we're using it
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int> shuffle = Enumerable.Range(0, validContainers.Count).ToArray();
            Builder.SessionSeedRandom.ShuffleArray(shuffle);
            List<int> removeIndices = new();
            for (int i = 0; i < shuffle.Length && remainingItems.Count > 0; i++)
            {
                (var storage, var slots, var distItem) = validContainers[shuffle[i]];
                while (slots.Count > 0 && remainingItems.Count > 0)
                {
                    if (!slots.RemoveRandomAndCheckSpace(out var slot))
                        removeIndices.Add(shuffle[i]);
                    SpawnItem(storage, slot, remainingItems[^1], distItem);
                    remainingItems.RemoveAt(remainingItems.Count - 1);
                }
            }

            removeIndices.Sort((a, b) => b.CompareTo(a));
            foreach (var index in removeIndices)
                validContainers.RemoveAt(index);
            _currentTracker = oldTracker;

            if (remainingItems.Count > 0)
            {
                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"No remaining containers to place {GetPackListString(remainingItems)}!");
                return false;
            }
            return true;
        }

        private static void SpawnItem(LG_ResourceContainer_Storage storage, StorageSlot storageSlot, ResourceContainerSpawnData data, LG_DistributeResourceContainer distItem)
        {
            distItem.m_packs.Add(data);
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
