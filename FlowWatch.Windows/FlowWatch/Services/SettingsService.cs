using System;
using System.IO;
using System.Text.Json;
using FlowWatch.Models;

namespace FlowWatch.Services
{
    public class SettingsService
    {
        private static readonly Lazy<SettingsService> _instance = new Lazy<SettingsService>(() => new SettingsService());
        public static SettingsService Instance => _instance.Value;

        private readonly string _settingsDir;
        private readonly string _settingsPath;
        private AppSettings _settings;

        public AppSettings Settings => _settings;

        public event Action SettingsChanged;

        private SettingsService()
        {
            _settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowWatch");
            _settingsPath = Path.Combine(_settingsDir, "settings.json");
            Load();
        }

        private void Load()
        {
            bool loadedFromFile = false;
            bool hasShowNetworkSpeed = false;
            bool hasShowTodayUsage = false;

            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    loadedFromFile = true;
                    using (var doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            hasShowNetworkSpeed = doc.RootElement.TryGetProperty(nameof(AppSettings.ShowNetworkSpeed), out _);
                            hasShowTodayUsage = doc.RootElement.TryGetProperty(nameof(AppSettings.ShowTodayUsage), out _);
                        }
                    }

                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }

            if (NormalizeSettings(loadedFromFile && !hasShowNetworkSpeed && !hasShowTodayUsage))
                Save();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(_settingsDir))
                    Directory.CreateDirectory(_settingsDir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);

                // Atomic write: write to temp, then replace
                var tempPath = _settingsPath + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(_settingsPath))
                {
                    var backupPath = _settingsPath + ".bak";
                    File.Replace(tempPath, _settingsPath, backupPath);
                }
                else
                {
                    File.Move(tempPath, _settingsPath);
                }
            }
            catch
            {
                // Silently fail - settings will be lost on crash but app continues
            }
        }

        public void Update(Action<AppSettings> modifier)
        {
            modifier(_settings);
            NormalizeSettings(contentFieldsMissing: false);
            Save();
            SettingsChanged?.Invoke();
        }

        private bool NormalizeSettings(bool contentFieldsMissing)
        {
            if (_settings == null)
            {
                _settings = new AppSettings();
                return true;
            }

            bool changed = false;

            switch (_settings.DisplayMode)
            {
                case "speed":
                    _settings.DisplayMode = "standard";
                    _settings.ShowNetworkSpeed = true;
                    _settings.ShowTodayUsage = false;
                    changed = true;
                    break;
                case "usage":
                    _settings.DisplayMode = "standard";
                    _settings.ShowNetworkSpeed = false;
                    _settings.ShowTodayUsage = true;
                    changed = true;
                    break;
                case "both":
                    _settings.DisplayMode = "standard";
                    _settings.ShowNetworkSpeed = true;
                    _settings.ShowTodayUsage = true;
                    changed = true;
                    break;
                case "minimal":
                case "spiral":
                    if (contentFieldsMissing)
                    {
                        _settings.ShowNetworkSpeed = true;
                        _settings.ShowTodayUsage = true;
                        changed = true;
                    }
                    break;
                case "standard":
                    break;
                default:
                    _settings.DisplayMode = "standard";
                    changed = true;
                    break;
            }

            if (!_settings.ShowNetworkSpeed && !_settings.ShowTodayUsage)
            {
                _settings.ShowNetworkSpeed = true;
                changed = true;
            }

            return changed;
        }
    }
}
