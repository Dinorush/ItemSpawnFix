using AIGraph;
using GameData;
using GTFO.API;
using HarmonyLib;
using ItemSpawnFix.Utils;
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
        public static System.Random Random { get; private set; } = new();
        public static ExpeditionFunction DistributeFunction { get; set; } = ExpeditionFunction.None;

        private readonly static Dictionary<int, List<StorageTracker>> _nodeTrackers = new();
        private readonly static Dictionary<int, List<StorageTracker>> _nodeTrackersEmpty = new();
        private static StorageTracker? _currentTracker;
        
        public static void Init()
        {
            LevelAPI.OnBuildStart += OnBuildStart;
        }

        // JFS - Should be cleared by OnZoneFinished
        private static void OnBuildStart()
        {
            SetSeed(Builder.BuildSeedRandom.Seed);
            _nodeTrackers.Clear();
            _nodeTrackersEmpty.Clear();
        }

        internal static void OnZoneFinished()
        {
            foreach (var list in _nodeTrackersEmpty.Values)
                foreach (var tracker in list)
                    SNet.DestroySelfManagedReplicatedObject(tracker.Storage.transform.parent.gameObject);

            _nodeTrackers.Clear();
            _nodeTrackersEmpty.Clear();
        }

        internal static void OnContainerStorageSpawned(LG_ResourceContainer_Storage storage)
        {
            if (DistributeFunction != ExpeditionFunction.ResourceContainerWeak) return;

            var node = storage.m_core.SpawnNode;
            if (node == null) return;

            var nodeID = node.NodeID;
            _currentTracker = new(storage, nodeID);
            List<StorageTracker> emptyTrackers;
            if (!_nodeTrackers.TryGetValue(nodeID, out var trackers))
            {
                _nodeTrackers.Add(nodeID, trackers = new());
                _nodeTrackersEmpty.Add(nodeID, emptyTrackers = new());
            }
            else
                emptyTrackers = _nodeTrackersEmpty[nodeID];
            trackers.Add(_currentTracker);
            emptyTrackers.Add(_currentTracker);
        }

        internal static void OnContainerItemSpawned(LG_ResourceContainer_Storage storage, Transform align)
        {
            if (DistributeFunction != ExpeditionFunction.ResourceContainerWeak || _currentTracker == null) return;

            if (_currentTracker.Unused)
            {
                var node = storage.m_core.SpawnNode;
                if (node != null && _nodeTrackersEmpty.TryGetValue(node.NodeID, out var trackersEmpty))
                    trackersEmpty.RemoveAt(trackersEmpty.Count - 1);
            }

            if (!_currentTracker.RemoveAndCheckSpace(align))
            {
                var node = storage.m_core.SpawnNode;
                if (node != null && _nodeTrackers.TryGetValue(node.NodeID, out var trackers))
                    trackers.RemoveAt(trackers.Count - 1);
            }
        }

        public static void SetSeed(int seed) => Random = new(seed);

        public static bool TryRedistributeItems(AIG_CourseNode node, Il2Collection.List<ResourceContainerSpawnData> items, out List<ResourceContainerSpawnData> remainingItems, bool empty)
        {
            remainingItems = new();
            if (items.Count == 0) return true;

            remainingItems.EnsureCapacity(items.Count);
            foreach (var pack in items)
                remainingItems.Add(pack);

            var trackerDict = empty ? _nodeTrackersEmpty : _nodeTrackers;
            if (trackerDict.TryGetValue(node.NodeID, out var trackers) && TryRedistributeToList(remainingItems, trackers, empty))
                return true;

            List<StorageTracker> global = new(trackerDict.Values.Sum(list => list.Count));
            foreach (var list in trackerDict.Values)
                global.AddRange(list);
            return TryRedistributeToList(remainingItems, global, empty);
        }

        private static bool TryRedistributeToList(List<ResourceContainerSpawnData> items, List<StorageTracker> trackers, bool empty = false)
        {
            if (trackers.Count == 0) return false;

            var oldTracker = _currentTracker;
            _currentTracker = null; // Prevent patch from modifying the list while we're using it

            Dictionary<int, HashSet<uint>> toRemove = new();
            var shuffle = Enumerable.Range(0, trackers.Count).ToArray();
            Shuffle(shuffle);

            for (int i = 0; i < shuffle.Length && items.Count > 0; i++)
            {
                int index = shuffle[i];
                var tracker = trackers[index];
                while (tracker.Count > 0 && items.Count > 0)
                {
                    if (!tracker.RemoveRandomAndCheckSpace(out var slot) && !empty)
                        toRemove.GetOrAdd(tracker.NodeID).Add(tracker.ID);

                    SpawnItem(tracker.Storage, slot, items[^1]);
                    items.RemoveAt(items.Count - 1);
                }

                // If using empty storages, remove when used at all
                if (empty)
                    toRemove.GetOrAdd(tracker.NodeID).Add(tracker.ID);
            }

            foreach ((var nodeID, var idSet) in toRemove)
            {
                if (empty)
                {
                    RemoveFromList(_nodeTrackersEmpty[nodeID], idSet);
                    RemoveFromList(_nodeTrackers[nodeID], idSet, (tracker) => tracker.Count > 0);
                }
                else
                {
                    RemoveFromList(_nodeTrackers[nodeID], idSet);
                }
            }

            _currentTracker = oldTracker;
            return items.Count == 0;
        }

        private static void RemoveFromList(List<StorageTracker> list, HashSet<uint> idsToRemove, Predicate<StorageTracker>? cond = null)
        {
            int newIndex = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var tracker = list[i];
                if (!idsToRemove.Contains(tracker.ID) || cond?.Invoke(tracker) == true)
                    list[newIndex++] = tracker;
            }
            list.RemoveRange(newIndex, list.Count - newIndex);
        }

        private static void Shuffle<T>(T[] array)
        {
            int num = array.Length;
            while (num > 1)
            {
                int num2 = Random.Next(0, num--);
                (array[num2], array[num]) = (array[num], array[num2]);
            }
        }

        private static void SpawnItem(LG_ResourceContainer_Storage storage, StorageSlot storageSlot, ResourceContainerSpawnData data)
        {
            int seed = Random.Next();
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
