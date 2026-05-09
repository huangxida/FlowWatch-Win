using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlowWatch.Helpers;
using FlowWatch.Models;
using FlowWatch.Services;

namespace FlowWatch.ViewModels
{
    public class AppDisplayRecord
    {
        public string ProcessName { get; set; }
        public long DownloadBytes { get; set; }
        public long UploadBytes { get; set; }
        public long TotalBytes { get; set; }
        public string DownloadFormatted { get; set; }
        public string UploadFormatted { get; set; }
        public string TotalFormatted { get; set; }
        public string ShareFormatted { get; set; }
        public string SpeedFormatted { get; set; }
        public double DownloadRatio { get; set; }
        public double UploadRatio { get; set; }
        public double TotalRatio { get; set; }
        public ImageSource Icon { get; set; }
        public string IconFallback { get; set; }
        public Visibility IconVisibility { get; set; }
        public Visibility IconFallbackVisibility { get; set; }
    }

    public class AppTrafficViewModel : ViewModelBase
    {
        private static readonly ConcurrentDictionary<string, ImageSource> _iconCache =
            new ConcurrentDictionary<string, ImageSource>();

        private string _selectedRange = "day";
        private string _periodLabel;
        private string _totalDownloadFormatted;
        private string _totalUploadFormatted;
        private string _summaryTotalFormatted;
        private string _appCountLabel;
        private string _topAppName;
        private string _downloadShareFormatted;
        private string _uploadShareFormatted;
        private Visibility _emptyVisibility = Visibility.Collapsed;
        private Visibility _recordsVisibility = Visibility.Visible;
        private ObservableCollection<AppDisplayRecord> _appRecords = new ObservableCollection<AppDisplayRecord>();
        private DispatcherTimer _realtimeTimer;

        public AppTrafficViewModel()
        {
            RealtimeCommand = new RelayCommand(() => SelectedRange = "realtime");
            DayCommand = new RelayCommand(() => SelectedRange = "day");
            WeekCommand = new RelayCommand(() => SelectedRange = "week");
            MonthCommand = new RelayCommand(() => SelectedRange = "month");
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Refresh();
        }

        public string SelectedRange
        {
            get => _selectedRange;
            set
            {
                if (SetProperty(ref _selectedRange, value))
                {
                    OnPropertyChanged(nameof(IsRealtimeSelected));
                    OnPropertyChanged(nameof(IsDaySelected));
                    OnPropertyChanged(nameof(IsWeekSelected));
                    OnPropertyChanged(nameof(IsMonthSelected));
                    UpdateRealtimeTimer();
                    Refresh();
                }
            }
        }

        public bool IsRealtimeSelected => _selectedRange == "realtime";
        public bool IsDaySelected => _selectedRange == "day";
        public bool IsWeekSelected => _selectedRange == "week";
        public bool IsMonthSelected => _selectedRange == "month";

        public string PeriodLabel
        {
            get => _periodLabel;
            private set => SetProperty(ref _periodLabel, value);
        }

        public string TotalDownloadFormatted
        {
            get => _totalDownloadFormatted;
            private set => SetProperty(ref _totalDownloadFormatted, value);
        }

        public string TotalUploadFormatted
        {
            get => _totalUploadFormatted;
            private set => SetProperty(ref _totalUploadFormatted, value);
        }

        public string SummaryTotalFormatted
        {
            get => _summaryTotalFormatted;
            private set => SetProperty(ref _summaryTotalFormatted, value);
        }

        public string AppCountLabel
        {
            get => _appCountLabel;
            private set => SetProperty(ref _appCountLabel, value);
        }

        public string TopAppName
        {
            get => _topAppName;
            private set => SetProperty(ref _topAppName, value);
        }

        public string DownloadShareFormatted
        {
            get => _downloadShareFormatted;
            private set => SetProperty(ref _downloadShareFormatted, value);
        }

        public string UploadShareFormatted
        {
            get => _uploadShareFormatted;
            private set => SetProperty(ref _uploadShareFormatted, value);
        }

        public Visibility EmptyVisibility
        {
            get => _emptyVisibility;
            private set => SetProperty(ref _emptyVisibility, value);
        }

        public Visibility RecordsVisibility
        {
            get => _recordsVisibility;
            private set => SetProperty(ref _recordsVisibility, value);
        }

        public ObservableCollection<AppDisplayRecord> AppRecords
        {
            get => _appRecords;
            private set => SetProperty(ref _appRecords, value);
        }

        public ICommand RealtimeCommand { get; }
        public ICommand DayCommand { get; }
        public ICommand WeekCommand { get; }
        public ICommand MonthCommand { get; }

        private void OnLanguageChanged()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (_selectedRange == "realtime")
            {
                RefreshRealtime();
                return;
            }

            var allRecords = ProcessTrafficService.Instance.GetRecords();
            var today = DateTime.Now.Date;
            var loc = LocalizationService.Instance;

