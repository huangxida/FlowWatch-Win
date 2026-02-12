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
        public string DownloadFormatted { get; set; }
        public string UploadFormatted { get; set; }
        public double DownloadRatio { get; set; }
        public double UploadRatio { get; set; }
        public ImageSource Icon { get; set; }
    }

    public class AppTrafficViewModel : ViewModelBase
    {
        private static readonly ConcurrentDictionary<string, ImageSource> _iconCache =
            new ConcurrentDictionary<string, ImageSource>();

        private string _selectedRange = "day";
        private string _periodLabel;
        private string _totalDownloadFormatted;
        private string _totalUploadFormatted;
        private ObservableCollection<AppDisplayRecord> _appRecords = new ObservableCollection<AppDisplayRecord>();
        private DispatcherTimer _realtimeTimer;

        public AppTrafficViewModel()
        {
            RealtimeCommand = new RelayCommand(() => SelectedRange = "realtime");
            DayCommand = new RelayCommand(() => SelectedRange = "day");
            WeekCommand = new RelayCommand(() => SelectedRange = "week");
            MonthCommand = new RelayCommand(() => SelectedRange = "month");
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

        public ObservableCollection<AppDisplayRecord> AppRecords
        {
            get => _appRecords;
            private set => SetProperty(ref _appRecords, value);
        }

        public ICommand RealtimeCommand { get; }
        public ICommand DayCommand { get; }
        public ICommand WeekCommand { get; }
        public ICommand MonthCommand { get; }

        public void Refresh()
        {
            if (_selectedRange == "realtime")
            {
                RefreshRealtime();
                return;
            }

            var allRecords = ProcessTrafficService.Instance.GetRecords();
            var today = DateTime.Now.Date;

            List<DailyAppTrafficRecord> filtered;

            switch (_selectedRange)
            {
                case "week":
                    int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                    var weekStart = today.AddDays(-diff);
                    PeriodLabel = $"本周 {weekStart:MM/dd} - {weekStart.AddDays(6):MM/dd}";
                    filtered = FilterByRange(allRecords, weekStart, today);
                    break;

                case "month":
                    var monthStart = new DateTime(today.Year, today.Month, 1);
                    PeriodLabel = $"{today.Year}年{today.Month}月";
                    filtered = FilterByRange(allRecords, monthStart, today);
                    break;

                default:
                    PeriodLabel = "今天";
                    filtered = FilterByRange(allRecords, today, today);
                    break;
            }

            // 按进程名聚合多天数据
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

            // 计算汇总
            long totalDown = aggregated.Values.Sum(a => a[0]);
            long totalUp = aggregated.Values.Sum(a => a[1]);

            var downFmt = FormatHelper.FormatUsage(totalDown);
            var upFmt = FormatHelper.FormatUsage(totalUp);
            TotalDownloadFormatted = $"{downFmt.Num} {downFmt.Unit}";
            TotalUploadFormatted = $"{upFmt.Num} {upFmt.Unit}";

            // 按总流量降序排序
            var sorted = aggregated
                .OrderByDescending(kv => kv.Value[0] + kv.Value[1])
                .ToList();

            // 计算柱状图比例
            long maxBytes = sorted.Count > 0
                ? sorted.Max(kv => Math.Max(kv.Value[0], kv.Value[1]))
                : 0;

            var displayRecords = new ObservableCollection<AppDisplayRecord>();
            foreach (var kv in sorted)
            {
                var dFmt = FormatHelper.FormatUsage(kv.Value[0]);
                var uFmt = FormatHelper.FormatUsage(kv.Value[1]);

                displayRecords.Add(new AppDisplayRecord
                {
                    ProcessName = kv.Key,
                    DownloadBytes = kv.Value[0],
                    UploadBytes = kv.Value[1],
                    DownloadFormatted = $"{dFmt.Num} {dFmt.Unit}",
                    UploadFormatted = $"{uFmt.Num} {uFmt.Unit}",
                    DownloadRatio = maxBytes > 0 ? (double)kv.Value[0] / maxBytes : 0,
                    UploadRatio = maxBytes > 0 ? (double)kv.Value[1] / maxBytes : 0,
                    Icon = GetProcessIcon(kv.Key),
                });
            }

            AppRecords = displayRecords;
        }

        private void RefreshRealtime()
        {
            PeriodLabel = "实时速度";

            var speeds = ProcessTrafficService.Instance.GetRealtimeSpeeds();

            // 计算汇总速度
            double totalDown = speeds.Values.Sum(a => a[0]);
            double totalUp = speeds.Values.Sum(a => a[1]);

            var downFmt = FormatHelper.FormatSpeed(totalDown);
            var upFmt = FormatHelper.FormatSpeed(totalUp);
            TotalDownloadFormatted = $"{downFmt.Num} {downFmt.Unit}";
            TotalUploadFormatted = $"{upFmt.Num} {upFmt.Unit}";

            // 按总速度降序排序，过滤掉速度为 0 的进程
            var sorted = speeds
                .Where(kv => kv.Value[0] > 0.5 || kv.Value[1] > 0.5)
                .OrderByDescending(kv => kv.Value[0] + kv.Value[1])
                .ToList();

            double maxSpeed = sorted.Count > 0
                ? sorted.Max(kv => Math.Max(kv.Value[0], kv.Value[1]))
                : 0;

            var displayRecords = new ObservableCollection<AppDisplayRecord>();
            foreach (var kv in sorted)
            {
                var dFmt = FormatHelper.FormatSpeed(kv.Value[0]);
                var uFmt = FormatHelper.FormatSpeed(kv.Value[1]);

                displayRecords.Add(new AppDisplayRecord
                {
                    ProcessName = kv.Key,
                    DownloadFormatted = $"{dFmt.Num} {dFmt.Unit}",
                    UploadFormatted = $"{uFmt.Num} {uFmt.Unit}",
                    DownloadRatio = maxSpeed > 0 ? kv.Value[0] / maxSpeed : 0,
                    UploadRatio = maxSpeed > 0 ? kv.Value[1] / maxSpeed : 0,
                    Icon = GetProcessIcon(kv.Key),
                });
            }

            AppRecords = displayRecords;
        }

        private void UpdateRealtimeTimer()
        {
            if (_selectedRange == "realtime")
            {
                if (_realtimeTimer == null)
                {
                    // 先丢弃第一次采样（基线）
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
    }
}
