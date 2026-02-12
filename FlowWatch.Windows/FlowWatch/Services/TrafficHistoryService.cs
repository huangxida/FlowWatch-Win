using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using FlowWatch.Models;

namespace FlowWatch.Services
{
    /// <summary>
    /// 流量历史记录服务，负责按日累计并持久化流量数据
    /// </summary>
    public class TrafficHistoryService
    {
        private static readonly Lazy<TrafficHistoryService> _instance = new Lazy<TrafficHistoryService>(() => new TrafficHistoryService());
        public static TrafficHistoryService Instance => _instance.Value;

        private const string DateFormat = "yyyy-MM-dd";

        private readonly string _dataDir;
        private readonly string _dataPath;
        private readonly object _saveLock = new object();

        private TrafficHistory _history;
        private DailyTrafficRecord _todayRecord;
        private DateTime _currentDate;

        private long _lastTotalDownload;
        private long _lastTotalUpload;
        private bool _hasBaseline;

        private DispatcherTimer _saveTimer;

        private TrafficHistoryService()
        {
            _dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowWatch");
            _dataPath = Path.Combine(_dataDir, "traffic_history.json");
        }

        public void Start()
        {
            Load();

            _currentDate = DateTime.Now.Date;
            _todayRecord = _history.Records.FirstOrDefault(r => r.Date == FormatDate(_currentDate));
            if (_todayRecord == null)
            {
                _todayRecord = new DailyTrafficRecord { Date = FormatDate(_currentDate) };
                _history.Records.Add(_todayRecord);
            }

            _hasBaseline = false;

            NetworkMonitorService.Instance.StatsUpdated += OnStatsUpdated;

            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _saveTimer.Tick += (s, e) => Save();
            _saveTimer.Start();
        }

        public void Stop()
        {
            NetworkMonitorService.Instance.StatsUpdated -= OnStatsUpdated;

            if (_saveTimer != null)
            {
                _saveTimer.Stop();
                _saveTimer = null;
            }

            Save();
        }

        private void OnStatsUpdated(NetworkStats stats)
        {
            var today = DateTime.Now.Date;

            // 跨天处理
            if (today != _currentDate)
            {
                Save();
                _currentDate = today;
                _todayRecord = _history.Records.FirstOrDefault(r => r.Date == FormatDate(_currentDate));
                if (_todayRecord == null)
                {
                    _todayRecord = new DailyTrafficRecord { Date = FormatDate(_currentDate) };
                    _history.Records.Add(_todayRecord);
                }
                // 跨天后重置基线，下次事件开始累加
                _hasBaseline = false;
            }

            if (!_hasBaseline)
            {
                // 首次事件仅记录快照，不累加
                _lastTotalDownload = stats.TotalDownload;
                _lastTotalUpload = stats.TotalUpload;
                _hasBaseline = true;
                return;
            }

            // 计算增量，Math.Max(0, delta) 容错基线重置
            var deltaDown = Math.Max(0, stats.TotalDownload - _lastTotalDownload);
            var deltaUp = Math.Max(0, stats.TotalUpload - _lastTotalUpload);

            _todayRecord.DownloadBytes += deltaDown;
            _todayRecord.UploadBytes += deltaUp;

            _lastTotalDownload = stats.TotalDownload;
            _lastTotalUpload = stats.TotalUpload;
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_dataPath))
                {
                    var json = File.ReadAllText(_dataPath);
                    _history = JsonSerializer.Deserialize<TrafficHistory>(json) ?? new TrafficHistory();
                }
                else
                {
                    _history = new TrafficHistory();
                }
            }
            catch (JsonException)
            {
                // JSON 反序列化失败，保留损坏的文件作为备份
                PreserveCorruptedFile();
                _history = new TrafficHistory();
            }
            catch (IOException)
            {
                // 文件 I/O 错误
                _history = new TrafficHistory();
            }
            catch (UnauthorizedAccessException)
            {
                // 权限不足
                _history = new TrafficHistory();
            }
        }

        private void PreserveCorruptedFile()
        {
            try
            {
                if (File.Exists(_dataPath))
                {
                    var corruptedPath = _dataPath + ".corrupted";
                    // 如果已有 .corrupted 文件，先删除
                    if (File.Exists(corruptedPath))
                    {
                        File.Delete(corruptedPath);
                    }
                    File.Move(_dataPath, corruptedPath);
                }
            }
            catch
            {
                // 备份失败时静默处理，避免影响程序启动
            }
        }

        private void Save()
        {
            lock (_saveLock)
            {
                try
                {
                    if (!Directory.Exists(_dataDir))
                        Directory.CreateDirectory(_dataDir);

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(_history, options);

                    var tempPath = _dataPath + ".tmp";
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(_dataPath))
                    {
                        var backupPath = _dataPath + ".bak";
                        File.Replace(tempPath, _dataPath, backupPath);
                    }
                    else
                    {
                        File.Move(tempPath, _dataPath);
                    }
                }
                catch (IOException)
                {
                    // 文件 I/O 错误，静默失败，崩溃最多丢 60 秒数据
                }
                catch (UnauthorizedAccessException)
                {
                    // 权限不足
                }
                catch (JsonException)
                {
                    // JSON 序列化错误（理论上不应发生）
                }
            }
        }

        /// <summary>
        /// 格式化日期为 yyyy-MM-dd 格式
        /// </summary>
        private string FormatDate(DateTime date) => date.ToString(DateFormat);
    }
}
