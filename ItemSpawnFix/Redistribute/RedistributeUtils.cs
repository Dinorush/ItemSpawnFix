using AIGraph;
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
        private readonly static List<(LG_ResourceContainer_Storage storage, StorageTracker slots)> _storages = new();
        private readonly static List<(LG_ResourceContainer_Storage storage, StorageTracker slots)> _storagesEmpty = new();
        private readonly static Dictionary<IntPtr, List<(LG_ResourceContainer_Storage storage, StorageTracker slots)>> _nodeStorages = new();
        private readonly static Dictionary<IntPtr, List<(LG_ResourceContainer_Storage storage, StorageTracker slots)>> _nodeStoragesEmpty = new();
        private static StorageTracker? _currentTracker;

        public static void Init()
        {
            LevelAPI.OnBuildStart += OnBuildStart;
        }

        // JFS - Should be cleared by OnZoneFinished
        private static void OnBuildStart()
        {
            _storages.Clear();
            _storagesEmpty.Clear();
            _nodeStorages.Clear();
            _nodeStoragesEmpty.Clear();
        }

        internal static void OnZoneFinished()
        {
            foreach ((var storage, var _) in _storagesEmpty)
                SNet.DestroySelfManagedReplicatedObject(storage.transform.parent.gameObject);

            _storages.Clear();
            _storagesEmpty.Clear();
            _nodeStorages.Clear();
            _nodeStoragesEmpty.Clear();
        }

        internal static void OnContainerStorageSpawned(LG_ResourceContainer_Storage storage)
        {
            if (DistributeFunction != ExpeditionFunction.ResourceContainerWeak) return;

            var storagePair = (storage, _currentTracker = new(storage));
            _storages.Add(storagePair);
            _storagesEmpty.Add(storagePair);
            var node = storage.m_core.SpawnNode;
            if (node == null) return;

            List<(LG_ResourceContainer_Storage, StorageTracker)> emptyStorages;
            if (!_nodeStorages.TryGetValue(node.Pointer, out var storages))
            {
                _nodeStorages.Add(node.Pointer, storages = new());
                _nodeStoragesEmpty.Add(node.Pointer, emptyStorages = new());
            }
            else
                emptyStorages = _nodeStoragesEmpty[node.Pointer];
            storages.Add(storagePair);
            emptyStorages.Add(storagePair);
        }

        internal static void OnContainerItemSpawned(LG_ResourceContainer_Storage storage, Transform align)
        {
            if (DistributeFunction != ExpeditionFunction.ResourceContainerWeak || _currentTracker == null) return;

            if (_currentTracker.Unused)
            {
                _storagesEmpty.RemoveAt(_storagesEmpty.Count - 1);

                var node = storage.m_core.SpawnNode;
                if (node != null && _nodeStoragesEmpty.TryGetValue(node.Pointer, out var storagesEmpty))
                    storagesEmpty.RemoveAt(storagesEmpty.Count - 1);
            }

            if (!_currentTracker.RemoveAndCheckSpace(align))
            {
                _storages.RemoveAt(_storages.Count - 1);

                var node = storage.m_core.SpawnNode;
                if (node != null && _nodeStorages.TryGetValue(node.Pointer, out var storages))
                    storages.RemoveAt(storages.Count - 1);
            }
        }

        public static bool TryRedistributeItems(AIG_CourseNode node, Il2Collection.List<ResourceContainerSpawnData> items, out List<ResourceContainerSpawnData> remainingItems, bool empty)
        {
            remainingItems = new();
            if (items.Count == 0) return true;

            remainingItems.EnsureCapacity(items.Count);
            foreach (var pack in items)
                remainingItems.Add(pack);

            if (_storages.Count == 0) return false;

            if (empty)
            {
                if (_nodeStoragesEmpty.TryGetValue(node.Pointer, out var nodeStoragesEmpty))
                    TryRedistributeToList(remainingItems, nodeStoragesEmpty, _nodeStorages[node.Pointer]);
                TryRedistributeToList(remainingItems, _storagesEmpty, _storages);
            }
            else
            {
                if (_nodeStorages.TryGetValue(node.Pointer, out var nodeStorages))
                    TryRedistributeToList(remainingItems, nodeStorages);
                TryRedistributeToList(remainingItems, _storages);
            }

            return remainingItems.Count == 0;
        }

        private static bool TryRedistributeToList(List<ResourceContainerSpawnData> items, List<(LG_ResourceContainer_Storage, StorageTracker)> storages, List<(LG_ResourceContainer_Storage, StorageTracker)>? parent = null)
        {
            if (storages.Count == 0) return false;

            var oldTracker = _currentTracker;
            _currentTracker = null; // Prevent patch from modifying the list while we're using it
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int> shuffle = Enumerable.Range(0, storages.Count).ToArray();
            Builder.SessionSeedRandom.ShuffleArray(shuffle);
            List<int> removeIndices = new();
            for (int i = 0; i < shuffle.Length && items.Count > 0; i++)
            {
                (var storage, var slots) = storages[shuffle[i]];
                while (slots.Count > 0 && items.Count > 0)
                {
                    if (!slots.RemoveRandomAndCheckSpace(out var slot))
                    {
                        // If using only the empty storages and no slots remain, need to remove from parent too
                        if (parent != null)
                        {
                            int parIndex = parent.FindIndex(pair => pair.Item1.Pointer == storage.Pointer);
                            parent.RemoveAt(parIndex);
                        }
                        removeIndices.Add(shuffle[i]);
                    }
                    // If using empty storages, remove as soon as any slot is used
                    else if (parent != null)
                        removeIndices.Add(shuffle[i]);

                    SpawnItem(storage, slot, items[^1]);
                    items.RemoveAt(items.Count - 1);
                }
            }

            removeIndices.Sort((a, b) => b.CompareTo(a));
            foreach (var index in removeIndices)
                storages.RemoveAt(index);
            _currentTracker = oldTracker;
            return items.Count > 0;
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
