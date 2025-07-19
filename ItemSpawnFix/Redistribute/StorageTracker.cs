using LevelGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemSpawnFix.Redistribute
{
    public class StorageTracker
    {
        private readonly List<(StorageSlot slot, HashSet<IntPtr> alignSet)> _slotAligns;

        public StorageTracker(LG_ResourceContainer_Storage storage)
        {
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
        }

        private void TryAddAlign(Transform align, HashSet<IntPtr> alignSet)
        {
            if (align != null)
                alignSet.Add(align.Pointer);
        }

        public int Count => _slotAligns.Count;

        public bool RemoveRandomAndCheckSpace(out StorageSlot slot)
        {
            var index = Builder.SessionSeedRandom.Range(0, Count);
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
