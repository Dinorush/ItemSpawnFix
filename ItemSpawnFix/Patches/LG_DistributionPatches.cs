using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Class;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using LevelGeneration;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ItemSpawnFix.Patches
{
    [HarmonyPatch]
    internal static class LG_DistributionPatches
    {
        private readonly static Dictionary<IntPtr, LG_DistributeResourceContainer> _distItems = new();
        [HarmonyPatch(typeof(LG_DistributeResourceContainer), nameof(LG_DistributeResourceContainer.ShouldBeRemoved))]
        [HarmonyPostfix]
        private static void Post_ShouldBeRemoved(ref bool __result)
        {
            // Cancel resource containers being removed automatically
            __result = false;
        }

        private static bool _inDistribute = false;
        [HarmonyPatch(typeof(LG_Distribute_ResourcePacksPerZone), nameof(LG_Distribute_ResourcePacksPerZone.Build))]
        [HarmonyPrefix]
        private static void Pre_Build()
        {
            _inDistribute = true;
        }

        [HarmonyPatch(typeof(LG_Distribute_ResourcePacksPerZone), nameof(LG_Distribute_ResourcePacksPerZone.Build))]
        [HarmonyPostfix]
        private static void Post_Build()
        {
            _inDistribute = false;
        }

        private static INativeDetour? GetDistributionDetour;
        private static d_Enqueue? orig_Enqueue;
        private unsafe delegate void d_Enqueue(IntPtr _this, IntPtr item, Il2CppMethodInfo* methodInfo);

        public unsafe static void Init()
        {
            INativeClassStruct val = UnityVersionHandler.Wrap((Il2CppClass*)Il2CppClassPointerStore<DistributeItemQueueWrapper<LG_DistributeResourceContainer>>.NativeClassPtr);

            for (int i = 0; i < val.MethodCount; i++)
            {
                INativeMethodInfoStruct val2 = UnityVersionHandler.Wrap(val.Methods[i]);

                if (Marshal.PtrToStringAnsi(val2.Name) == "Enqueue")
                {
                    GetDistributionDetour = INativeDetour.CreateAndApply<d_Enqueue>(val2.MethodPointer, EnqueuePatch, out orig_Enqueue);
                    return;
                }
            }
        }

        private unsafe static void EnqueuePatch(IntPtr _this, IntPtr item, Il2CppMethodInfo* methodInfo)
        {
            orig_Enqueue!(_this, item, methodInfo);

            if (_inDistribute)
            {
                LG_DistributeItem distItem = new(item);
                distItem.m_assignedNode.FunctionDistributionPerAreaLookup[distItem.m_function].Add(distItem);
                if (Configuration.ShowDebugMessages)
                    DinoLogger.Log($"New distribution created for {distItem.m_assignedNode.m_zone.NavInfo.ToString()} {distItem.m_assignedNode.m_area.m_navInfo.ToString()} of type {distItem.m_function} added to lookup");
            }
        }
    }
}
