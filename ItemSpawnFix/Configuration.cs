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
        private readonly static ConfigEntry<bool> _raiseObjectPriority;
        public static bool RaiseObjectPriority => _raiseObjectPriority.Value;
        private readonly static ConfigEntry<bool> _allowMoveObjects;
        public static bool AllowMoveObjects => _allowMoveObjects.Value;

        private readonly static ConfigFile configFile;

        static Configuration()
        {
            configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, EntryPoint.MODNAME + ".cfg"), saveOnInit: true);
            string section = "Debug Settings";
            _showDebugMessages = configFile.Bind(section, "Enable Logs", false, "Prints information to the logs when redistributing resources.");

            section = "Rundown Settings";
            _raiseObjectPriority = configFile.Bind(section, "Raise Object Priority", false, "Changes the spawn order to prioritize important objects:\n1. Objects (Bulkhead/Terminals/Generators/...)\n2. Big Pickups/Consumables\n3. Resources\n\nVanilla Order:\n1. Certain objects (Bulkhead/...)\n2. Resources\n3. Big Pickups/Consumables\n4. Other Objects (Terminals/Generators/...)");

            _allowMoveObjects = configFile.Bind(section, "Allow Moving Objects", false, "Allows objects (e.g. Terminals) to be redistributed to other areas if no markers exist in their area.");

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
