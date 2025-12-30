using LevelGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemSpawnFix.Redistribute
{
    public class StorageTracker
    {
        private static uint s_nextID = 0; 

        public readonly int NodeID;
        public readonly uint ID;
        public readonly LG_ResourceContainer_Storage Storage;

        private readonly List<(StorageSlot slot, HashSet<IntPtr> alignSet)> _slotAligns;
        private readonly int _maxCount;

        public StorageTracker(LG_ResourceContainer_Storage storage, int nodeID)
        {
            NodeID = nodeID;
            ID = s_nextID++;
            Storage = storage;

            _slotAligns = new();
            foreach (var slot in storage.m_storageSlots)
            {
                HashSet<IntPtr> alignSet = new();
                _slotAligns.Add((slot, alignSet));
                TryAddAlign(slot.ResourcePack, alignSet);
                TryAddAlign(slot.CommoditySmall, alignSet);
                TryAddAlign(slot.CommodityMedium, alignSet);
                TryAddAlign(slot.CommodityLarge, alignSet);
                TryAddAlign(slot.Consumable, alignSet);
                TryAddAlign(slot.Keycard, alignSet);
            }
            _maxCount = _slotAligns.Count;
        }

        private void TryAddAlign(Transform align, HashSet<IntPtr> alignSet)
        {
            if (align != null)
                alignSet.Add(align.Pointer);
        }

        public bool Unused => _slotAligns.Count == _maxCount;
        public int Count => _slotAligns.Count;

        public bool RemoveRandomAndCheckSpace(out StorageSlot slot)
        {
            var index = RedistributeUtils.Random.Next(0, Count);
            slot = _slotAligns[index].slot;
            _slotAligns.RemoveAt(index);
            return Count > 0;
        }

        public bool RemoveAndCheckSpace(Transform align)
        {
            var alignSet = _slotAligns.First(pair => pair.alignSet.Contains(align.Pointer));
            _slotAligns.Remove(alignSet);
            return Count > 0;
        }
    }
}
