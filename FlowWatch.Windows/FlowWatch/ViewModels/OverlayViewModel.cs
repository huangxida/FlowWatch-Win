using System;
using System.Windows;
using System.Windows.Media;
using FlowWatch.Helpers;
using FlowWatch.Models;
using FlowWatch.Services;

namespace FlowWatch.ViewModels
{
    public class OverlayViewModel : ViewModelBase
    {
        private string _uploadNum = "0";
        private string _uploadUnit = "B/s";
        private string _downloadNum = "0";
        private string _downloadUnit = "B/s";
        private string _uploadUsageNum = "0";
        private string _uploadUsageUnit = "B";
        private string _downloadUsageNum = "0";
        private string _downloadUsageUnit = "B";
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
            var settings = SettingsService.Instance.Settings;
            int maxMbps = settings.SpeedColorMaxMbps;

            // Format speed
            var (upNum, upUnit) = FormatHelper.FormatSpeed(stats.UploadSpeed);
            var (downNum, downUnit) = FormatHelper.FormatSpeed(stats.DownloadSpeed);

            // Format usage
            var (upUsageNum, upUsageUnit) = FormatHelper.FormatUsage(stats.TotalUpload);
            var (downUsageNum, downUsageUnit) = FormatHelper.FormatUsage(stats.TotalDownload);

            // Get colors
            var upBrush = ColorGradient.GetSpeedBrush(stats.UploadSpeed, maxMbps);
            var downBrush = ColorGradient.GetSpeedBrush(stats.DownloadSpeed, maxMbps);

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

            UploadColor = upBrush;
            DownloadColor = downBrush;
            UploadLabelColor = upBrush;
            DownloadLabelColor = downBrush;
        }

        private void OnSettingsChanged()
        {
            ApplySettings();
        }

        private void ApplySettings()
        {
            var s = SettingsService.Instance.Settings;

            // Parse font family - take first font name for WPF
            var fontName = s.FontFamily?.Split(',')[0]?.Trim() ?? "Segoe UI";
            FontFamily = new FontFamily(fontName);
            FontSize = Math.Max(11, Math.Min(19, s.FontSize));
            IsVertical = s.Layout == "vertical";
            IsLocked = s.LockOnTop;
            DisplayMode = s.DisplayMode ?? "speed";
        }

        public void Cleanup()
        {
            NetworkMonitorService.Instance.StatsUpdated -= OnStatsUpdated;
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
        }
    }
}
