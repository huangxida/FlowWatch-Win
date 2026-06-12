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
        private const string MinimalDisplayMode = "minimal";
        private const double SignalBreathMinDelta = 16 * 1024.0;
        private const double SignalEnergyEpsilon = 0.004;

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
        private Visibility _standardVisibility = Visibility.Visible;
        private Visibility _minimalVisibility = Visibility.Collapsed;
        private Brush _uploadSignalColor = Brushes.White;
        private Brush _downloadSignalColor = Brushes.White;
        private double _uploadSignalOpacity = 1.0;
        private double _downloadSignalOpacity = 1.0;

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
        private bool _signalRenderingSubscribed;
        private readonly SignalBreathState _downloadBreath = new SignalBreathState();
        private readonly SignalBreathState _uploadBreath = new SignalBreathState();

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
            set
            {
                if (SetProperty(ref _fontSize, value))
                    OnPropertyChanged(nameof(MinimalFontSize));
            }
        }

        public double MinimalFontSize => Math.Max(10, FontSize - 1);

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
                var mode = NormalizeDisplayMode(value);
                if (SetProperty(ref _displayMode, mode))
                {
                    var isMinimal = mode == MinimalDisplayMode;
                    SecondaryVisibility = mode == "both" ? Visibility.Visible : Visibility.Collapsed;
                    StandardVisibility = isMinimal ? Visibility.Collapsed : Visibility.Visible;
                    MinimalVisibility = isMinimal ? Visibility.Visible : Visibility.Collapsed;
                    if (!isMinimal)
                        ResetSignalBreathing();
                    OnPropertyChanged(nameof(ShowSpeed));
                    OnPropertyChanged(nameof(ShowUsage));
                    OnPropertyChanged(nameof(IsMinimalMode));
                }
            }
        }

        public Visibility SecondaryVisibility
        {
            get => _secondaryVisibility;
            set => SetProperty(ref _secondaryVisibility, value);
        }

        public Visibility StandardVisibility
        {
            get => _standardVisibility;
            set => SetProperty(ref _standardVisibility, value);
        }

        public Visibility MinimalVisibility
        {
            get => _minimalVisibility;
            set => SetProperty(ref _minimalVisibility, value);
        }

        public Brush UploadSignalColor
        {
            get => _uploadSignalColor;
            set => SetProperty(ref _uploadSignalColor, value);
        }

        public Brush DownloadSignalColor
        {
            get => _downloadSignalColor;
            set => SetProperty(ref _downloadSignalColor, value);
        }

        public double UploadSignalOpacity
        {
            get => _uploadSignalOpacity;
            set => SetProperty(ref _uploadSignalOpacity, value);
        }

        public double DownloadSignalOpacity
        {
            get => _downloadSignalOpacity;
            set => SetProperty(ref _downloadSignalOpacity, value);
        }

        public bool ShowSpeed => _displayMode != "usage" && _displayMode != MinimalDisplayMode;
        public bool ShowUsage => _displayMode == "usage";
        public bool IsMinimalMode => _displayMode == MinimalDisplayMode;

        private void OnStatsUpdated(NetworkStats stats)
        {
            UpdateSignalBreathing(stats.DownloadSpeed, stats.UploadSpeed);

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
                DownloadSignalColor = downBrush;
            }

            if (upColorQ != _lastUpColorQ)
            {
                _lastUpColorQ = upColorQ;
                var upBrush = ColorGradient.GetSpeedBrush(upSpeed, maxMbps);
                UploadColor = upBrush;
                UploadLabelColor = upBrush;
                UploadSignalColor = upBrush;
            }
        }

        private void UpdateSignalBreathing(double downSpeed, double upSpeed)
        {
            UpdateSignalTarget(_downloadBreath, downSpeed);
            UpdateSignalTarget(_uploadBreath, upSpeed);

            if (IsMinimalMode && HasActiveBreathing())
                EnsureSignalRendering();
        }

        private void UpdateSignalTarget(SignalBreathState state, double speed)
        {
            if (!state.RawSpeedInitialized)
            {
                state.LastRawSpeed = speed;
                state.RawSpeedInitialized = true;
                return;
            }

            double speedDelta = Math.Abs(speed - state.LastRawSpeed);
            state.LastRawSpeed = speed;

            if (!IsMinimalMode || speedDelta < SignalBreathMinDelta)
                return;

            var settings = SettingsService.Instance.Settings;
            double maxBytesPerSecond = Math.Max(1, settings.SpeedColorMaxMbps) * 1_000_000.0 / 8.0;
            double scale = Math.Max(128 * 1024.0, maxBytesPerSecond / 8.0);
            double normalized = Math.Min(1.0, Math.Max(0.0, speedDelta / scale));
            double energy = 0.35 + 0.65 * normalized;
            state.TargetEnergy = Math.Max(state.TargetEnergy, energy);
        }

        private void EnsureSignalRendering()
        {
            if (_signalRenderingSubscribed)
                return;

            _downloadBreath.LastRenderTick = 0;
            _uploadBreath.LastRenderTick = 0;
            CompositionTarget.Rendering += OnSignalRendering;
            _signalRenderingSubscribed = true;
        }

        private void StopSignalRendering()
        {
            if (!_signalRenderingSubscribed)
                return;

            CompositionTarget.Rendering -= OnSignalRendering;
            _signalRenderingSubscribed = false;
        }

        private void OnSignalRendering(object sender, EventArgs e)
        {
            bool downloadActive = UpdateSignalFrame(
                _downloadBreath,
                value => DownloadSignalOpacity = value);
            bool uploadActive = UpdateSignalFrame(
                _uploadBreath,
                value => UploadSignalOpacity = value);

            if (!IsMinimalMode || (!downloadActive && !uploadActive))
                StopSignalRendering();
        }

        private bool UpdateSignalFrame(SignalBreathState state, Action<double> setOpacity)
        {
            long now = Stopwatch.GetTimestamp();
            if (state.LastRenderTick == 0)
            {
                state.LastRenderTick = now;
                return IsBreathingActive(state);
            }

            double elapsedSeconds = (now - state.LastRenderTick) / (double)Stopwatch.Frequency;
            state.LastRenderTick = now;
            elapsedSeconds = Math.Max(0.0, Math.Min(0.08, elapsedSeconds));

            state.TargetEnergy *= Math.Exp(-elapsedSeconds / 0.7);
            if (state.TargetEnergy < SignalEnergyEpsilon)
                state.TargetEnergy = 0;

            double responseSeconds = state.TargetEnergy > state.CurrentEnergy ? 0.12 : 0.55;
            double alpha = 1.0 - Math.Exp(-elapsedSeconds / responseSeconds);
            state.CurrentEnergy += (state.TargetEnergy - state.CurrentEnergy) * alpha;
            if (state.CurrentEnergy < SignalEnergyEpsilon && state.TargetEnergy <= 0)
                state.CurrentEnergy = 0;

            if (state.CurrentEnergy <= 0)
            {
                setOpacity(1.0);
                return false;
            }

            double frequencyHz = 0.85 + 1.45 * state.CurrentEnergy;
            state.Phase = (state.Phase + elapsedSeconds * frequencyHz * Math.PI * 2.0) % (Math.PI * 2.0);

            double breath = (1.0 - Math.Cos(state.Phase)) / 2.0;
            double opacity = 1.0 - breath;

            setOpacity(Math.Max(0.0, Math.Min(1.0, opacity)));
            return true;
        }

        private bool HasActiveBreathing()
        {
            return IsBreathingActive(_downloadBreath) || IsBreathingActive(_uploadBreath);
        }

        private static bool IsBreathingActive(SignalBreathState state)
        {
            return state.TargetEnergy > SignalEnergyEpsilon || state.CurrentEnergy > SignalEnergyEpsilon;
        }

        private void ResetSignalBreathing()
        {
            StopSignalRendering();
            ResetSignalBreathVisual(_downloadBreath);
            ResetSignalBreathVisual(_uploadBreath);
            DownloadSignalOpacity = 1.0;
            UploadSignalOpacity = 1.0;
        }

        private static void ResetSignalBreathVisual(SignalBreathState state)
        {
            state.CurrentEnergy = 0;
            state.TargetEnergy = 0;
            state.LastRenderTick = 0;
            state.Phase = 0;
        }

        private void OnSettingsChanged()
        {
            ApplySettings();
        }

        private void ApplySettings()
        {
            var s = SettingsService.Instance.Settings;

            FontFamily = FontHelper.ResolveFontFamily(s.FontFamily, "overlay");
            FontSize = Math.Max(11, Math.Min(19, s.FontSize));
            IsVertical = s.Layout == "vertical";
            IsLocked = s.LockOnTop;
            DisplayMode = s.DisplayMode;

            _smoothTransition = s.SmoothTransition;
            // Animation completes exactly as the next sample arrives
            _animationDurationMs = s.RefreshInterval;
            _lastRenderedKey = null;
            _lastDownColorQ = -1;
            _lastUpColorQ = -1;

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

        private static string NormalizeDisplayMode(string value)
        {
            switch (value)
            {
                case "speed":
                case "usage":
                case "both":
                case MinimalDisplayMode:
                    return value;
                default:
                    return "speed";
            }
        }

        public void Cleanup()
        {
            _animationTimer?.Stop();
            StopSignalRendering();
            NetworkMonitorService.Instance.StatsUpdated -= OnStatsUpdated;
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
        }

        private sealed class SignalBreathState
        {
            public bool RawSpeedInitialized { get; set; }
            public double LastRawSpeed { get; set; }
            public double CurrentEnergy { get; set; }
            public double TargetEnergy { get; set; }
            public double Phase { get; set; }
            public long LastRenderTick { get; set; }
        }
    }
}