            List<DailyAppTrafficRecord> filtered;
            switch (_selectedRange)
            {
                case "week":
                    int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                    var weekStart = today.AddDays(-diff);
                    PeriodLabel = loc.Format("AppTraffic.PeriodWeek", weekStart.ToString("MM/dd"), weekStart.AddDays(6).ToString("MM/dd"));
                    filtered = FilterByRange(allRecords, weekStart, today);
                    break;
                case "month":
                    var monthStart = new DateTime(today.Year, today.Month, 1);
                    PeriodLabel = loc.Format("AppTraffic.PeriodMonth", today.Year, today.Month);
                    filtered = FilterByRange(allRecords, monthStart, today);
                    break;
                default:
                    PeriodLabel = loc.Get("AppTraffic.PeriodToday");
                    filtered = FilterByRange(allRecords, today, today);
                    break;
            }

            var aggregated = new Dictionary<string, long[]>();
            foreach (var day in filtered)
            {
                if (day.Apps == null) continue;
                foreach (var app in day.Apps)
                {
                    if (!aggregated.TryGetValue(app.ProcessName, out var arr))
                    {
                        arr = new long[2];
                        aggregated[app.ProcessName] = arr;
                    }

                    arr[0] += app.DownloadBytes;
                    arr[1] += app.UploadBytes;
                }
            }

            long totalDown = aggregated.Values.Sum(a => a[0]);
            long totalUp = aggregated.Values.Sum(a => a[1]);
            long grandTotal = totalDown + totalUp;
            TotalDownloadFormatted = FormatUsageString(totalDown);
            TotalUploadFormatted = FormatUsageString(totalUp);
            SummaryTotalFormatted = FormatUsageString(grandTotal);
            DownloadShareFormatted = grandTotal > 0 ? FormatPercent((double)totalDown / grandTotal) : "0%";
            UploadShareFormatted = grandTotal > 0 ? FormatPercent((double)totalUp / grandTotal) : "0%";

            var sorted = aggregated
                .OrderByDescending(kv => kv.Value[0] + kv.Value[1])
                .ToList();

            long maxBytes = sorted.Count > 0
                ? sorted.Max(kv => Math.Max(kv.Value[0], kv.Value[1]))
                : 0;
            long maxTotal = sorted.Count > 0
                ? sorted.Max(kv => kv.Value[0] + kv.Value[1])
                : 0;

            var displayRecords = new ObservableCollection<AppDisplayRecord>();
            foreach (var kv in sorted)
            {
                var total = kv.Value[0] + kv.Value[1];
                var icon = GetProcessIcon(kv.Key);
                displayRecords.Add(new AppDisplayRecord
                {
                    ProcessName = kv.Key,
                    DownloadBytes = kv.Value[0],
                    UploadBytes = kv.Value[1],
                    TotalBytes = total,
                    DownloadFormatted = FormatUsageString(kv.Value[0]),
                    UploadFormatted = FormatUsageString(kv.Value[1]),
                    TotalFormatted = FormatUsageString(total),
                    ShareFormatted = grandTotal > 0 ? FormatPercent((double)total / grandTotal) : "0%",
                    SpeedFormatted = "-",
                    DownloadRatio = maxBytes > 0 ? (double)kv.Value[0] / maxBytes : 0,
                    UploadRatio = maxBytes > 0 ? (double)kv.Value[1] / maxBytes : 0,
                    TotalRatio = maxTotal > 0 ? (double)total / maxTotal : 0,
                    Icon = icon,
                    IconFallback = BuildIconFallback(kv.Key),
                    IconVisibility = icon != null ? Visibility.Visible : Visibility.Collapsed,
                    IconFallbackVisibility = icon != null ? Visibility.Collapsed : Visibility.Visible,
                });
            }

            AppRecords = displayRecords;
            UpdateMeta(displayRecords);
        }

