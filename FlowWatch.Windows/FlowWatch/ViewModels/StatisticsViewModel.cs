using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
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
        public string TotalFormatted { get; set; }
        public string ShareFormatted { get; set; }
        public double DownloadRatio { get; set; }
        public double UploadRatio { get; set; }
        public double TotalRatio { get; set; }
    }

    public class StatisticsViewModel : ViewModelBase
    {
        private string _selectedRange = "day";
        private string _periodLabel;
        private string _totalDownloadFormatted;
        private string _totalUploadFormatted;
        private string _rangeTotalFormatted;
        private string _allTimeDownloadFormatted;
        private string _allTimeUploadFormatted;
        private string _totalTrafficFormatted;
        private string _todayTrafficFormatted;
        private string _yesterdayTrafficFormatted;
        private string _recordDaysFormatted;
        private string _activeDaysFormatted;
        private string _last7TotalFormatted;
        private string _last7AverageFormatted;
        private string _last30TotalFormatted;
        private string _last30AverageFormatted;
        private string _historyAverageFormatted;
        private string _activeDayAverageFormatted;
        private string _dayOverDayFormatted;
        private string _last7PeakFormatted;
        private string _allTimePeakFormatted;
        private string _downloadShareFormatted;
        private string _uploadShareFormatted;
        private string _uploadDownloadRatioFormatted;
        private string _activeRateFormatted;
        private string _currentStreakFormatted;
        private string _longestStreakFormatted;
        private string _quietDaysFormatted;
        private string _recentActiveDateFormatted;
        private string _personaTitle;
        private string _personaSubtitle;
        private Visibility _emptyVisibility = Visibility.Collapsed;
        private Visibility _recordsVisibility = Visibility.Visible;
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

        public string RangeTotalFormatted
        {
            get => _rangeTotalFormatted;
            private set => SetProperty(ref _rangeTotalFormatted, value);
        }

        public string AllTimeDownloadFormatted
        {
            get => _allTimeDownloadFormatted;
            private set => SetProperty(ref _allTimeDownloadFormatted, value);
        }

        public string AllTimeUploadFormatted
        {
            get => _allTimeUploadFormatted;
            private set => SetProperty(ref _allTimeUploadFormatted, value);
        }

        public string TotalTrafficFormatted
        {
            get => _totalTrafficFormatted;
            private set => SetProperty(ref _totalTrafficFormatted, value);
        }

        public string TodayTrafficFormatted
        {
            get => _todayTrafficFormatted;
            private set => SetProperty(ref _todayTrafficFormatted, value);
        }

        public string YesterdayTrafficFormatted
        {
            get => _yesterdayTrafficFormatted;
            private set => SetProperty(ref _yesterdayTrafficFormatted, value);
        }

        public string RecordDaysFormatted
        {
            get => _recordDaysFormatted;
            private set => SetProperty(ref _recordDaysFormatted, value);
        }

        public string ActiveDaysFormatted
        {
            get => _activeDaysFormatted;
            private set => SetProperty(ref _activeDaysFormatted, value);
        }

        public string Last7TotalFormatted
        {
            get => _last7TotalFormatted;
            private set => SetProperty(ref _last7TotalFormatted, value);
        }

        public string Last7AverageFormatted
        {
            get => _last7AverageFormatted;
            private set => SetProperty(ref _last7AverageFormatted, value);
        }

        public string Last30TotalFormatted
        {
            get => _last30TotalFormatted;
            private set => SetProperty(ref _last30TotalFormatted, value);
        }

        public string Last30AverageFormatted
        {
            get => _last30AverageFormatted;
            private set => SetProperty(ref _last30AverageFormatted, value);
        }

        public string HistoryAverageFormatted
        {
            get => _historyAverageFormatted;
            private set => SetProperty(ref _historyAverageFormatted, value);
        }

        public string ActiveDayAverageFormatted
        {
            get => _activeDayAverageFormatted;
            private set => SetProperty(ref _activeDayAverageFormatted, value);
        }

        public string DayOverDayFormatted
        {
            get => _dayOverDayFormatted;
            private set => SetProperty(ref _dayOverDayFormatted, value);
        }

        public string Last7PeakFormatted
        {
            get => _last7PeakFormatted;
            private set => SetProperty(ref _last7PeakFormatted, value);
        }

        public string AllTimePeakFormatted
        {
            get => _allTimePeakFormatted;
            private set => SetProperty(ref _allTimePeakFormatted, value);
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

        public string UploadDownloadRatioFormatted
        {
            get => _uploadDownloadRatioFormatted;
            private set => SetProperty(ref _uploadDownloadRatioFormatted, value);
        }

        public string ActiveRateFormatted
        {
            get => _activeRateFormatted;
            private set => SetProperty(ref _activeRateFormatted, value);
        }

        public string CurrentStreakFormatted
        {
            get => _currentStreakFormatted;
            private set => SetProperty(ref _currentStreakFormatted, value);
        }

        public string LongestStreakFormatted
        {
            get => _longestStreakFormatted;
            private set => SetProperty(ref _longestStreakFormatted, value);
        }

        public string QuietDaysFormatted
        {
            get => _quietDaysFormatted;
            private set => SetProperty(ref _quietDaysFormatted, value);
        }

        public string RecentActiveDateFormatted
        {
            get => _recentActiveDateFormatted;
            private set => SetProperty(ref _recentActiveDateFormatted, value);
        }

        public string PersonaTitle
        {
            get => _personaTitle;
            private set => SetProperty(ref _personaTitle, value);
        }

        public string PersonaSubtitle
        {
            get => _personaSubtitle;
            private set => SetProperty(ref _personaSubtitle, value);
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
            var allRecords = TrafficHistoryService.Instance.GetRecords() ?? new List<DailyTrafficRecord>();
            var today = DateTime.Now.Date;
            var loc = LocalizationService.Instance;

            List<DailyTrafficRecord> filtered;
            switch (_selectedRange)
            {
                case "week":
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
                default:
                    PeriodLabel = loc.Get("Stats.PeriodToday");
                    filtered = FilterByRange(allRecords, today, today);
                    break;
            }

            long rangeDown = filtered.Sum(r => r.DownloadBytes);
            long rangeUp = filtered.Sum(r => r.UploadBytes);
            long rangeTotal = rangeDown + rangeUp;
            TotalDownloadFormatted = FormatUsageString(rangeDown);
            TotalUploadFormatted = FormatUsageString(rangeUp);
            RangeTotalFormatted = FormatUsageString(rangeTotal);

            RefreshSummary(allRecords, today, loc);
            RefreshDailyRecords(filtered, rangeTotal);
        }

        private void RefreshSummary(List<DailyTrafficRecord> allRecords, DateTime today, LocalizationService loc)
        {
            var byDate = allRecords
                .GroupBy(r => r.Date ?? string.Empty)
                .ToDictionary(g => g.Key, g => new DailyTrafficRecord
                {
                    Date = g.Key,
                    DownloadBytes = g.Sum(x => x.DownloadBytes),
                    UploadBytes = g.Sum(x => x.UploadBytes)
                });

            var recent7 = BuildRecentRecords(byDate, today, 7);
            var recent30 = BuildRecentRecords(byDate, today, 30);
            long allDown = allRecords.Sum(r => r.DownloadBytes);
            long allUp = allRecords.Sum(r => r.UploadBytes);
            long allTotal = allDown + allUp;
            int recordDays = allRecords.Count;
            int activeDays = allRecords.Count(r => TotalBytes(r) > 0);
            long todayTotal = TotalBytes(recent7.LastOrDefault());
            long yesterdayTotal = recent7.Count >= 2 ? TotalBytes(recent7[recent7.Count - 2]) : 0;
            long last7Down = recent7.Sum(r => r.DownloadBytes);
            long last7Up = recent7.Sum(r => r.UploadBytes);
            long last7Total = last7Down + last7Up;
            long last30Total = recent30.Sum(TotalBytes);
            int quietDays = recent7.Count(r => TotalBytes(r) == 0);
            double downloadShare = last7Total > 0 ? (double)last7Down / last7Total : 0;
            double uploadShare = last7Total > 0 ? (double)last7Up / last7Total : 0;
            double activeRate = recordDays > 0 ? (double)activeDays / recordDays : 0;

            AllTimeDownloadFormatted = FormatUsageString(allDown);
            AllTimeUploadFormatted = FormatUsageString(allUp);
            TotalTrafficFormatted = FormatUsageString(allTotal);
            TodayTrafficFormatted = FormatUsageString(todayTotal);
            YesterdayTrafficFormatted = FormatUsageString(yesterdayTotal);
            RecordDaysFormatted = FormatDays(recordDays, loc);
            ActiveDaysFormatted = FormatDays(activeDays, loc);
            Last7TotalFormatted = FormatUsageString(last7Total);
            Last7AverageFormatted = FormatUsageString(last7Total / 7);
            Last30TotalFormatted = FormatUsageString(last30Total);
            Last30AverageFormatted = FormatUsageString(last30Total / 30);
            HistoryAverageFormatted = FormatUsageString(recordDays > 0 ? allTotal / recordDays : 0);
            ActiveDayAverageFormatted = FormatUsageString(activeDays > 0 ? allTotal / activeDays : 0);
            DayOverDayFormatted = FormatDayOverDay(todayTotal, yesterdayTotal, loc);
            DownloadShareFormatted = FormatPercent(downloadShare);
            UploadShareFormatted = FormatPercent(uploadShare);
            UploadDownloadRatioFormatted = FormatRatio(last7Up, last7Down);
            ActiveRateFormatted = FormatPercent(activeRate);
            QuietDaysFormatted = FormatDays(quietDays, loc);

            var activeDates = new HashSet<string>(allRecords.Where(r => TotalBytes(r) > 0).Select(r => r.Date));
            CurrentStreakFormatted = FormatDays(CurrentStreak(activeDates, today), loc);
            LongestStreakFormatted = FormatDays(LongestStreak(allRecords), loc);

            var last7Peak = PeakRecord(recent7);
            var allTimePeak = PeakRecord(allRecords);
            Last7PeakFormatted = FormatPeak(last7Peak, loc);
            AllTimePeakFormatted = FormatPeak(allTimePeak, loc);
            RecentActiveDateFormatted = FormatDate(
                allRecords.Where(r => TotalBytes(r) > 0)
                    .Select(r => ParseDate(r.Date))
                    .Where(d => d.HasValue)
                    .Select(d => d.Value)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max(),
                loc);
            PersonaTitle = BuildPersonaTitle(last7Down, last7Up, last7Total, loc);
            PersonaSubtitle = loc.Format("Stats.PersonaSubtitle", FormatUsageString(last7Total));

            EmptyVisibility = allTotal > 0 ? Visibility.Collapsed : Visibility.Visible;
            RecordsVisibility = allTotal > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshDailyRecords(List<DailyTrafficRecord> filtered, long rangeTotal)
        {
            long maxBytes = filtered.Count > 0 ? filtered.Max(r => Math.Max(r.DownloadBytes, r.UploadBytes)) : 0;
            long maxTotal = filtered.Count > 0 ? filtered.Max(TotalBytes) : 0;
            var displayRecords = new ObservableCollection<DailyDisplayRecord>();

            foreach (var r in filtered.OrderBy(r => r.Date))
            {
                var total = TotalBytes(r);
                displayRecords.Add(new DailyDisplayRecord
                {
                    Date = ShortDate(r.Date),
                    DownloadBytes = r.DownloadBytes,
                    UploadBytes = r.UploadBytes,
                    DownloadFormatted = FormatUsageString(r.DownloadBytes),
                    UploadFormatted = FormatUsageString(r.UploadBytes),
                    TotalFormatted = FormatUsageString(total),
                    ShareFormatted = rangeTotal > 0 ? FormatPercent((double)total / rangeTotal) : "0%",
                    DownloadRatio = maxBytes > 0 ? (double)r.DownloadBytes / maxBytes : 0,
                    UploadRatio = maxBytes > 0 ? (double)r.UploadBytes / maxBytes : 0,
                    TotalRatio = maxTotal > 0 ? (double)total / maxTotal : 0
                });
            }

            DailyRecords = displayRecords;
        }

        private static List<DailyTrafficRecord> BuildRecentRecords(Dictionary<string, DailyTrafficRecord> byDate, DateTime today, int days)
        {
            var records = new List<DailyTrafficRecord>();
            for (int i = days - 1; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var key = date.ToString("yyyy-MM-dd");
                DailyTrafficRecord record;
                records.Add(byDate.TryGetValue(key, out record)
                    ? record
                    : new DailyTrafficRecord { Date = key });
            }

            return records;
        }

        private static long TotalBytes(DailyTrafficRecord record)
        {
            return record == null ? 0 : record.DownloadBytes + record.UploadBytes;
        }

        private static string FormatUsageString(long bytes)
        {
            var fmt = FormatHelper.FormatUsage(bytes);
            return fmt.Num + " " + fmt.Unit;
        }

        private static string FormatPercent(double value)
        {
            var clamped = Math.Max(0, Math.Min(1, value));
            return (clamped * 100).ToString("F0") + "%";
        }

        private static string FormatRatio(long upload, long download)
        {
            if (upload == 0 && download == 0) return "0.00x";
            if (download == 0) return "N/A";
            return ((double)upload / download).ToString("F2") + "x";
        }

        private static string FormatDays(int days, LocalizationService loc)
        {
            return loc.Format("Stats.ValueDays", days);
        }

        private static string FormatDayOverDay(long todayTotal, long yesterdayTotal, LocalizationService loc)
        {
            if (todayTotal == yesterdayTotal) return loc.Get("Stats.DayOverDayEven");
            if (todayTotal > yesterdayTotal) return "+" + FormatUsageString(todayTotal - yesterdayTotal);
            return "-" + FormatUsageString(yesterdayTotal - todayTotal);
        }

        private static string FormatPeak(DailyTrafficRecord record, LocalizationService loc)
        {
            if (record == null) return loc.Get("Stats.ValueNone");
            return FormatUsageString(TotalBytes(record)) + " / " + FormatDate(ParseDate(record.Date), loc);
        }

        private static string FormatDate(DateTime? date, LocalizationService loc)
        {
            if (!date.HasValue || date.Value == DateTime.MinValue) return loc.Get("Stats.ValueNone");
            return date.Value.ToString("MM/dd");
        }

        private static string ShortDate(string date)
        {
            DateTime dt;
            if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                return dt.ToString("M/d");
            }

            return date;
        }

        private static DateTime? ParseDate(string date)
        {
            DateTime dt;
            if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                return dt;
            }

            return null;
        }

        private static DailyTrafficRecord PeakRecord(IEnumerable<DailyTrafficRecord> records)
        {
            return records
                .Where(r => TotalBytes(r) > 0)
                .OrderByDescending(TotalBytes)
                .FirstOrDefault();
        }

        private static int CurrentStreak(HashSet<string> activeDates, DateTime today)
        {
            var streak = 0;
            var date = today;
            while (true)
            {
                if (!activeDates.Contains(date.ToString("yyyy-MM-dd")))
                    return streak;

                streak++;
                date = date.AddDays(-1);
            }
        }

        private static int LongestStreak(List<DailyTrafficRecord> records)
        {
            var dates = records
                .Where(r => TotalBytes(r) > 0)
                .Select(r => ParseDate(r.Date))
                .Where(d => d.HasValue)
                .Select(d => d.Value.Date)
                .OrderBy(d => d)
                .ToList();

            if (dates.Count == 0) return 0;

            var longest = 1;
            var current = 1;
            for (int i = 1; i < dates.Count; i++)
            {
                var delta = (dates[i] - dates[i - 1]).Days;
                if (delta == 1)
                {
                    current++;
                }
                else if (delta > 1)
                {
                    current = 1;
                }

                longest = Math.Max(longest, current);
            }

            return longest;
        }

        private static string BuildPersonaTitle(long last7Down, long last7Up, long last7Total, LocalizationService loc)
        {
            if (last7Total <= 0)
            {
                return loc.Get("Stats.PersonaQuiet");
            }

            var totalGb = last7Total / 1073741824.0;
            string tierKey = totalGb >= 50
                ? "Stats.PersonaTierHeavy"
                : totalGb >= 10
                    ? "Stats.PersonaTierActive"
                    : "Stats.PersonaTierLight";

            string roleKey;
            if (last7Down == 0 && last7Up > 0)
            {
                roleKey = "Stats.PersonaRoleUploader";
            }
            else
            {
                var ratio = (double)last7Up / Math.Max(last7Down, 1);
                if (ratio >= 2)
                    roleKey = "Stats.PersonaRoleUploader";
                else if (ratio <= 0.5)
                    roleKey = "Stats.PersonaRoleDownloader";
                else
                    roleKey = "Stats.PersonaRoleBalanced";
            }

            return loc.Format("Stats.PersonaFormat", loc.Get(tierKey), loc.Get(roleKey));
        }

        private List<DailyTrafficRecord> FilterByRange(List<DailyTrafficRecord> records, DateTime start, DateTime end)
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
