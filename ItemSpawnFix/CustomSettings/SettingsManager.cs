using GTFO.API;
using GTFO.API.Utilities;
using ItemSpawnFix.Dependencies;
using ItemSpawnFix.JSON;
using ItemSpawnFix.Utils;
using System.Collections.Generic;
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

        private readonly Dictionary<string, List<SettingsData>> _fileToData = new();

        private readonly LiveEditListener _liveEditListener;

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
            var oldSettings = ActiveSettings;
            ActiveSettings = SettingsData.Default;

            var rundownID = ActiveRundownID();
            if (rundownID == 0) return;

            var expData = RundownManager.GetActiveExpeditionData();
            if (expData.tier == eRundownTier.Surface) return;

            if (_currentLevel.tier == expData.tier && _currentLevel.tierIndex == expData.expeditionIndex)
            {
                ActiveSettings = oldSettings;
                return;
            }

            var layoutID = RundownManager.ActiveExpedition.LevelLayoutData;
            var enumerator = Current.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var data = enumerator.Current;
                if (data.RundownID != 0 && data.RundownID != rundownID) continue;

                foreach (var target in data.Levels)
                {
                    if (target.IsMatch(layoutID, rundownID, expData.tier, expData.expeditionIndex))
                    {
                        ActiveSettings = data;
                        return;
                    }
                }
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

            _liveEditListener = LiveEdit.CreateListener(DEFINITION_PATH, "*.json", true);
            _liveEditListener.FileCreated += FileCreated;
            _liveEditListener.FileChanged += FileChanged;
            _liveEditListener.FileDeleted += FileDeleted;
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