        private void RefreshRealtime()
        {
            PeriodLabel = LocalizationService.Instance.Get("AppTraffic.PeriodRealtime");

            var speeds = ProcessTrafficService.Instance.GetRealtimeSpeeds();
            double totalDown = speeds.Values.Sum(a => a[0]);
            double totalUp = speeds.Values.Sum(a => a[1]);
            double grandTotal = totalDown + totalUp;

            TotalDownloadFormatted = FormatSpeedString(totalDown);
            TotalUploadFormatted = FormatSpeedString(totalUp);
            SummaryTotalFormatted = FormatSpeedString(grandTotal);
            DownloadShareFormatted = grandTotal > 0 ? FormatPercent(totalDown / grandTotal) : "0%";
            UploadShareFormatted = grandTotal > 0 ? FormatPercent(totalUp / grandTotal) : "0%";

            var sorted = speeds
                .Where(kv => kv.Value[0] > 0.5 || kv.Value[1] > 0.5)
                .OrderByDescending(kv => kv.Value[0] + kv.Value[1])
                .ToList();

            double maxSpeed = sorted.Count > 0
                ? sorted.Max(kv => Math.Max(kv.Value[0], kv.Value[1]))
                : 0;
            double maxTotal = sorted.Count > 0
                ? sorted.Max(kv => kv.Value[0] + kv.Value[1])
                : 0;

            var displayRecords = new ObservableCollection<AppDisplayRecord>();
            foreach (var kv in sorted)
            {
                var total = kv.Value[0] + kv.Value[1];
                var icon = GetProcessIcon(kv.Key);
                displayRecords.Add(new AppDisplayRecord
                {
                    ProcessName = kv.Key,
                    DownloadFormatted = FormatSpeedString(kv.Value[0]),
                    UploadFormatted = FormatSpeedString(kv.Value[1]),
                    TotalFormatted = FormatSpeedString(total),
                    ShareFormatted = grandTotal > 0 ? FormatPercent(total / grandTotal) : "0%",
                    SpeedFormatted = FormatSpeedString(total),
                    DownloadRatio = maxSpeed > 0 ? kv.Value[0] / maxSpeed : 0,
                    UploadRatio = maxSpeed > 0 ? kv.Value[1] / maxSpeed : 0,
                    TotalRatio = maxTotal > 0 ? total / maxTotal : 0,
                    Icon = icon,
                    IconFallback = BuildIconFallback(kv.Key),
                    IconVisibility = icon != null ? Visibility.Visible : Visibility.Collapsed,
                    IconFallbackVisibility = icon != null ? Visibility.Collapsed : Visibility.Visible,
                });
            }

            AppRecords = displayRecords;
            UpdateMeta(displayRecords);
        }

        private void UpdateMeta(ObservableCollection<AppDisplayRecord> records)
        {
            var loc = LocalizationService.Instance;
            AppCountLabel = loc.Format("AppTraffic.AppCount", records.Count);
            TopAppName = records.Count > 0 ? records[0].ProcessName : loc.Get("AppTraffic.NoTopApp");
            EmptyVisibility = records.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            RecordsVisibility = records.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateRealtimeTimer()
        {
            if (_selectedRange == "realtime")
            {
                if (_realtimeTimer == null)
                {
                    ProcessTrafficService.Instance.GetRealtimeSpeeds();

                    _realtimeTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    _realtimeTimer.Tick += (s, e) => RefreshRealtime();
                    _realtimeTimer.Start();
                }
            }
            else
            {
                if (_realtimeTimer != null)
                {
                    _realtimeTimer.Stop();
                    _realtimeTimer = null;
                }
            }
        }

        public void StopRealtimeTimer()
        {
            if (_realtimeTimer != null)
            {
                _realtimeTimer.Stop();
                _realtimeTimer = null;
            }
        }

        public void StartRealtimeTimerIfNeeded()
        {
            if (_selectedRange == "realtime" && _realtimeTimer == null)
            {
                ProcessTrafficService.Instance.GetRealtimeSpeeds();
                _realtimeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _realtimeTimer.Tick += (s, e) => RefreshRealtime();
                _realtimeTimer.Start();
            }
        }

        private List<DailyAppTrafficRecord> FilterByRange(
            List<DailyAppTrafficRecord> records, DateTime start, DateTime end)
        {
            var startStr = start.ToString("yyyy-MM-dd");
            var endStr = end.ToString("yyyy-MM-dd");
            return records
                .Where(r => string.Compare(r.Date, startStr, StringComparison.Ordinal) >= 0
                         && string.Compare(r.Date, endStr, StringComparison.Ordinal) <= 0)
                .ToList();
        }

        private static string FormatUsageString(long bytes)
        {
            var fmt = FormatHelper.FormatUsage(bytes);
            return fmt.Num + " " + fmt.Unit;
        }

        private static string FormatSpeedString(double bytesPerSecond)
        {
            var fmt = FormatHelper.FormatSpeed(bytesPerSecond);
            return fmt.Num + " " + fmt.Unit;
        }

        private static string FormatPercent(double value)
        {
            var clamped = Math.Max(0, Math.Min(1, value));
            return (clamped * 100).ToString("F0") + "%";
        }

        private static ImageSource GetProcessIcon(string processName)
        {
            return _iconCache.GetOrAdd(processName, name =>
            {
                try
                {
                    var exePath = ProcessTrafficService.Instance.GetExePath(name);
                    if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                        return null;

                    using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                    {
                        if (icon == null) return null;
                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                }
                catch
                {
                    return null;
                }
            });
        }

        private static string BuildIconFallback(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return "?";

            foreach (var ch in processName.Trim())
            {
                if (char.IsLetterOrDigit(ch))
                    return char.ToUpperInvariant(ch).ToString();
            }

            return "?";
        }
    }
}
