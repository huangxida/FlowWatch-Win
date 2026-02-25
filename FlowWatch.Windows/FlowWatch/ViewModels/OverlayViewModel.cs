using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FlowWatch.Helpers;
using FlowWatch.Models;
using FlowWatch.Services;

namespace FlowWatch.ViewModels
{
    public class OverlayViewModel : ViewModelBase
    {
        private string _uploadNum = "0";
        private string _uploadUnit = "KB/s";
        private string _downloadNum = "0";
        private string _downloadUnit = "KB/s";
        private string _uploadUsageNum = "0";
        private string _uploadUsageUnit = "KB";
        private string _downloadUsageNum = "0";
        private string _downloadUsageUnit = "KB";
        private Brush _uploadColor = Brushes.White;
        private Brush _downloadColor = Brushes.White;
        private Brush _uploadLabelColor;
        private Brush _downloadLabelColor;
        private FontFamily _fontFamily = new FontFamily("Segoe UI, Microsoft YaHei");
        private double _fontSize = 18;
        private bool _isVertical;
        private bool _isLocked = true;
        private string _displayMode = "speed";
        private Visibility _secondaryVisibility = Visibility.Collapsed;

        // Smooth transition animation state
        private double _displayedDownSpeed, _displayedUpSpeed;
        private double _startDownSpeed, _startUpSpeed;
        private double _targetDownSpeed, _targetUpSpeed;
        private double _displayedTotalDown, _displayedTotalUp;
        private double _startTotalDown, _startTotalUp;
        private double _targetTotalDown, _targetTotalUp;
        private DispatcherTimer _animationTimer;
        private long _animationStartTick;
        private int _animationDurationMs = 1000;
        private string _lastRenderedKey;
        private string _targetRenderKey;
        private bool _smoothTransition = true;
        private bool _totalsInitialized;
        private long _lastDownColorQ = -1;
        private long _lastUpColorQ = -1;

        private static readonly Brush DefaultUpLabelColor = new SolidColorBrush(Color.FromRgb(0x9E, 0xF6, 0xC5));
        private static readonly Brush DefaultDownLabelColor = new SolidColorBrush(Color.FromRgb(0xFF, 0xDA, 0x89));

        static OverlayViewModel()
        {
            ((SolidColorBrush)DefaultUpLabelColor).Freeze();
            ((SolidColorBrush)DefaultDownLabelColor).Freeze();
        }

        public OverlayViewModel()
        {
            _uploadLabelColor = DefaultUpLabelColor;
            _downloadLabelColor = DefaultDownLabelColor;

            NetworkMonitorService.Instance.StatsUpdated += OnStatsUpdated;
            SettingsService.Instance.SettingsChanged += OnSettingsChanged;
            ApplySettings();
        }

        public string UploadNum
        {
            get => _uploadNum;
            set => SetProperty(ref _uploadNum, value);
        }

        public string UploadUnit
        {
            get => _uploadUnit;
            set => SetProperty(ref _uploadUnit, value);
        }

        public string DownloadNum
        {
            get => _downloadNum;
            set => SetProperty(ref _downloadNum, value);
        }

        public string DownloadUnit
        {
            get => _downloadUnit;
            set => SetProperty(ref _downloadUnit, value);
        }

        public string UploadUsageNum
        {
            get => _uploadUsageNum;
            set => SetProperty(ref _uploadUsageNum, value);
        }

        public string UploadUsageUnit
        {
            get => _uploadUsageUnit;
            set => SetProperty(ref _uploadUsageUnit, value);
        }

        public string DownloadUsageNum
        {
            get => _downloadUsageNum;
            set => SetProperty(ref _downloadUsageNum, value);
        }

        public string DownloadUsageUnit
        {
            get => _downloadUsageUnit;
            set => SetProperty(ref _downloadUsageUnit, value);
        }

        public Brush UploadColor
        {
            get => _uploadColor;
            set => SetProperty(ref _uploadColor, value);
        }

        public Brush DownloadColor
        {
            get => _downloadColor;
            set => SetProperty(ref _downloadColor, value);
        }

        public Brush UploadLabelColor
        {
            get => _uploadLabelColor;
            set => SetProperty(ref _uploadLabelColor, value);
        }

        public Brush DownloadLabelColor
        {
            get => _downloadLabelColor;
            set => SetProperty(ref _downloadLabelColor, value);
        }

        public FontFamily FontFamily
        {
            get => _fontFamily;
            set => SetProperty(ref _fontFamily, value);
        }

