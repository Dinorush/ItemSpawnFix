using BepInEx.Unity.IL2CPP.Hook;
using GTFO.API;
using HarmonyLib;
using Il2CppInterop.Runtime.Runtime;
using LevelGeneration;
using System;
using GameData;
using ItemSpawnFix.Redistribute;

namespace ItemSpawnFix.Patches
{
    [HarmonyPatch]
    internal static class LG_PopulatePatches
    {
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

            RedistributeUtils.DistributeFunction = distItem.m_function;
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
        }

        private unsafe static void TriggerFunctionBuilderPatch(IntPtr _this, IntPtr builder, IntPtr distItem, out IntPtr deepestSpawner, bool debug, Il2CppMethodInfo* methodInfo)
        {
            LG_PopulateFunctionMarkersInZoneJob job = new(_this);
            LG_FunctionMarkerBuilder markerBuilder = new(builder);
            RedistributeUtils.DistributeFunction = markerBuilder.GetFunction();

            LG_DistributeItem temp = new(distItem);
            if (job.m_fallbackMode && RedistributeUtils.DistributeFunction == ExpeditionFunction.ResourceContainerWeak)
            {
                LG_DistributeResourceContainer item = new(distItem);
                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"No markers left in {item.m_assignedNode.m_zone.NavInfo.ToString()} {item.m_assignedNode.m_area.m_navInfo.ToString()}, redistributing {RedistributeUtils.GetPackListString(item.m_packs)}");

                // If resources can be distributed to existing boses, don't spawn anything
                deepestSpawner = IntPtr.Zero;
                if (RedistributeUtils.TryRedistributeItems(item.m_assignedNode, item.m_packs, out var remainingItems))
                {
                    RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
                    return;
                }

                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"Sending remaining items to floor spawns: {RedistributeUtils.GetPackListString(remainingItems)}");
                // If resources remained, let them spawn on the floor
                item.m_packs.Clear();
                foreach (var pack in remainingItems)
                    item.m_packs.Add(pack);
                LG_ResourceContainerBuilder resourceBuilder = new(builder);
                resourceBuilder.m_packs = item.m_packs;
            }

            orig_TriggerFunctionBuilder!(_this, builder, distItem, out deepestSpawner, debug, methodInfo);

            if (!job.m_fallbackMode && deepestSpawner == IntPtr.Zero)
            {
                LG_DistributeItem item = new(distItem);
                if (item.m_allowFunctionFallback)
                {
                    if (Configuration.ShowDebugMessages)
                        DinoLogger.Log($"No markers remaining for {item.m_assignedNode.m_zone.NavInfo.ToString()} {item.m_assignedNode.m_area.m_navInfo.ToString()}, moving to floor fallback");
                    ChangeGenericFallbackToSpecific(job, item);
                }
                else
                {
                    if (Configuration.ShowDebugMessages)
                        DinoLogger.Log($"No marker spawner found for {item.m_assignedNode.m_zone.NavInfo.ToString()} {item.m_assignedNode.m_area.m_navInfo.ToString()}, moving to floor fallback");

                    var fallbackData = job.m_zone.DistributionDataFallback;
                    if (item.m_function == ExpeditionFunction.ResourceContainerWeak)
                        fallbackData.ResourceContainerItems.Enqueue(item.Cast<LG_DistributeResourceContainer>());
                    else if (item.m_function == ExpeditionFunction.SmallPickupItem || item.m_function == ExpeditionFunction.BigPickupItem)
                        fallbackData.PickupItems.Enqueue(item.Cast<LG_DistributePickUpItem>());
                    else
                        fallbackData.GenericFunctionItems.Enqueue(item);
                }
            }

            RedistributeUtils.DistributeFunction = ExpeditionFunction.None;
        }

        private static void ChangeGenericFallbackToSpecific(LG_PopulateFunctionMarkersInZoneJob job, LG_DistributeItem distItem)
        {
            var fallbackData = job.m_zone.DistributionDataFallback;
            switch (distItem.m_function)
            {
                case ExpeditionFunction.ResourceContainerWeak:
                    fallbackData.ResourceContainerItems.Enqueue(distItem.Cast<LG_DistributeResourceContainer>());
                    break;
                case ExpeditionFunction.SmallPickupItem:
                case ExpeditionFunction.BigPickupItem:
                    fallbackData.PickupItems.Enqueue(distItem.Cast<LG_DistributePickUpItem>());
                    break;
                default:
                    return;
            }

            var queue = fallbackData.GenericFunctionItems.m_itemQueue;
            var lastPos = (queue._tail - 1 + queue._array.Count) % queue._array.Count;
            if (queue._array[lastPos].Pointer == distItem.Pointer)
            {
                queue._array[lastPos] = null;
                queue._tail = lastPos;
                queue._size--;
            }
        }
    }
}
