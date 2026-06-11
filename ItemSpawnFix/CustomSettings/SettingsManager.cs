using GameData;
using GTFO.API;
using GTFO.API.Utilities;
using ItemSpawnFix.Dependencies;
using ItemSpawnFix.JSON;
using ItemSpawnFix.Utils;
using LevelGeneration;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ItemSpawnFix.CustomSettings
{
    public sealed class SettingsManager
    {
        public static readonly SettingsManager Current;
        public static SettingsData ActiveSettings { get; set; } = SettingsData.Default;
        private static (eRundownTier tier, int tierIndex) _currentLevel = (eRundownTier.Surface, 0);
        private static readonly Dictionary<(eDimensionIndex, LG_LayerType, eLocalZoneIndex), List<BaseSpawnData>> _currentSetSpawns = new();

        public static bool TryGetSetSpawns(LG_Zone zone, [MaybeNullWhen(false)] out List<BaseSpawnData> list) => _currentSetSpawns.TryGetValue((zone.DimensionIndex, zone.Layer.m_type, zone.LocalIndex), out list);
        public static void ClearSetSpawns(LG_Zone zone) => _currentSetSpawns.Remove((zone.DimensionIndex, zone.Layer.m_type, zone.LocalIndex));

        private readonly Dictionary<string, List<SettingsData>> _fileToData = new();

        private void FileChanged(LiveEditEventArgs e)
        {
            DinoLogger.Warning($"LiveEdit File Changed: {e.FileName}");
            LiveEdit.TryReadFileContent(e.FullPath, (content) =>
            {
                ReadFileContent(e.FullPath, content);
            });
        }

        private void FileDeleted(LiveEditEventArgs e)
        {
            DinoLogger.Warning($"LiveEdit File Removed: {e.FileName}");

            _fileToData.Remove(e.FullPath);
            RefreshSettings();
        }

        private void FileCreated(LiveEditEventArgs e)
        {
            DinoLogger.Warning($"LiveEdit File Created: {e.FileName}");
            LiveEdit.TryReadFileContent(e.FullPath, (content) =>
            {
                ReadFileContent(e.FullPath, content);
            });
        }

        private void ReadFileContent(string file, string content)
        {
            _fileToData.Remove(file);

            List<SettingsData>? dataList = null;
            try
            {
                dataList = ISFJson.Deserialize<List<SettingsData>>(content);
            }
            catch (JsonException ex)
            {
                DinoLogger.Error("Error parsing settings json " + file);
                DinoLogger.Error(ex.Message);
            }

            if (dataList == null) return;

            _fileToData[file] = dataList;
            RefreshSettings();
        }

        private static uint ActiveRundownID()
        {
            var rundownKey = RundownManager.ActiveRundownKey;
            if (!RundownManager.RundownProgressionReady || !RundownManager.TryGetIdFromLocalRundownKey(rundownKey, out var rundownID)) return 0u;

            return rundownID;
        }

        private static void RefreshSettings()
        {
            var rundownID = ActiveRundownID();
            if (rundownID == 0)
            {
                ActiveSettings = SettingsData.Default;
                _currentSetSpawns.Clear();
                return;
            }

            var expData = RundownManager.GetActiveExpeditionData();
            if (expData.tier == eRundownTier.Surface)
            {
                ActiveSettings = SettingsData.Default;
                _currentSetSpawns.Clear();
                return;
            }

            if (_currentLevel.tier == expData.tier && _currentLevel.tierIndex == expData.expeditionIndex) return;

            var layoutID = RundownManager.ActiveExpedition.LevelLayoutData;
            var enumerator = Current.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var data = enumerator.Current;
                if (data.RundownID != 0 && data.RundownID != rundownID) continue;

                foreach (var target in data.Levels)
                {
                    if (target.IsMatch(layoutID, expData.tier, expData.expeditionIndex))
                    {
                        ActiveSettings = data;
                        CacheSetSpawns();
                        return;
                    }
                }
            }
        }

        private static void CacheSetSpawns()
        {
            _currentSetSpawns.Clear();
            foreach (var spawn in ActiveSettings.SetConsumableSpawns)
            {
                var location = (spawn.DimensionIndex, spawn.Layer, spawn.LocalIndex);
                var list = _currentSetSpawns.GetOrAdd(location);
                list.EnsureCapacity(list.Count + spawn.Count);
                for (int i = 0; i < spawn.Count; i++)
                    list.Add(spawn);
            }

            foreach (var spawn in ActiveSettings.SetResourceSpawns)
            {
                var location = (spawn.DimensionIndex, spawn.Layer, spawn.LocalIndex);
                var list = _currentSetSpawns.GetOrAdd(location);
                list.EnsureCapacity(list.Count + spawn.Count);
                for (int i = 0; i < spawn.Count; i++)
                    list.Add(spawn);
            }
        }

        private SettingsManager()
        {
            if (!MTFOWrapper.HasCustomContent)
            {
                DinoLogger.Log($"No custom path detected! Has MTFO? {MTFOWrapper.HasMTFO}");
                return;
            }

            string DEFINITION_PATH = Path.Combine(MTFOWrapper.CustomPath, EntryPoint.MODNAME);
            if (!Directory.Exists(DEFINITION_PATH))
            {
                DinoLogger.Log("No settings directory detected. Creating template.");
                Directory.CreateDirectory(DEFINITION_PATH);
                var file = File.CreateText(Path.Combine(DEFINITION_PATH, "Template.json"));
                file.WriteLine(ISFJson.Serialize(SettingsData.Template));
                file.Flush();
                file.Close();
            }
            else
                DinoLogger.Log("SettingsData directory detected.");

            foreach (string confFile in Directory.EnumerateFiles(DEFINITION_PATH, "*.json", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(confFile);
                ReadFileContent(confFile, content);
            }

            var liveEditListener = LiveEdit.CreateListener(DEFINITION_PATH, "*.json", true);
            liveEditListener.FileCreated += FileCreated;
            liveEditListener.FileChanged += FileChanged;
            liveEditListener.FileDeleted += FileDeleted;
        }

        static SettingsManager() => Current = new();

        internal static void Init() 
        {
            LevelAPI.OnBuildStart += RefreshSettings;
        }

        public List<SettingsData> GetSettingsData()
        {
            List<SettingsData> dataList = new(_fileToData.Values.Sum(list => list.Count));
            IEnumerator<SettingsData> dataEnumerator = GetEnumerator();
            while (dataEnumerator.MoveNext())
                dataList.Add(dataEnumerator.Current);
            return dataList;
        }

        public IEnumerator<SettingsData> GetEnumerator() => new DictListEnumerator<SettingsData>(_fileToData);
    }
}
