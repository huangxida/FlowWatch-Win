using System;
using System.Windows;
using System.Windows.Input;
using FlowWatch.Services;

namespace FlowWatch.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private int _refreshIntervalSeconds = 1;
        private bool _lockOnTop = true;
        private bool _pinToDesktop;
        private bool _autoLaunch = true;
        private bool _autoCheckUpdate = true;
        private string _language = "auto";
        private string _layout = "horizontal";
        private string _displayMode = "speed";
        private string _fontFamily = "Segoe UI, Microsoft YaHei, sans-serif";
        private int _fontSize = 18;
        private int _speedColorMaxMbps = 100;
        private bool _suppressPush;
        private string _skippedVersion;
        private bool _hasSkippedVersion;
        private bool _isCheckingUpdate;

        public SettingsViewModel()
        {
            ResetTodayCommand = new RelayCommand(OnResetToday);
            ResetAllCommand = new RelayCommand(OnResetAll);
            CheckUpdateCommand = new RelayCommand(OnCheckUpdate, () => !_isCheckingUpdate);
            ClearSkippedCommand = new RelayCommand(OnClearSkipped);
            SettingsService.Instance.SettingsChanged += OnSettingsChanged;
            LoadFromSettings();
        }

        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set
            {
                if (SetProperty(ref _refreshIntervalSeconds, Math.Max(1, Math.Min(10, value))))
                    PushSettings();
            }
        }

        public bool LockOnTop
        {
            get => _lockOnTop;
            set
            {
                if (SetProperty(ref _lockOnTop, value))
                {
                    if (value && _pinToDesktop)
                    {
                        _pinToDesktop = false;
                        OnPropertyChanged(nameof(PinToDesktop));
                    }
                    PushSettings();
                }
            }
        }

        public bool PinToDesktop
        {
            get => _pinToDesktop;
            set
            {
                if (SetProperty(ref _pinToDesktop, value))
                {
                    if (value && _lockOnTop)
                    {
                        _lockOnTop = false;
                        OnPropertyChanged(nameof(LockOnTop));
                    }
                    PushSettings();
                }
            }
        }

        public bool AutoLaunch
        {
            get => _autoLaunch;
            set
            {
                if (SetProperty(ref _autoLaunch, value))
                    PushSettings();
            }
        }

        public bool AutoCheckUpdate
        {
            get => _autoCheckUpdate;
            set
            {
                if (SetProperty(ref _autoCheckUpdate, value))
                {
                    PushSettings();
                    if (!_suppressPush)
                    {
                        if (value)
                            UpdateService.Instance.StartAutoCheck();
                        else
                            UpdateService.Instance.StopAutoCheck();
                    }
                }
            }
        }

        public string Language
        {
            get => _language;
            set
            {
                if (SetProperty(ref _language, value))
                {
                    if (!_suppressPush)
                        LocalizationService.Instance.ApplyLanguage(value);
                    PushSettings();
                }
            }
        }

        public string Layout
        {
            get => _layout;
            set
            {
                if (SetProperty(ref _layout, value))
                    PushSettings();
            }
        }

        public string DisplayMode
        {
            get => _displayMode;
            set
            {
                if (SetProperty(ref _displayMode, value))
                    PushSettings();
            }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set
            {
                if (SetProperty(ref _fontFamily, value))
                    PushSettings();
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                if (SetProperty(ref _fontSize, Math.Max(11, Math.Min(19, value))))
                    PushSettings();
            }
        }

        public int SpeedColorMaxMbps
        {
            get => _speedColorMaxMbps;
            set
            {
                if (SetProperty(ref _speedColorMaxMbps, Math.Max(1, Math.Min(1000, value))))
                    PushSettings();
            }
        }

        public string CurrentVersion => UpdateService.Instance.GetCurrentVersion().ToString(3);

        public string SkippedVersion
        {
            get => _skippedVersion;
            private set => SetProperty(ref _skippedVersion, value);
        }

        public bool HasSkippedVersion
        {
            get => _hasSkippedVersion;
            private set => SetProperty(ref _hasSkippedVersion, value);
        }

        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            private set => SetProperty(ref _isCheckingUpdate, value);
        }

        public ICommand CheckUpdateCommand { get; }
        public ICommand ClearSkippedCommand { get; }
        public ICommand ResetTodayCommand { get; }
        public ICommand ResetAllCommand { get; }

        private void LoadFromSettings()
        {
            _suppressPush = true;
            var s = SettingsService.Instance.Settings;
            RefreshIntervalSeconds = Math.Max(1, Math.Min(10, s.RefreshInterval / 1000));
            LockOnTop = s.LockOnTop;
            PinToDesktop = s.PinToDesktop;
            AutoLaunch = s.AutoLaunch;
            AutoCheckUpdate = s.AutoCheckUpdate;
            Language = s.Language ?? "auto";
            Layout = s.Layout ?? "horizontal";
            DisplayMode = s.DisplayMode ?? "speed";
            FontFamily = s.FontFamily ?? "Segoe UI, Microsoft YaHei, sans-serif";
            FontSize = s.FontSize;
            SpeedColorMaxMbps = s.SpeedColorMaxMbps;
            UpdateSkippedVersion(s.SkippedVersion);
            _suppressPush = false;
        }

        private void PushSettings()
        {
            if (_suppressPush) return;

            var svc = SettingsService.Instance;
            var prevInterval = svc.Settings.RefreshInterval;

            svc.Update(s =>
            {
                s.RefreshInterval = _refreshIntervalSeconds * 1000;
                s.LockOnTop = _lockOnTop;
                s.PinToDesktop = _pinToDesktop;
                s.AutoLaunch = _autoLaunch;
                s.AutoCheckUpdate = _autoCheckUpdate;
                s.Language = _language;
                s.Layout = _layout;
                s.DisplayMode = _displayMode;
                s.FontFamily = _fontFamily;
                s.FontSize = _fontSize;
                s.SpeedColorMaxMbps = _speedColorMaxMbps;
            });

            AutoLaunchService.SetAutoLaunch(_autoLaunch);

            if (svc.Settings.RefreshInterval != prevInterval)
            {
                NetworkMonitorService.Instance.Restart(svc.Settings.RefreshInterval);
            }
        }

        private void OnSettingsChanged()
        {
            _suppressPush = true;
            var s = SettingsService.Instance.Settings;
            RefreshIntervalSeconds = Math.Max(1, Math.Min(10, s.RefreshInterval / 1000));
            LockOnTop = s.LockOnTop;
            PinToDesktop = s.PinToDesktop;
            AutoLaunch = s.AutoLaunch;
            AutoCheckUpdate = s.AutoCheckUpdate;
            Language = s.Language ?? "auto";
            Layout = s.Layout ?? "horizontal";
            DisplayMode = s.DisplayMode ?? "speed";
            FontFamily = s.FontFamily ?? "Segoe UI, Microsoft YaHei, sans-serif";
            FontSize = s.FontSize;
            SpeedColorMaxMbps = s.SpeedColorMaxMbps;
            _suppressPush = false;
        }

        public void RefreshSkippedVersion()
        {
            UpdateSkippedVersion(SettingsService.Instance.Settings.SkippedVersion);
        }

        private void UpdateSkippedVersion(string skipped)
        {
            SkippedVersion = skipped;
            HasSkippedVersion = !string.IsNullOrEmpty(skipped);
        }

        private async void OnCheckUpdate()
        {
            if (_isCheckingUpdate) return;
            IsCheckingUpdate = true;
            var loc = LocalizationService.Instance;
            try
            {
                var info = await UpdateService.Instance.CheckForUpdateAsync();
                if (info != null)
                {
                    var skipped = SettingsService.Instance.Settings.SkippedVersion;
                    if (skipped == info.TagName)
                    {
                        MessageBox.Show(
                            loc.Format("Update.AlreadyLatest", CurrentVersion),
                            "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var app = Application.Current as App;
                        app?.ShowUpdateWindowFromSettings(info);
                    }
                }
                else
                {
                    MessageBox.Show(
                        loc.Format("Update.AlreadyLatest", CurrentVersion),
                        "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch
            {
                MessageBox.Show(
                    loc.Get("Update.CheckFailed"),
                    "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        private void OnClearSkipped()
        {
            SettingsService.Instance.Update(s => s.SkippedVersion = null);
            UpdateSkippedVersion(null);
        }

        private void OnResetToday()
        {
            var loc = LocalizationService.Instance;
            var result = MessageBox.Show(
                loc.Get("Settings.ResetTodayConfirm"),
                "FlowWatch", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                TrafficHistoryService.Instance.ResetToday();
        }

        private void OnResetAll()
        {
            var loc = LocalizationService.Instance;
            var result = MessageBox.Show(
                loc.Get("Settings.ResetAllConfirm"),
                "FlowWatch", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                TrafficHistoryService.Instance.ResetAll();
        }

        public void Cleanup()
        {
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }
}
