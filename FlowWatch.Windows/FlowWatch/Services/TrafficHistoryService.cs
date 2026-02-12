using System;
using System.Collections.Generic;
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
            LogService.Info("TrafficHistoryService.Start() 开始");
            Load();
            LogService.Info($"历史数据加载完成，共 {_history.Records.Count} 条记录");

            _currentDate = DateTime.Now.Date;
            _todayRecord = _history.Records.FirstOrDefault(r => r.Date == FormatDate(_currentDate));
            if (_todayRecord == null)
            {
                _todayRecord = new DailyTrafficRecord { Date = FormatDate(_currentDate) };
                _history.Records.Add(_todayRecord);
                LogService.Info($"创建今日记录: {_todayRecord.Date}");
            }
            else
            {
                LogService.Info($"找到今日记录: {_todayRecord.Date}, 下载={_todayRecord.DownloadBytes}, 上传={_todayRecord.UploadBytes}");
            }

            _hasBaseline = false;

            // 将历史累计数据设为偏移量，使 UI 启动时即显示当天已有流量
            NetworkMonitorService.Instance.SetTrafficOffset(
                _todayRecord.DownloadBytes,
                _todayRecord.UploadBytes);

            NetworkMonitorService.Instance.StatsUpdated += OnStatsUpdated;

            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _saveTimer.Tick += (s, e) => Save();
            _saveTimer.Start();
            LogService.Info("TrafficHistoryService.Start() 完成，定时保存已启动");
        }

        public void Stop()
        {
            LogService.Info("TrafficHistoryService.Stop() 开始");
            NetworkMonitorService.Instance.StatsUpdated -= OnStatsUpdated;

            if (_saveTimer != null)
            {
                _saveTimer.Stop();
                _saveTimer = null;
            }

            Save();
            LogService.Info("TrafficHistoryService.Stop() 完成");
        }

        private void OnStatsUpdated(NetworkStats stats)
        {
            var today = DateTime.Now.Date;

            // 跨天处理
            if (today != _currentDate)
            {
                LogService.Info($"检测到跨天: {FormatDate(_currentDate)} -> {FormatDate(today)}");
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
            LogService.Info($"加载流量历史文件: {_dataPath}");
            try
            {
                if (File.Exists(_dataPath))
                {
                    var json = File.ReadAllText(_dataPath);
                    LogService.Info($"文件读取成功，大小={json.Length} 字符");
                    _history = JsonSerializer.Deserialize<TrafficHistory>(json) ?? new TrafficHistory();
                    LogService.Info($"反序列化成功，记录数={_history.Records.Count}");
                }
                else
                {
                    LogService.Info("历史文件不存在，创建空记录");
                    _history = new TrafficHistory();
                }
            }
            catch (JsonException ex)
            {
                LogService.Error("JSON 反序列化失败", ex);
                PreserveCorruptedFile();
                _history = new TrafficHistory();
            }
            catch (IOException ex)
            {
                LogService.Error("文件 I/O 错误", ex);
                _history = new TrafficHistory();
            }
            catch (UnauthorizedAccessException ex)
            {
                LogService.Error("权限不足", ex);
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
                catch (IOException ex)
                {
                    LogService.Error("保存流量数据 I/O 错误", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogService.Error("保存流量数据权限不足", ex);
                }
                catch (JsonException ex)
                {
                    LogService.Error("保存流量数据序列化错误", ex);
                }
            }
        }

        public List<DailyTrafficRecord> GetRecords()
        {
            if (_history == null)
                return new List<DailyTrafficRecord>();
            return _history.Records.ToList();
        }

        public void ResetToday()
        {
            LogService.Info("重置今日流量");
            if (_todayRecord != null)
            {
                _todayRecord.DownloadBytes = 0;
                _todayRecord.UploadBytes = 0;
            }
            _hasBaseline = false;
            NetworkMonitorService.Instance.ResetTraffic();
            Save();
        }

        public void ResetAll()
        {
            LogService.Info("重置全部流量历史");
            _history = new TrafficHistory();
            _todayRecord = new DailyTrafficRecord { Date = FormatDate(_currentDate) };
            _history.Records.Add(_todayRecord);
            _hasBaseline = false;
            NetworkMonitorService.Instance.ResetTraffic();
            Save();
        }

        /// <summary>
        /// 格式化日期为 yyyy-MM-dd 格式
        /// </summary>
        private string FormatDate(DateTime date) => date.ToString(DateFormat);
    }
}
