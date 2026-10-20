using System;
using System.IO;
using System.Text.Json;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class LauncherSettingsService
    {
        private readonly string _settingsFilePath;
        private LauncherSettings _settings;

        public LauncherSettings Settings => _settings;

        public LauncherSettingsService(string? customFilePath = null)
        {
            if (!string.IsNullOrWhiteSpace(customFilePath))
            {
                _settingsFilePath = customFilePath;
            }
            else
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData))
                {
                    localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                }
                string configDir = Path.Combine(localAppData, "ZenithMCServer");
                Directory.CreateDirectory(configDir);
                _settingsFilePath = Path.Combine(configDir, "settings.json");
            }

            _settings = LoadSettings();
        }

        public LauncherSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<LauncherSettings>(json);
                    if (loaded != null)
                    {
                        EnsureDefaultPaths(loaded);
                        return loaded;
                    }
                }
            }
            catch
            {
                // Fallback on corrupt settings file
            }

            var defaultSettings = new LauncherSettings();
            EnsureDefaultPaths(defaultSettings);
            SaveSettings(defaultSettings);
            return defaultSettings;
        }

        private void EnsureDefaultPaths(LauncherSettings settings)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
            {
                localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            if (string.IsNullOrWhiteSpace(settings.DefaultServersDirectory))
            {
                settings.DefaultServersDirectory = Path.Combine(localAppData, "ZenithMCServer", "servers");
            }

            if (string.IsNullOrWhiteSpace(settings.DefaultJavaRuntimesDirectory))
            {
                settings.DefaultJavaRuntimesDirectory = Path.Combine(localAppData, "ZenithMCServer", "runtimes", "java");
            }

            Directory.CreateDirectory(settings.DefaultServersDirectory);
            Directory.CreateDirectory(settings.DefaultJavaRuntimesDirectory);
        }

        public void SaveSettings(LauncherSettings? settings = null)
        {
            if (settings != null)
            {
                _settings = settings;
            }

            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // File write exception handling
            }
        }
    }
}