        public double FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public bool IsVertical
        {
            get => _isVertical;
            set => SetProperty(ref _isVertical, value);
        }

        public bool IsLocked
        {
            get => _isLocked;
            set => SetProperty(ref _isLocked, value);
        }

        public string DisplayMode
        {
            get => _displayMode;
            set
            {
                if (SetProperty(ref _displayMode, value))
                {
                    SecondaryVisibility = value == "both" ? Visibility.Visible : Visibility.Collapsed;
                    OnPropertyChanged(nameof(ShowSpeed));
                    OnPropertyChanged(nameof(ShowUsage));
                }
            }
        }

        public Visibility SecondaryVisibility
        {
            get => _secondaryVisibility;
            set => SetProperty(ref _secondaryVisibility, value);
        }

        public bool ShowSpeed => _displayMode != "usage";
        public bool ShowUsage => _displayMode == "usage";

        private void OnStatsUpdated(NetworkStats stats)
        {
            if (!_totalsInitialized)
            {
                _displayedTotalDown = stats.TotalDownload;
                _displayedTotalUp = stats.TotalUpload;
                _targetTotalDown = stats.TotalDownload;
                _targetTotalUp = stats.TotalUpload;
                _totalsInitialized = true;
            }

            if (_smoothTransition)
            {
                _targetDownSpeed = stats.DownloadSpeed;
                _targetUpSpeed = stats.UploadSpeed;
                _targetTotalDown = stats.TotalDownload;
                _targetTotalUp = stats.TotalUpload;
                StartAnimation();
            }
            else
            {
                _displayedDownSpeed = stats.DownloadSpeed;
                _displayedUpSpeed = stats.UploadSpeed;
                _displayedTotalDown = stats.TotalDownload;
                _displayedTotalUp = stats.TotalUpload;
                UpdateDisplay(stats.DownloadSpeed, stats.UploadSpeed, stats.TotalDownload, stats.TotalUpload);
            }
        }

        private void StartAnimation()
        {
            _startDownSpeed = _displayedDownSpeed;
            _startUpSpeed = _displayedUpSpeed;
            _startTotalDown = _displayedTotalDown;
            _startTotalUp = _displayedTotalUp;

            if (_startDownSpeed == _targetDownSpeed && _startUpSpeed == _targetUpSpeed
                && _startTotalDown == _targetTotalDown && _startTotalUp == _targetTotalUp)
            {
                return;
            }

            _animationStartTick = Stopwatch.GetTimestamp();
            _targetRenderKey = BuildRenderKey(_targetDownSpeed, _targetUpSpeed, (long)_targetTotalDown, (long)_targetTotalUp);

            if (_animationTimer == null)
            {
                _animationTimer = new DispatcherTimer();
                _animationTimer.Interval = TimeSpan.FromMilliseconds(100);
                _animationTimer.Tick += (s, e) => AnimationTick();
            }

            if (!_animationTimer.IsEnabled)
                _animationTimer.Start();
        }

        private void AnimationTick()
        {
            long now = Stopwatch.GetTimestamp();
            double elapsedMs = (now - _animationStartTick) * 1000.0 / Stopwatch.Frequency;
            double progress = Math.Min(elapsedMs / _animationDurationMs, 1.0);
            double eased = 1.0 - Math.Pow(1.0 - progress, 3);

            _displayedDownSpeed = _startDownSpeed + (_targetDownSpeed - _startDownSpeed) * eased;
            _displayedUpSpeed = _startUpSpeed + (_targetUpSpeed - _startUpSpeed) * eased;
            _displayedTotalDown = _startTotalDown + (_targetTotalDown - _startTotalDown) * eased;
            _displayedTotalUp = _startTotalUp + (_targetTotalUp - _startTotalUp) * eased;

            UpdateDisplay(_displayedDownSpeed, _displayedUpSpeed, (long)_displayedTotalDown, (long)_displayedTotalUp);

            if (progress >= 1.0 || _lastRenderedKey == _targetRenderKey)
            {
                _animationTimer.Stop();
            }
        }

        private static string BuildRenderKey(
            string mode,
            string downNum, string downUnit, string upNum, string upUnit,
            string downUsageNum, string downUsageUnit, string upUsageNum, string upUsageUnit,
            long downColorQ, long upColorQ)
        {
            return $"{mode}|{downNum}|{downUnit}|{upNum}|{upUnit}|{downUsageNum}|{downUsageUnit}|{upUsageNum}|{upUsageUnit}|{downColorQ}|{upColorQ}";
        }

