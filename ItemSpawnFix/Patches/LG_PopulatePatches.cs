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
    [HarmonyPatch(typeof(LG_PopulateFunctionMarkersInZoneJob))]
    internal static class LG_PopulatePatches
    {
        private readonly static List<AIG_CourseNode> _validNodes = new();
        private static int _currZoneID = -1;

        [HarmonyPatch(nameof(LG_PopulateFunctionMarkersInZoneJob.Build))]
        [HarmonyPostfix]
        private static void PostBuild()
        {
            RedistributeUtils.OnZoneFinished();
        }

        [HarmonyPatch(nameof(LG_PopulateFunctionMarkersInZoneJob.BuildBothFunctionAndPropMarkerAndRemoveSurplus))]
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
            // ShouldBeRemoved is overriden for boxes to allow redistributing
            bool isRes = function == ExpeditionFunction.ResourceContainerWeak;
            LG_DistributeResourceContainer distRes = isRes ? new(distItem) : null!;
            bool empty = isRes ? distRes.m_packs.Count == 0 : item.ShouldBeRemoved();

            if (job.m_fallbackMode)
            {
                var node = item.m_assignedNode;
                var areaString = node.m_area.m_navInfo.ToString();
                var zoneString = node.m_zone.NavInfo.ToString();
                if (!empty && Configuration.ShowDebugMessages)
                    DinoLogger.Log($"Creating {function} with floor fallback in {zoneString} {areaString}");

                if (empty)
                    deepestSpawner = IntPtr.Zero;
                else if (isRes && TryRedistributeItems(item.m_assignedNode, distRes, empty: false))
                    deepestSpawner = IntPtr.Zero;
                else
                    orig_TriggerFunctionBuilder!(_this, builder, distItem, out deepestSpawner, debug, methodInfo);

                RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
                return;
            }

            // Fallback functionality is handled manually. This avoids the original function adding it to the wrong queue.
            item.m_allowFunctionFallback = false;

            orig_TriggerFunctionBuilder!(_this, builder, distItem, out deepestSpawner, debug, methodInfo);

            if (empty)
            {
                RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
                return;
            }

            if (deepestSpawner == IntPtr.Zero)
            {
                var node = item.m_assignedNode;
                var areaString = node.m_area.m_navInfo.ToString();
                var zone = node.m_zone;
                var zoneString = zone.NavInfo.ToString();
                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"No markers remaining for distribute {function} in {zoneString} {areaString} trying to redistribute");

                if (isRes && TryRedistributeItems(node, distRes, empty: true))
                {
                    deepestSpawner = IntPtr.Zero;
                    RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
                    return;
                }

                if (_currZoneID != zone.ID)
                {
                    _currZoneID = zone.ID;
                    _validNodes.Clear();
                    foreach (var area in zone.m_areas)
                        _validNodes.Add(area.m_courseNode);
                }

                int removeIndex = _validNodes.FindLastIndex(n => n.NodeID == node.NodeID);
                if (removeIndex >= 0)
                    _validNodes.RemoveAt(removeIndex);

                while (_validNodes.Count > 0)
                {
                    int index = Builder.SessionSeedRandom.Range(0, _validNodes.Count, "LG_PopulateFunctionMarkersInZone_Retry_Zone");
                    markerBuilder.m_node = _validNodes[index];
                    item.m_assignedNode = _validNodes[index];
                    orig_TriggerFunctionBuilder!(_this, builder, distItem, out deepestSpawner, debug, methodInfo);
                    if (deepestSpawner == IntPtr.Zero)
                        _validNodes.RemoveAt(index);
                    else
                    {
                        if (Configuration.ShowDebugMessages)
                            DinoLogger.Log($"Redistributed {function} to {zoneString} {_validNodes[index].m_area.m_navInfo.ToString()} (orig: {areaString})");
                        RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
                        return;
                    }
                }

                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"No markers available in {zoneString}");

                if (isRes && TryRedistributeItems(node, distRes, empty: false))
                {
                    deepestSpawner = IntPtr.Zero;
                    RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
                    return;
                }

                markerBuilder.m_node = node;
                item.m_assignedNode = node;
                // Everything failed, spawn on the floor
                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"Unable to redistribute {function} in {zoneString}, moving to floor fallback in {areaString}");

                AddToFallbackQueue(job, item);
            }

            RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
        }

        private static bool TryRedistributeItems(AIG_CourseNode node, LG_DistributeResourceContainer distRes, bool empty)
        {
            var zoneString = node.m_zone.NavInfo.ToString();
            // If resources can be distributed to boxes, don't spawn anything
            if (RedistributeUtils.TryRedistributeItems(node, distRes.m_packs, out var remainingItems, empty))
            {
                if (empty)
                    DinoLogger.Log($"Redistributed {RedistributeUtils.GetPackListString(distRes.m_packs)} to empty containers in {zoneString}");
                else
                    DinoLogger.Log($"Redistributed {RedistributeUtils.GetPackListString(distRes.m_packs)} to containers in {zoneString}");
                return true;
            }

            if (Configuration.ShowDebugMessages)
            {
                if (empty)
                    DinoLogger.Log($"Not enough empty containers in {zoneString}, sending remaining items to new container spawns: {RedistributeUtils.GetPackListString(remainingItems)}");
                else
                    DinoLogger.Log($"Not enough containers in {zoneString}, sending remaining items to floor container spawns: {RedistributeUtils.GetPackListString(remainingItems)}");
            }

            // If resources remained, let them spawn on the floor
            distRes.m_packs.Clear();
            foreach (var pack in remainingItems)
                distRes.m_packs.Add(pack);
            return false;
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
