using BepInEx.Unity.IL2CPP;
using MTFO.API;
using System.Runtime.CompilerServices;

namespace ItemSpawnFix.Dependencies
{
    internal static class MTFOWrapper
    {
        public const string PLUGIN_GUID = "com.dak.MTFO";
        public readonly static bool HasMTFO;

        static MTFOWrapper()
        {
            HasMTFO = IL2CPPChainloader.Instance.Plugins.ContainsKey(PLUGIN_GUID);
        }
        
        public static string GameDataPath => HasMTFO ? GameDataPath_Unsafe() : "";
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GameDataPath_Unsafe() => MTFOPathAPI.RundownPath;

        public static string CustomPath => HasMTFO ? CustomPath_Unsafe() : "";
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string CustomPath_Unsafe() => MTFOPathAPI.CustomPath;

        public static bool HasCustomContent => HasMTFO ? HasCustomContent_Unsafe() : false;
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HasCustomContent_Unsafe() => MTFOPathAPI.HasRundownPath;
    }
}
