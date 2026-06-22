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
        private const string StandardDisplayMode = "standard";
        private const string MinimalDisplayMode = "minimal";
        private const string SpiralDisplayMode = "spiral";
        private const double SignalBlinkMinSpeedBytesPerSecond = 1024.0;
        private const double SignalOffOpacity = 0.0;
        private const double SignalBrightOpacity = 1.0;
        private const double SignalIntervalSmoothingSeconds = 0.22;
        private const int SignalSlowBlinkMs = 900;
        private const int SignalFastBlinkMs = 120;
        private const double SpeedColorStepBytesPerSecond = 262144.0;
        private const double SpiralMotionHoldSeconds = 2.0;
        private const double SpiralMotionDecaySeconds = 3.0;
        private const int MinRandomAnimationIntervalMinutes = 1;
        private const int MaxRandomAnimationIntervalMinutes = 60;
        private const int DefaultRandomAnimationIntervalMinutes = 5;

        private string _uploadNum = "0.0";
        private string _uploadUnit = "KB/s";
        private string _downloadNum = "0.0";
        private string _downloadUnit = "KB/s";
        private string _uploadUsageNum = "0.0";
        private string _uploadUsageUnit = "KB";
        private string _downloadUsageNum = "0.0";
        private string _downloadUsageUnit = "KB";
        private Brush _uploadColor = Brushes.White;
        private Brush _downloadColor = Brushes.White;
        private Brush _uploadLabelColor;
        private Brush _downloadLabelColor;
        private FontFamily _fontFamily = new FontFamily("Segoe UI, Microsoft YaHei");
        private double _fontSize = 18;
        private bool _overlayTextEnhancementEnabled = true;
        private bool _isVertical;
        private bool _isLocked = true;
        private string _displayMode = StandardDisplayMode;
        private bool _showNetworkSpeed = true;
        private bool _showTodayUsage;
        private string _overlayAnimationKey = MathCurveCatalog.DefaultKey;
        private string _activeOverlayAnimationKey = MathCurveCatalog.DefaultKey;
        private Visibility _secondaryVisibility = Visibility.Collapsed;
        private Visibility _standardVisibility = Visibility.Visible;
        private Visibility _minimalVisibility = Visibility.Collapsed;
        private Visibility _spiralVisibility = Visibility.Collapsed;
        private Visibility _minimalSignalVisibility = Visibility.Visible;
        private Visibility _minimalUsageVisibility = Visibility.Collapsed;
        private Visibility _spiralAnimationVisibility = Visibility.Visible;
        private Visibility _spiralUsageVisibility = Visibility.Collapsed;
        private Brush _uploadSignalColor = Brushes.White;
        private Brush _downloadSignalColor = Brushes.White;
        private double _uploadSignalOpacity = SignalBrightOpacity;
        private double _downloadSignalOpacity = SignalBrightOpacity;
        private Brush _spiralColor = Brushes.White;
        private double _spiralMotionRatio;
        private double _lastSpiralColorSpeedBytesPerSecond;
        private double _lastSpiralMotionSpeedBytesPerSecond;
        private double _heldSpiralMotionRatio;
        private long _lastSpiralMotionUpdateTick;
        private long _lastSpiralMotionPeakTick;
        private long _lastSpiralMotionLogTick;

        // Smooth transition animation state
        private double _displayedDownSpeed, _displayedUpSpeed;
        private double _startDownSpeed, _startUpSpeed;
        private double _targetDownSpeed, _targetUpSpeed;
        private double _displayedTotalDown, _displayedTotalUp;
        private double _startTotalDown, _startTotalUp;
        private double _targetTotalDown, _targetTotalUp;
        private DispatcherTimer _animationTimer;
        private DispatcherTimer _randomAnimationTimer;
        private long _animationStartTick;
        private int _animationDurationMs = 1000;
        private string _lastRenderedKey;
        private string _targetRenderKey;
        private bool _smoothTransition = true;
        private bool _totalsInitialized;
        private long _lastDownColorQ = -1;
        private long _lastUpColorQ = -1;
        private long _lastSpiralColorQ = -1;
        private int _indicatorBlinkThresholdMbps = 100;
        private int _randomAnimationIntervalMinutes = DefaultRandomAnimationIntervalMinutes;
        private bool _signalRenderingSubscribed;
        private readonly Random _random = new Random();
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
            set
            {
                if (SetProperty(ref _uploadUsageNum, value))
                    OnPropertyChanged(nameof(UploadMinimalUsageText));
            }
        }

        public string UploadUsageUnit
        {
            get => _uploadUsageUnit;
            set
            {
                if (SetProperty(ref _uploadUsageUnit, value))
                    OnPropertyChanged(nameof(UploadMinimalUsageText));
            }
        }

        public string DownloadUsageNum
        {
            get => _downloadUsageNum;
            set
            {
                if (SetProperty(ref _downloadUsageNum, value))
                    OnPropertyChanged(nameof(DownloadMinimalUsageText));
            }
        }

        public string DownloadUsageUnit
        {
            get => _downloadUsageUnit;
            set
            {
                if (SetProperty(ref _downloadUsageUnit, value))
                    OnPropertyChanged(nameof(DownloadMinimalUsageText));
            }
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

        public bool OverlayTextEnhancementEnabled
        {
            get => _overlayTextEnhancementEnabled;
            private set => SetProperty(ref _overlayTextEnhancementEnabled, value);
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
                var mode = NormalizeDisplayMode(value);
                if (SetProperty(ref _displayMode, mode))
                {
                    _lastRenderedKey = null;
                    UpdateDisplayVisibility();
                }
            }
        }

        public bool ShowNetworkSpeed
        {
            get => _showNetworkSpeed;
            private set
            {
                if (SetProperty(ref _showNetworkSpeed, value))
                {
                    _lastRenderedKey = null;
                    UpdateDisplayVisibility();
                }
            }
        }

        public bool ShowTodayUsage
        {
            get => _showTodayUsage;
            private set
            {
                if (SetProperty(ref _showTodayUsage, value))
                {
                    _lastRenderedKey = null;
                    UpdateDisplayVisibility();
                }
            }
        }

        public string OverlayAnimationKey
        {
            get => _overlayAnimationKey;
            set
            {
                if (SetProperty(ref _overlayAnimationKey, MathCurveCatalog.NormalizeKey(value)))
                    UpdateAnimationSelectionMode();
            }
        }

        public string ActiveOverlayAnimationKey
        {
            get => _activeOverlayAnimationKey;
            private set => SetProperty(ref _activeOverlayAnimationKey, MathCurveCatalog.Get(value).Key);
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

        public Visibility SpiralVisibility
        {
            get => _spiralVisibility;
            set => SetProperty(ref _spiralVisibility, value);
        }

        public Visibility MinimalSignalVisibility
        {
            get => _minimalSignalVisibility;
            set => SetProperty(ref _minimalSignalVisibility, value);
        }

        public Visibility MinimalUsageVisibility
        {
            get => _minimalUsageVisibility;
            set => SetProperty(ref _minimalUsageVisibility, value);
        }

        public Visibility SpiralAnimationVisibility
        {
            get => _spiralAnimationVisibility;
            set => SetProperty(ref _spiralAnimationVisibility, value);
        }

        public Visibility SpiralUsageVisibility
        {
            get => _spiralUsageVisibility;
            set => SetProperty(ref _spiralUsageVisibility, value);
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

        public Brush SpiralColor
        {
            get => _spiralColor;
            set => SetProperty(ref _spiralColor, value);
        }

        public double SpiralMotionRatio
        {
            get => _spiralMotionRatio;
            set => SetProperty(ref _spiralMotionRatio, Math.Min(1.0, Math.Max(0.0, value)));
        }

        public bool ShowSpeed => _showNetworkSpeed;
        public bool ShowUsage => _showTodayUsage;
        public bool IsMinimalMode => _displayMode == MinimalDisplayMode;
        public bool IsSpiralMode => _displayMode == SpiralDisplayMode;
        public string UploadMinimalUsageText => FormatMinimalUsageText(_uploadUsageNum, _uploadUsageUnit);
        public string DownloadMinimalUsageText => FormatMinimalUsageText(_downloadUsageNum, _downloadUsageUnit);
        private bool ShouldShowNetworkSpeed => _showNetworkSpeed || !_showTodayUsage;
        private bool ShouldShowTodayUsage => _showTodayUsage;
        private bool IsMinimalSignalActive => IsMinimalMode && ShouldShowNetworkSpeed;
        private bool IsSpiralAnimationActive => IsSpiralMode && ShouldShowNetworkSpeed;

        private static string FormatMinimalUsageText(string num, string unit)
        {
            return $"{num,4} {unit}";
        }

        private void UpdateDisplayVisibility()
        {
            var isStandard = _displayMode == StandardDisplayMode;
            var isMinimal = IsMinimalMode;
            var isSpiral = IsSpiralMode;
            var showSpeed = ShouldShowNetworkSpeed;
            var showUsage = ShouldShowTodayUsage;

            StandardVisibility = isStandard ? Visibility.Visible : Visibility.Collapsed;
            MinimalVisibility = isMinimal ? Visibility.Visible : Visibility.Collapsed;
            SpiralVisibility = isSpiral ? Visibility.Visible : Visibility.Collapsed;
            SecondaryVisibility = isStandard && showSpeed && showUsage ? Visibility.Visible : Visibility.Collapsed;
            MinimalSignalVisibility = isMinimal && showSpeed ? Visibility.Visible : Visibility.Collapsed;
            MinimalUsageVisibility = isMinimal && showUsage ? Visibility.Visible : Visibility.Collapsed;
            SpiralAnimationVisibility = isSpiral && showSpeed ? Visibility.Visible : Visibility.Collapsed;
            SpiralUsageVisibility = isSpiral && showUsage ? Visibility.Visible : Visibility.Collapsed;

            if (IsMinimalSignalActive)
                RefreshSignalBlinking();
            else
                ResetSignalBlinking();

            OnPropertyChanged(nameof(ShowSpeed));
            OnPropertyChanged(nameof(ShowUsage));
            OnPropertyChanged(nameof(IsMinimalMode));
            OnPropertyChanged(nameof(IsSpiralMode));
            UpdateAnimationSelectionMode();
        }

        private void OnStatsUpdated(NetworkStats stats)
        {
            UpdateSignalBlinking(stats.DownloadSpeed, stats.UploadSpeed);

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
            bool showNetworkSpeed,
            bool showTodayUsage,
            string downNum, string downUnit, string upNum, string upUnit,
            string downUsageNum, string downUsageUnit, string upUsageNum, string upUsageUnit,
            long downColorQ, long upColorQ)
        {
            return $"{mode}|{showNetworkSpeed}|{showTodayUsage}|{downNum}|{downUnit}|{upNum}|{upUnit}|{downUsageNum}|{downUsageUnit}|{upUsageNum}|{upUsageUnit}|{downColorQ}|{upColorQ}";
        }

        private string BuildRenderKey(double downSpeed, double upSpeed, long totalDown, long totalUp)
        {
            var (downNum, downUnit) = FormatHelper.FormatSpeed(downSpeed, alwaysShowDecimal: true);
            var (upNum, upUnit) = FormatHelper.FormatSpeed(upSpeed, alwaysShowDecimal: true);
            var (downUsageNum, downUsageUnit) = FormatHelper.FormatUsage(totalDown, alwaysShowDecimal: true);
            var (upUsageNum, upUsageUnit) = FormatHelper.FormatUsage(totalUp, alwaysShowDecimal: true);

            long downColorQ = (long)(downSpeed / SpeedColorStepBytesPerSecond);
            long upColorQ = (long)(upSpeed / SpeedColorStepBytesPerSecond);

            return BuildRenderKey(
                _displayMode,
                ShouldShowNetworkSpeed,
                ShouldShowTodayUsage,
                downNum,
                downUnit,
                upNum,
                upUnit,
                downUsageNum,
                downUsageUnit,
                upUsageNum,
                upUsageUnit,
                downColorQ,
                upColorQ);
        }

        private void UpdateDisplay(double downSpeed, double upSpeed, long totalDown, long totalUp)
        {
            var settings = SettingsService.Instance.Settings;
            int colorMaxMbps = settings.SpeedColorMaxMbps;
            int motionMaxMbps = Math.Max(1, Math.Min(1000, settings.IndicatorBlinkThresholdMbps));

            var (downNum, downUnit) = FormatHelper.FormatSpeed(downSpeed, alwaysShowDecimal: true);
            var (upNum, upUnit) = FormatHelper.FormatSpeed(upSpeed, alwaysShowDecimal: true);
            var (downUsageNum, downUsageUnit) = FormatHelper.FormatUsage(totalDown, alwaysShowDecimal: true);
            var (upUsageNum, upUsageUnit) = FormatHelper.FormatUsage(totalUp, alwaysShowDecimal: true);

            UpdateSpiralVisuals(
                Math.Max(downSpeed, upSpeed),
                Math.Max(0.0, downSpeed) + Math.Max(0.0, upSpeed),
                colorMaxMbps,
                motionMaxMbps);

            long downColorQ = (long)(downSpeed / SpeedColorStepBytesPerSecond);
            long upColorQ = (long)(upSpeed / SpeedColorStepBytesPerSecond);

            string key = BuildRenderKey(
                _displayMode,
                ShouldShowNetworkSpeed,
                ShouldShowTodayUsage,
                downNum,
                downUnit,
                upNum,
                upUnit,
                downUsageNum,
                downUsageUnit,
                upUsageNum,
                upUsageUnit,
                downColorQ,
                upColorQ);

            if (key == _lastRenderedKey)
                return;
            _lastRenderedKey = key;

            if (!ShouldShowNetworkSpeed && ShouldShowTodayUsage)
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
                var downBrush = ColorGradient.GetSpeedBrush(downSpeed, colorMaxMbps);
                DownloadColor = downBrush;
                DownloadLabelColor = downBrush;
                DownloadSignalColor = GetSignalBrush(_downloadBreath, downBrush);
            }

            if (upColorQ != _lastUpColorQ)
            {
                _lastUpColorQ = upColorQ;
                var upBrush = ColorGradient.GetSpeedBrush(upSpeed, colorMaxMbps);
                UploadColor = upBrush;
                UploadLabelColor = upBrush;
                UploadSignalColor = GetSignalBrush(_uploadBreath, upBrush);
            }
        }

        private void UpdateSpiralVisuals(double colorSpeedBytesPerSecond, double motionSpeedBytesPerSecond, int colorMaxMbps, int motionMaxMbps)
        {
            var clampedColorSpeed = Math.Max(0.0, colorSpeedBytesPerSecond);
            var clampedMotionSpeed = Math.Max(0.0, motionSpeedBytesPerSecond);
            _lastSpiralColorSpeedBytesPerSecond = clampedColorSpeed;
            _lastSpiralMotionSpeedBytesPerSecond = clampedMotionSpeed;
            double rawMotionRatio = GetMotionRatio(clampedMotionSpeed, motionMaxMbps);
            double stabilizedMotionRatio = GetStabilizedSpiralMotionRatio(rawMotionRatio);
            SpiralMotionRatio = stabilizedMotionRatio;
            LogSpiralMotionDebug(clampedMotionSpeed, rawMotionRatio, stabilizedMotionRatio, motionMaxMbps);

            long speedColorQ = (long)(clampedColorSpeed / SpeedColorStepBytesPerSecond);
            if (speedColorQ == _lastSpiralColorQ)
                return;

            _lastSpiralColorQ = speedColorQ;
            SpiralColor = ColorGradient.GetSpeedBrush(clampedColorSpeed, colorMaxMbps);
        }

        private double GetStabilizedSpiralMotionRatio(double rawRatio)
        {
            long now = Stopwatch.GetTimestamp();
            if (_lastSpiralMotionUpdateTick == 0)
            {
                _lastSpiralMotionUpdateTick = now;
                _lastSpiralMotionPeakTick = now;
                _heldSpiralMotionRatio = rawRatio;
                return _heldSpiralMotionRatio;
            }

            double elapsedSeconds = (now - _lastSpiralMotionUpdateTick) / (double)Stopwatch.Frequency;
            _lastSpiralMotionUpdateTick = now;
            elapsedSeconds = Math.Max(0.0, Math.Min(1.0, elapsedSeconds));

            if (rawRatio >= _heldSpiralMotionRatio)
            {
                _heldSpiralMotionRatio = rawRatio;
                _lastSpiralMotionPeakTick = now;
                return _heldSpiralMotionRatio;
            }

            double secondsSincePeak = (now - _lastSpiralMotionPeakTick) / (double)Stopwatch.Frequency;
            if (secondsSincePeak < SpiralMotionHoldSeconds)
                return _heldSpiralMotionRatio;

            double smoothing = 1.0 - Math.Exp(-elapsedSeconds / SpiralMotionDecaySeconds);
            _heldSpiralMotionRatio += (rawRatio - _heldSpiralMotionRatio) * smoothing;
            return _heldSpiralMotionRatio;
        }

        private void LogSpiralMotionDebug(double speedBytesPerSecond, double rawRatio, double stabilizedRatio, int thresholdMbps)
        {
            if (!IsSpiralMode)
                return;

            long now = Stopwatch.GetTimestamp();
            if (_lastSpiralMotionLogTick != 0 &&
                (now - _lastSpiralMotionLogTick) / (double)Stopwatch.Frequency < 1.0)
            {
                return;
            }

            _lastSpiralMotionLogTick = now;
            double mbps = speedBytesPerSecond * 8.0 / 1_000_000.0;
            LogService.Debug(
                $"SpiralMotion speedMbps={mbps:0.###}, thresholdMbps={thresholdMbps}, raw={rawRatio:0.###}, stabilized={stabilizedRatio:0.###}");
        }

        private static double GetMotionRatio(double speedBytesPerSecond, int thresholdMbps)
        {
            double mbps = Math.Max(0.0, speedBytesPerSecond) * 8.0 / 1_000_000.0;
            double threshold = Math.Max(1.0, thresholdMbps);
            return Math.Min(1.0, Math.Max(0.0, mbps / threshold));
        }

        private void UpdateSignalBlinking(double downSpeed, double upSpeed)
        {
            UpdateSignalTarget(
                _downloadBreath,
                downSpeed,
                value => DownloadSignalOpacity = value,
                value => DownloadSignalColor = value);
            UpdateSignalTarget(
                _uploadBreath,
                upSpeed,
                value => UploadSignalOpacity = value,
                value => UploadSignalColor = value);

            if (IsMinimalSignalActive && HasActiveBlinking())
                EnsureSignalRendering();
            else if (!HasActiveBlinking())
                StopSignalRendering();
        }

        private void UpdateSignalTarget(
            SignalBreathState state,
            double speed,
            Action<double> setOpacity,
            Action<Brush> setColor)
        {
            state.LastRawSpeed = speed;
            state.RawSpeedInitialized = true;

            if (!IsMinimalSignalActive || speed < SignalBlinkMinSpeedBytesPerSecond)
            {
                ResetSignalBlinkVisual(state);
                setColor(Brushes.White);
                setOpacity(SignalBrightOpacity);
                return;
            }

            double targetIntervalSeconds = GetSignalBlinkIntervalMs(speed) / 1000.0;
            if (!state.Active)
            {
                state.Active = true;
                state.CurrentBlinkIntervalSeconds = targetIntervalSeconds;
                state.TargetBlinkIntervalSeconds = targetIntervalSeconds;
            }
            else
            {
                state.TargetBlinkIntervalSeconds = targetIntervalSeconds;
            }

            setColor(ColorGradient.GetSpeedBrush(speed, SettingsService.Instance.Settings.SpeedColorMaxMbps));
            if (state.LastRenderTick == 0)
                setOpacity(SignalBrightOpacity);
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

            if (!IsMinimalSignalActive || (!downloadActive && !uploadActive))
                StopSignalRendering();
        }

        private bool UpdateSignalFrame(SignalBreathState state, Action<double> setOpacity)
        {
            if (!state.Active)
            {
                setOpacity(SignalBrightOpacity);
                return false;
            }

            long now = Stopwatch.GetTimestamp();
            if (state.LastRenderTick == 0)
            {
                state.LastRenderTick = now;
                setOpacity(SignalBrightOpacity);
                return true;
            }

            double elapsedSeconds = (now - state.LastRenderTick) / (double)Stopwatch.Frequency;
            state.LastRenderTick = now;
            elapsedSeconds = Math.Max(0.0, Math.Min(0.08, elapsedSeconds));

            double smoothing = 1.0 - Math.Exp(-elapsedSeconds / SignalIntervalSmoothingSeconds);
            state.CurrentBlinkIntervalSeconds +=
                (state.TargetBlinkIntervalSeconds - state.CurrentBlinkIntervalSeconds) * smoothing;

            double blinkIntervalSeconds = Math.Max(SignalFastBlinkMs / 1000.0, state.CurrentBlinkIntervalSeconds);
            state.Phase = (state.Phase + elapsedSeconds * Math.PI / blinkIntervalSeconds) % (Math.PI * 2.0);

            double brightness = (1.0 + Math.Cos(state.Phase)) / 2.0;
            double opacity = SignalOffOpacity + (SignalBrightOpacity - SignalOffOpacity) * brightness;

            setOpacity(Math.Max(0.0, Math.Min(1.0, opacity)));
            return true;
        }

        private void RefreshSignalBlinking()
        {
            if (_downloadBreath.RawSpeedInitialized)
            {
                UpdateSignalTarget(
                    _downloadBreath,
                    _downloadBreath.LastRawSpeed,
                    value => DownloadSignalOpacity = value,
                    value => DownloadSignalColor = value);
            }
            else
            {
                DownloadSignalColor = Brushes.White;
                DownloadSignalOpacity = SignalBrightOpacity;
            }

            if (_uploadBreath.RawSpeedInitialized)
            {
                UpdateSignalTarget(
                    _uploadBreath,
                    _uploadBreath.LastRawSpeed,
                    value => UploadSignalOpacity = value,
                    value => UploadSignalColor = value);
            }
            else
            {
                UploadSignalColor = Brushes.White;
                UploadSignalOpacity = SignalBrightOpacity;
            }

            if (IsMinimalSignalActive && HasActiveBlinking())
                EnsureSignalRendering();
            else if (!HasActiveBlinking())
                StopSignalRendering();
        }

        private double GetSignalBlinkIntervalMs(double bytesPerSecond)
        {
            double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
            double threshold = Math.Max(1, _indicatorBlinkThresholdMbps);
            double ratio = Math.Min(1.0, Math.Max(0.0, mbps / threshold));
            return SignalSlowBlinkMs - (SignalSlowBlinkMs - SignalFastBlinkMs) * ratio;
        }

        private Brush GetSignalBrush(SignalBreathState state, Brush speedBrush)
        {
            return IsMinimalSignalActive && !state.Active ? Brushes.White : speedBrush;
        }

        private bool HasActiveBlinking()
        {
            return IsSignalBlinkingActive(_downloadBreath) || IsSignalBlinkingActive(_uploadBreath);
        }

        private static bool IsSignalBlinkingActive(SignalBreathState state)
        {
            return state.Active;
        }

        private void ResetSignalBlinking()
        {
            StopSignalRendering();
            ResetSignalBlinkVisual(_downloadBreath);
            ResetSignalBlinkVisual(_uploadBreath);
            DownloadSignalColor = Brushes.White;
            UploadSignalColor = Brushes.White;
            DownloadSignalOpacity = SignalBrightOpacity;
            UploadSignalOpacity = SignalBrightOpacity;
        }

        private static void ResetSignalBlinkVisual(SignalBreathState state)
        {
            state.Active = false;
            state.LastRenderTick = 0;
            state.Phase = 0;
            state.CurrentBlinkIntervalSeconds = SignalSlowBlinkMs / 1000.0;
            state.TargetBlinkIntervalSeconds = SignalSlowBlinkMs / 1000.0;
        }

        private void UpdateAnimationSelectionMode()
        {
            if (IsSpiralAnimationActive && MathCurveCatalog.IsRandomKey(_overlayAnimationKey))
            {
                if (!IsRealCurveKey(_activeOverlayAnimationKey))
                    ActiveOverlayAnimationKey = MathCurveCatalog.DefaultKey;
                EnsureRandomAnimationTimer();
                return;
            }

            StopRandomAnimationTimer();
            if (!MathCurveCatalog.IsRandomKey(_overlayAnimationKey))
                ActiveOverlayAnimationKey = _overlayAnimationKey;
            else if (!IsRealCurveKey(_activeOverlayAnimationKey))
                ActiveOverlayAnimationKey = MathCurveCatalog.DefaultKey;
        }

        private void EnsureRandomAnimationTimer()
        {
            if (_randomAnimationTimer == null)
            {
                _randomAnimationTimer = new DispatcherTimer();
                _randomAnimationTimer.Tick += OnRandomAnimationTimerTick;
            }

            if (!_randomAnimationTimer.IsEnabled)
                ScheduleNextRandomAnimation();
        }

        private void ScheduleNextRandomAnimation()
        {
            if (_randomAnimationTimer == null)
                return;

            _randomAnimationTimer.Stop();
            _randomAnimationTimer.Interval = TimeSpan.FromMinutes(_randomAnimationIntervalMinutes);
            _randomAnimationTimer.Start();
        }

        private void StopRandomAnimationTimer()
        {
            if (_randomAnimationTimer?.IsEnabled == true)
                _randomAnimationTimer.Stop();
        }

        private void OnRandomAnimationTimerTick(object sender, EventArgs e)
        {
            _randomAnimationTimer.Stop();
            if (!IsSpiralAnimationActive || !MathCurveCatalog.IsRandomKey(_overlayAnimationKey))
                return;

            ActiveOverlayAnimationKey = PickRandomCurveKey(_activeOverlayAnimationKey);
            ScheduleNextRandomAnimation();
        }

        private string PickRandomCurveKey(string excludedKey)
        {
            var definitions = MathCurveCatalog.All;
            if (definitions.Count == 0)
                return MathCurveCatalog.DefaultKey;
            if (definitions.Count == 1)
                return definitions[0].Key;

            string currentKey = MathCurveCatalog.Get(excludedKey).Key;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string candidate = definitions[_random.Next(definitions.Count)].Key;
                if (!string.Equals(candidate, currentKey, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            foreach (var definition in definitions)
            {
                if (!string.Equals(definition.Key, currentKey, StringComparison.OrdinalIgnoreCase))
                    return definition.Key;
            }

            return definitions[0].Key;
        }

        private static bool IsRealCurveKey(string key)
        {
            foreach (var definition in MathCurveCatalog.All)
            {
                if (string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void OnSettingsChanged()
        {
            ApplySettings();
        }

        private void ApplySettings()
        {
            var s = SettingsService.Instance.Settings;
            int randomAnimationIntervalMinutes = ClampRandomAnimationIntervalMinutes(
                s.OverlayRandomAnimationIntervalMinutes);
            bool randomAnimationIntervalChanged =
                randomAnimationIntervalMinutes != _randomAnimationIntervalMinutes;

            _randomAnimationIntervalMinutes = randomAnimationIntervalMinutes;
            FontFamily = FontHelper.ResolveFontFamily(s.FontFamily, "overlay");
            FontSize = Math.Max(11, Math.Min(19, s.FontSize));
            OverlayTextEnhancementEnabled = s.OverlayTextEnhancementEnabled;
            IsVertical = s.Layout == "vertical";
            IsLocked = s.LockOnTop;
            ShowNetworkSpeed = s.ShowNetworkSpeed;
            ShowTodayUsage = s.ShowTodayUsage;
            DisplayMode = s.DisplayMode;
            OverlayAnimationKey = s.OverlayAnimationKey;

            if (randomAnimationIntervalChanged &&
                _randomAnimationTimer?.IsEnabled == true &&
                IsSpiralMode &&
                MathCurveCatalog.IsRandomKey(_overlayAnimationKey))
            {
                ScheduleNextRandomAnimation();
            }

            _smoothTransition = s.SmoothTransition;
            _indicatorBlinkThresholdMbps = Math.Max(1, Math.Min(1000, s.IndicatorBlinkThresholdMbps));
            // Animation completes exactly as the next sample arrives
            _animationDurationMs = s.RefreshInterval;
            _lastRenderedKey = null;
            _lastDownColorQ = -1;
            _lastUpColorQ = -1;
            _lastSpiralColorQ = -1;
            UpdateSpiralVisuals(
                _lastSpiralColorSpeedBytesPerSecond,
                _lastSpiralMotionSpeedBytesPerSecond,
                s.SpeedColorMaxMbps,
                _indicatorBlinkThresholdMbps);
            RefreshSignalBlinking();

            if (!_smoothTransition)
            {
                _animationTimer?.Stop();
                _displayedDownSpeed = _targetDownSpeed;
                _displayedUpSpeed = _targetUpSpeed;
                _displayedTotalDown = _targetTotalDown;
                _displayedTotalUp = _targetTotalUp;
            }

            UpdateDisplay(_displayedDownSpeed, _displayedUpSpeed, (long)_displayedTotalDown, (long)_displayedTotalUp);
        }

        private static string NormalizeDisplayMode(string value)
        {
            switch (value)
            {
                case StandardDisplayMode:
                case MinimalDisplayMode:
                case SpiralDisplayMode:
                    return value;
                case "speed":
                case "usage":
                case "both":
                    return StandardDisplayMode;
                default:
                    return StandardDisplayMode;
            }
        }

        private static int ClampRandomAnimationIntervalMinutes(int value)
        {
            return Math.Max(MinRandomAnimationIntervalMinutes, Math.Min(MaxRandomAnimationIntervalMinutes, value));
        }

        public void Cleanup()
        {
            _animationTimer?.Stop();
            if (_randomAnimationTimer != null)
            {
                _randomAnimationTimer.Stop();
                _randomAnimationTimer.Tick -= OnRandomAnimationTimerTick;
                _randomAnimationTimer = null;
            }
            StopSignalRendering();
            NetworkMonitorService.Instance.StatsUpdated -= OnStatsUpdated;
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
        }

        private sealed class SignalBreathState
        {
            public bool Active { get; set; }
            public bool RawSpeedInitialized { get; set; }
            public double LastRawSpeed { get; set; }
            public double CurrentBlinkIntervalSeconds { get; set; } = SignalSlowBlinkMs / 1000.0;
            public double TargetBlinkIntervalSeconds { get; set; } = SignalSlowBlinkMs / 1000.0;
            public double Phase { get; set; }
            public long LastRenderTick { get; set; }
        }
    }
}