        private string BuildRenderKey(double downSpeed, double upSpeed, long totalDown, long totalUp)
        {
            var (downNum, downUnit) = FormatHelper.FormatSpeed(downSpeed);
            var (upNum, upUnit) = FormatHelper.FormatSpeed(upSpeed);
            var (downUsageNum, downUsageUnit) = FormatHelper.FormatUsage(totalDown);
            var (upUsageNum, upUsageUnit) = FormatHelper.FormatUsage(totalUp);

            const double colorStep = 262144.0;
            long downColorQ = (long)(downSpeed / colorStep);
            long upColorQ = (long)(upSpeed / colorStep);

            return BuildRenderKey(_displayMode, downNum, downUnit, upNum, upUnit, downUsageNum, downUsageUnit, upUsageNum, upUsageUnit, downColorQ, upColorQ);
        }

        private void UpdateDisplay(double downSpeed, double upSpeed, long totalDown, long totalUp)
        {
            var settings = SettingsService.Instance.Settings;
            int maxMbps = settings.SpeedColorMaxMbps;

            var (downNum, downUnit) = FormatHelper.FormatSpeed(downSpeed);
            var (upNum, upUnit) = FormatHelper.FormatSpeed(upSpeed);
            var (downUsageNum, downUsageUnit) = FormatHelper.FormatUsage(totalDown);
            var (upUsageNum, upUsageUnit) = FormatHelper.FormatUsage(totalUp);

            const double colorStep = 262144.0;
            long downColorQ = (long)(downSpeed / colorStep);
            long upColorQ = (long)(upSpeed / colorStep);

            string key = BuildRenderKey(_displayMode, downNum, downUnit, upNum, upUnit, downUsageNum, downUsageUnit, upUsageNum, upUsageUnit, downColorQ, upColorQ);

            if (key == _lastRenderedKey)
                return;
            _lastRenderedKey = key;

            if (_displayMode == "usage")
            {
                UploadNum = upUsageNum;
                UploadUnit = upUsageUnit;
                DownloadNum = downUsageNum;
                DownloadUnit = downUsageUnit;
            }
            else
            {
                UploadNum = upNum;
                UploadUnit = upUnit;
                DownloadNum = downNum;
                DownloadUnit = downUnit;
            }

            UploadUsageNum = upUsageNum;
            UploadUsageUnit = upUsageUnit;
            DownloadUsageNum = downUsageNum;
            DownloadUsageUnit = downUsageUnit;

            if (downColorQ != _lastDownColorQ)
            {
                _lastDownColorQ = downColorQ;
                var downBrush = ColorGradient.GetSpeedBrush(downSpeed, maxMbps);
                DownloadColor = downBrush;
                DownloadLabelColor = downBrush;
            }

            if (upColorQ != _lastUpColorQ)
            {
                _lastUpColorQ = upColorQ;
                var upBrush = ColorGradient.GetSpeedBrush(upSpeed, maxMbps);
                UploadColor = upBrush;
                UploadLabelColor = upBrush;
            }
        }

        private void OnSettingsChanged()
        {
            ApplySettings();
        }

        private void ApplySettings()
        {
            var s = SettingsService.Instance.Settings;

            var fontName = s.FontFamily?.Split(',')[0]?.Trim() ?? "Segoe UI";
            FontFamily = new FontFamily(fontName);
            FontSize = Math.Max(11, Math.Min(19, s.FontSize));
            IsVertical = s.Layout == "vertical";
            IsLocked = s.LockOnTop;
            DisplayMode = s.DisplayMode ?? "speed";

            _smoothTransition = s.SmoothTransition;
            // Animation completes exactly as the next sample arrives
            _animationDurationMs = s.RefreshInterval;

            if (!_smoothTransition)
            {
                _animationTimer?.Stop();
                _displayedDownSpeed = _targetDownSpeed;
                _displayedUpSpeed = _targetUpSpeed;
                _displayedTotalDown = _targetTotalDown;
                _displayedTotalUp = _targetTotalUp;
                UpdateDisplay(_displayedDownSpeed, _displayedUpSpeed, (long)_displayedTotalDown, (long)_displayedTotalUp);
            }
        }

        public void Cleanup()
        {
            _animationTimer?.Stop();
            NetworkMonitorService.Instance.StatsUpdated -= OnStatsUpdated;
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
        }
    }
}
