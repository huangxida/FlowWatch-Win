using System;
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
        private string _layout = "horizontal";
        private string _displayMode = "speed";
        private string _fontFamily = "Segoe UI, Microsoft YaHei, sans-serif";
        private int _fontSize = 18;
        private int _speedColorMaxMbps = 100;
        private string _trafficStartTimeText = "--";
        private bool _suppressPush;

        public SettingsViewModel()
        {
            ResetTrafficCommand = new RelayCommand(OnResetTraffic);
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

        public string TrafficStartTimeText
        {
            get => _trafficStartTimeText;
            set => SetProperty(ref _trafficStartTimeText, value);
        }

        public ICommand ResetTrafficCommand { get; }

        private void LoadFromSettings()
        {
            _suppressPush = true;
            var s = SettingsService.Instance.Settings;
            RefreshIntervalSeconds = Math.Max(1, Math.Min(10, s.RefreshInterval / 1000));
            LockOnTop = s.LockOnTop;
            PinToDesktop = s.PinToDesktop;
            AutoLaunch = s.AutoLaunch;
            Layout = s.Layout ?? "horizontal";
            DisplayMode = s.DisplayMode ?? "speed";
            FontFamily = s.FontFamily ?? "Segoe UI, Microsoft YaHei, sans-serif";
            FontSize = s.FontSize;
            SpeedColorMaxMbps = s.SpeedColorMaxMbps;
            UpdateTrafficStartTime();
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
            Layout = s.Layout ?? "horizontal";
            DisplayMode = s.DisplayMode ?? "speed";
            FontFamily = s.FontFamily ?? "Segoe UI, Microsoft YaHei, sans-serif";
            FontSize = s.FontSize;
            SpeedColorMaxMbps = s.SpeedColorMaxMbps;
            UpdateTrafficStartTime();
            _suppressPush = false;
        }

        private void OnResetTraffic()
        {
            NetworkMonitorService.Instance.ResetTraffic();
            UpdateTrafficStartTime();
        }

        private void UpdateTrafficStartTime()
        {
            var dt = NetworkMonitorService.Instance.TrafficStartTime;
            TrafficStartTimeText = dt.ToString("yyyy-MM-dd HH:mm:ss");
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
