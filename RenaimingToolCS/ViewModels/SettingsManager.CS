using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace RenaimingToolCS.Helpers
{
    public class SettingsManager
    {
        private static readonly string _configFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RenaimingToolCS",
            "settings.json");

        private static SettingsManager _instance;

        // Properties for currently selected paths
        public string CurrentCreoPath { get; set; } = string.Empty;
        public string CurrentProDirectory { get; set; } = string.Empty;
        public string CurrentProCommMsgExe { get; set; } = string.Empty;
        public string CurrentVbApiRegisterBatPath { get; set; } = string.Empty;
        public string CurrentCommonFilesFolder { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;

        // History collections for the dropdowns
        public ObservableCollection<string> CreoPathHistory { get; set; }
        public ObservableCollection<string> ProDirectoryHistory { get; set; }
        public ObservableCollection<string> ProCommMsgExeHistory { get; set; }
        public ObservableCollection<string> VbApiRegisterBatPathHistory { get; set; }
        public ObservableCollection<string> CommonFilesFolderHistory { get; set; }

        public SettingsManager()
        {
            // Initialize history collections
            CreoPathHistory = new ObservableCollection<string>();
            ProDirectoryHistory = new ObservableCollection<string>();
            ProCommMsgExeHistory = new ObservableCollection<string>();
            VbApiRegisterBatPathHistory = new ObservableCollection<string>();
            CommonFilesFolderHistory = new ObservableCollection<string>();
        }

        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SettingsManager();
                    // Load settings from file immediately after creation
                    _instance.Load();
                }
                return _instance;
            }
        }

        public void AddPathToHistory(string newPath, ObservableCollection<string> historyList)
        {
            if (string.IsNullOrWhiteSpace(newPath)) return;
            if (historyList.Contains(newPath))
            {
                historyList.Remove(newPath);
            }
            historyList.Insert(0, newPath);
            while (historyList.Count > 5)
            {
                historyList.RemoveAt(historyList.Count - 1);
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath));
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(this, options);
                File.WriteAllText(_configFilePath, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private void Load()
        {
            if (!File.Exists(_configFilePath)) return;

            try
            {
                string jsonString = File.ReadAllText(_configFilePath);
                var loadedSettings = JsonSerializer.Deserialize<SettingsManager>(jsonString);

                if (loadedSettings != null)
                {
                    CurrentCreoPath = loadedSettings.CurrentCreoPath;
                    CurrentProDirectory = loadedSettings.CurrentProDirectory;
                    CurrentProCommMsgExe = loadedSettings.CurrentProCommMsgExe;
                    CurrentVbApiRegisterBatPath = loadedSettings.CurrentVbApiRegisterBatPath;
                    CurrentCommonFilesFolder = loadedSettings.CurrentCommonFilesFolder;
                    WorkingDirectory = loadedSettings.WorkingDirectory;

                    CreoPathHistory = new ObservableCollection<string>(loadedSettings.CreoPathHistory);
                    ProDirectoryHistory = new ObservableCollection<string>(loadedSettings.ProDirectoryHistory);
                    ProCommMsgExeHistory = new ObservableCollection<string>(loadedSettings.ProCommMsgExeHistory);
                    VbApiRegisterBatPathHistory = new ObservableCollection<string>(loadedSettings.VbApiRegisterBatPathHistory);
                    CommonFilesFolderHistory = new ObservableCollection<string>(loadedSettings.CommonFilesFolderHistory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }
    }
}