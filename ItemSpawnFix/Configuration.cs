using BepInEx.Configuration;
using BepInEx;
using System.IO;
using GTFO.API.Utilities;

namespace ItemSpawnFix
{
    internal static class Configuration
    {
        private readonly static ConfigEntry<bool> _showDebugMessages;
        public static bool ShowDebugMessages => _showDebugMessages.Value;

        private readonly static ConfigFile configFile;

        static Configuration()
        {
            configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, EntryPoint.MODNAME + ".cfg"), saveOnInit: true);
            string section = "Debug Settings";
            _showDebugMessages = configFile.Bind(section, "Enable Logs", false, "Prints information to the logs when redistributing resources.");
        }

        public static void Init()
        {
            LiveEditListener listener = LiveEdit.CreateListener(Paths.ConfigPath, EntryPoint.MODNAME + ".cfg", false);
            listener.FileChanged += OnFileChanged;
        }

        private static void OnFileChanged(LiveEditEventArgs _)
        {
            configFile.Reload();
        }
    }
}
