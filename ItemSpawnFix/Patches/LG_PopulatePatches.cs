using AIGraph;
using BepInEx.Unity.IL2CPP.Hook;
using GameData;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.Runtime;
using ItemSpawnFix.Redistribute;
using LevelGeneration;
using System;
using System.Collections.Generic;

namespace ItemSpawnFix.Patches
{
    [HarmonyPatch]
    internal static class LG_PopulatePatches
    {
        private readonly static List<AIG_CourseNode> _validNodes = new();
        private static int _currZoneID = -1;

        [HarmonyPatch(typeof(LG_PopulateFunctionMarkersInZoneJob), nameof(LG_PopulateFunctionMarkersInZoneJob.BuildBothFunctionAndPropMarkerAndRemoveSurplus))]
        [HarmonyWrapSafe]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPrefix]
        private static bool OverrideFallbackLogic(LG_PopulateFunctionMarkersInZoneJob __instance, LG_FunctionMarkerBuilder builder, LG_DistributeItem distItem)
        {
            if (!__instance.m_fallbackMode) return true;

            // Excluded builders skip to TriggerFunctionBuilder
            switch (distItem.m_function)
            {
                case ExpeditionFunction.SmallPickupItem:
                case ExpeditionFunction.BigPickupItem:
                case ExpeditionFunction.ResourceContainerWeak:
                    break;
                default:
                    return true;
            }

            __instance.TriggerFunctionBuilder(builder, distItem, out _);
            return false;
        }

        private static INativeDetour? TriggerFunctionBuilderDetour;
        private static d_BuildFunc? orig_TriggerFunctionBuilder;
        private unsafe delegate void d_BuildFunc(IntPtr _this, IntPtr builder, IntPtr distItem, out IntPtr deepestSpawner, bool debug, Il2CppMethodInfo* methodInfo);

        // Can't harmony patch the function due to out parameter so need a native detour
        public unsafe static void Init()
        {
            TriggerFunctionBuilderDetour = INativeDetour.CreateAndApply(
                (nint)Il2CppAPI.GetIl2CppMethod<LG_PopulateFunctionMarkersInZoneJob>(
                    nameof(LG_PopulateFunctionMarkersInZoneJob.TriggerFunctionBuilder),
                    typeof(void).Name,
                    false,
                    new[] {
                        nameof(LG_FunctionMarkerBuilder),
                        nameof(LG_DistributeItem),
                        typeof(LG_MarkerSpawner).MakeByRefType().FullName,
                        typeof(bool).Name
                    }),
                TriggerFunctionBuilderPatch,
                out orig_TriggerFunctionBuilder
                );

            LevelAPI.OnBuildStart += () => _currZoneID = -1;
        }

        private unsafe static void TriggerFunctionBuilderPatch(IntPtr _this, IntPtr builder, IntPtr distItem, out IntPtr deepestSpawner, bool debug, Il2CppMethodInfo* methodInfo)
        {
            LG_PopulateFunctionMarkersInZoneJob job = new(_this);
            LG_FunctionMarkerBuilder markerBuilder = new(builder);
            LG_DistributeItem item = new(distItem);
            var function = RedistributeUtils.DistributeFunction = item.m_function;

            if (item.ShouldBeRemoved())
            {
                deepestSpawner = IntPtr.Zero;
                return;
            }

            // Fallback functionality is handled manually. This avoids the original function adding it to the wrong queue.
            item.m_allowFunctionFallback = false;
            var zone = item.m_assignedNode.m_zone;

            if (job.m_fallbackMode && function == ExpeditionFunction.ResourceContainerWeak)
            {
                LG_DistributeResourceContainer distRes = new(distItem);
                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"Redistributing floor-spawned container with {RedistributeUtils.GetPackListString(distRes.m_packs)} to existing containers");

                // If resources can be distributed to existing boses, don't spawn anything
                if (RedistributeUtils.TryRedistributeItems(zone, distRes.m_packs, out var remainingItems))
                {
                    deepestSpawner = IntPtr.Zero;
                    RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
                    return;
                }

                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"Sending remaining items to floor spawns: {RedistributeUtils.GetPackListString(remainingItems)}");
                // If resources remained, let them spawn on the floor
                distRes.m_packs.Clear();
                foreach (var pack in remainingItems)
                    distRes.m_packs.Add(pack);
                LG_ResourceContainerBuilder resourceBuilder = new(builder);
                resourceBuilder.m_packs = distRes.m_packs;
            }

            orig_TriggerFunctionBuilder!(_this, builder, distItem, out deepestSpawner, debug, methodInfo);

            if (!job.m_fallbackMode && deepestSpawner == IntPtr.Zero)
            {
                if (_currZoneID != zone.ID)
                {
                    _currZoneID = zone.ID;
                    _validNodes.Clear();
                    foreach (var area in zone.m_areas)
                        _validNodes.Add(area.m_courseNode);
                }
                
                if (Configuration.ShowDebugMessages && _validNodes.Count > 0)
                    DinoLogger.Log($"No markers remaining for distribute {function} in {zone.NavInfo.ToString()} {item.m_assignedNode.m_area.m_navInfo.ToString()}, placing in random area in zone");

                var node = item.m_assignedNode;
                for (int i = _validNodes.Count - 1; i >= 0; i--)
                {
                    if (_validNodes[i].NodeID == node.NodeID)
                    {
                        _validNodes.RemoveAt(i);
                        break;
                    }
                }

                while (_validNodes.Count > 0)
                {
                    int index = Builder.SessionSeedRandom.Range(0, _validNodes.Count, "LG_PopulateFunctionMarkersInZone_Retry_Zone");
                    markerBuilder.m_node = _validNodes[index];
                    item.m_assignedNode = _validNodes[index];
                    orig_TriggerFunctionBuilder!(_this, builder, distItem, out deepestSpawner, debug, methodInfo);
                    if (deepestSpawner == IntPtr.Zero)
                        _validNodes.RemoveAt(index);
                    else
                        break;
                }

                if (deepestSpawner == IntPtr.Zero)
                {
                    if (Configuration.ShowDebugMessages)
                        DinoLogger.Log($"No markers remaining for distribute {function} in {zone.NavInfo.ToString()}, moving to floor fallback");
                    markerBuilder.m_node = node;
                    item.m_assignedNode = node;

                    AddToFallbackQueue(job, item);
                }
            }

            RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
        }

        private static void AddToFallbackQueue(LG_PopulateFunctionMarkersInZoneJob job, LG_DistributeItem distItem)
        {
            var fallbackData = job.m_zone.DistributionDataFallback;
            if (distItem.m_function == ExpeditionFunction.ResourceContainerWeak)
                fallbackData.ResourceContainerItems.Enqueue(distItem.Cast<LG_DistributeResourceContainer>());
            else if (distItem.m_function == ExpeditionFunction.SmallPickupItem || distItem.m_function == ExpeditionFunction.BigPickupItem)
                fallbackData.PickupItems.Enqueue(distItem.Cast<LG_DistributePickUpItem>());
            else
                fallbackData.GenericFunctionItems.Enqueue(distItem);
        }
    }
}
