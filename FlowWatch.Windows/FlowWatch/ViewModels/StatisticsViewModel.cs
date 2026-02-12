using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using FlowWatch.Helpers;
using FlowWatch.Models;
using FlowWatch.Services;

namespace FlowWatch.ViewModels
{
    public class DailyDisplayRecord
    {
        public string Date { get; set; }
        public long DownloadBytes { get; set; }
        public long UploadBytes { get; set; }
        public string DownloadFormatted { get; set; }
        public string UploadFormatted { get; set; }
        public double DownloadRatio { get; set; }
        public double UploadRatio { get; set; }
    }

    public class StatisticsViewModel : ViewModelBase
    {
        private string _selectedRange = "day";
        private string _periodLabel;
        private string _totalDownloadFormatted;
        private string _totalUploadFormatted;
        private ObservableCollection<DailyDisplayRecord> _dailyRecords = new ObservableCollection<DailyDisplayRecord>();

        public StatisticsViewModel()
        {
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
                    OnPropertyChanged(nameof(IsDaySelected));
                    OnPropertyChanged(nameof(IsWeekSelected));
                    OnPropertyChanged(nameof(IsMonthSelected));
                    Refresh();
                }
            }
        }

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

        public ObservableCollection<DailyDisplayRecord> DailyRecords
        {
            get => _dailyRecords;
            private set => SetProperty(ref _dailyRecords, value);
        }

        public ICommand DayCommand { get; }
        public ICommand WeekCommand { get; }
        public ICommand MonthCommand { get; }

        private void OnLanguageChanged()
        {
            Refresh();
        }

        public void Refresh()
        {
            LogService.Info($"StatisticsViewModel.Refresh() 范围={_selectedRange}");
            var allRecords = TrafficHistoryService.Instance.GetRecords();
            LogService.Info($"获取到 {allRecords.Count} 条历史记录");
            var today = DateTime.Now.Date;
            var loc = LocalizationService.Instance;

            List<DailyTrafficRecord> filtered;

            switch (_selectedRange)
            {
                case "week":
                    // 本周一到今天
                    int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                    var weekStart = today.AddDays(-diff);
                    PeriodLabel = loc.Format("Stats.PeriodWeek", weekStart.ToString("MM/dd"), weekStart.AddDays(6).ToString("MM/dd"));
                    filtered = FilterByRange(allRecords, weekStart, today);
                    break;

                case "month":
                    var monthStart = new DateTime(today.Year, today.Month, 1);
                    PeriodLabel = loc.Format("Stats.PeriodMonth", today.Year, today.Month);
                    filtered = FilterByRange(allRecords, monthStart, today);
                    break;

                default: // day
                    PeriodLabel = loc.Get("Stats.PeriodToday");
                    filtered = FilterByRange(allRecords, today, today);
                    break;
            }

            // 计算汇总
            long totalDown = filtered.Sum(r => r.DownloadBytes);
            long totalUp = filtered.Sum(r => r.UploadBytes);

            var downFmt = FormatHelper.FormatUsage(totalDown);
            var upFmt = FormatHelper.FormatUsage(totalUp);
            TotalDownloadFormatted = $"{downFmt.Num} {downFmt.Unit}";
            TotalUploadFormatted = $"{upFmt.Num} {upFmt.Unit}";

            // 计算柱状图比例
            long maxBytes = filtered.Count > 0
                ? filtered.Max(r => Math.Max(r.DownloadBytes, r.UploadBytes))
                : 0;

            var displayRecords = new ObservableCollection<DailyDisplayRecord>();
            foreach (var r in filtered.OrderBy(r => r.Date))
            {
                var dFmt = FormatHelper.FormatUsage(r.DownloadBytes);
                var uFmt = FormatHelper.FormatUsage(r.UploadBytes);

                // 将 yyyy-MM-dd 转为短日期显示
                string displayDate = r.Date;
                if (DateTime.TryParseExact(r.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    displayDate = dt.ToString("M/d");
                }

                displayRecords.Add(new DailyDisplayRecord
                {
                    Date = displayDate,
                    DownloadBytes = r.DownloadBytes,
                    UploadBytes = r.UploadBytes,
                    DownloadFormatted = $"{dFmt.Num} {dFmt.Unit}",
                    UploadFormatted = $"{uFmt.Num} {uFmt.Unit}",
                    DownloadRatio = maxBytes > 0 ? (double)r.DownloadBytes / maxBytes : 0,
                    UploadRatio = maxBytes > 0 ? (double)r.UploadBytes / maxBytes : 0,
                });
            }

            DailyRecords = displayRecords;
            LogService.Info($"Refresh 完成: 过滤后 {filtered.Count} 条, 总下载={TotalDownloadFormatted}, 总上传={TotalUploadFormatted}");
        }

        private List<DailyTrafficRecord> FilterByRange(
            List<DailyTrafficRecord> records, DateTime start, DateTime end)
        {
            var startStr = start.ToString("yyyy-MM-dd");
            var endStr = end.ToString("yyyy-MM-dd");
            return records
                .Where(r => string.Compare(r.Date, startStr, StringComparison.Ordinal) >= 0
                         && string.Compare(r.Date, endStr, StringComparison.Ordinal) <= 0)
                .ToList();
        }
    }
}
