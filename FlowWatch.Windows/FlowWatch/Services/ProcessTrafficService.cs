using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using FlowWatch.Models;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace FlowWatch.Services
{
    public class ProcessTrafficService
    {
        private static readonly Lazy<ProcessTrafficService> _instance =
            new Lazy<ProcessTrafficService>(() => new ProcessTrafficService());
        public static ProcessTrafficService Instance => _instance.Value;

        private const string SessionName = "FlowWatch-KernelNetTrace";
        private const string DateFormat = "yyyy-MM-dd";
        private const int FlushIntervalSeconds = 30;
        private const int MaxHistoryDays = 90;

        private readonly string _dataDir;
        private readonly string _dataPath;
        private readonly object _saveLock = new object();

        // PID → 进程名缓存
        private readonly ConcurrentDictionary<int, string> _pidNameCache =
            new ConcurrentDictionary<int, string>();

        // 进程名 → exe 完整路径缓存（供图标提取）
        private readonly ConcurrentDictionary<string, string> _exePathCache =
            new ConcurrentDictionary<string, string>();

        // 进程名 → [下载增量, 上传增量]（用于持久化累计）
        private readonly ConcurrentDictionary<string, long[]> _deltas =
            new ConcurrentDictionary<string, long[]>();

        // 进程名 → [下载增量, 上传增量]（用于实时速度计算，独立于持久化）
        private readonly ConcurrentDictionary<string, long[]> _realtimeDeltas =
            new ConcurrentDictionary<string, long[]>();
        private DateTime _lastRealtimeSnapshot = DateTime.UtcNow;

        private AppTrafficHistory _history;
        private TraceEventSession _session;
        private Thread _etwThread;
        private DispatcherTimer _flushTimer;
        private volatile bool _running;

        private ProcessTrafficService()
        {
            _dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowWatch");
            _dataPath = Path.Combine(_dataDir, "app_traffic_history.json");
        }

        public void Start()
        {
            LogService.Info("ProcessTrafficService.Start() 开始");

            Load();
            CleanOldRecords();
            LogService.Info($"应用流量历史加载完成，共 {_history.Records.Count} 天记录");

            _running = true;

            // 清理可能残留的同名 ETW 会话
            try
            {
                TraceEventSession.GetActiveSession(SessionName)?.Stop(true);
            }
            catch
            {
                // 无残留会话，忽略
            }

            // 后台线程启动 ETW 内核会话
            _etwThread = new Thread(EtwWorker)
            {
                IsBackground = true,
                Name = "FlowWatch-ETW"
            };
            _etwThread.Start();

            // UI 线程定时器，刷新增量到持久化数据
            _flushTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(FlushIntervalSeconds)
            };
            _flushTimer.Tick += (s, e) => FlushDeltas();
            _flushTimer.Start();

            LogService.Info("ProcessTrafficService.Start() 完成");
        }

        public void Stop()
        {
            LogService.Info("ProcessTrafficService.Stop() 开始");
            _running = false;

            if (_flushTimer != null)
            {
                _flushTimer.Stop();
                _flushTimer = null;
            }

            try
            {
                _session?.Stop();
            }
            catch (Exception ex)
            {
                LogService.Error("停止 ETW 会话失败", ex);
            }

            // 最终刷新一次
            FlushDeltas();

            LogService.Info("ProcessTrafficService.Stop() 完成");
        }

        public List<DailyAppTrafficRecord> GetRecords()
        {
            if (_history == null)
                return new List<DailyAppTrafficRecord>();
            return _history.Records.ToList();
        }

        private void EtwWorker()
        {
            try
            {
                using (_session = new TraceEventSession(SessionName))
                {
                    _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

                    var parser = _session.Source.Kernel;

                    // TCP
                    parser.TcpIpRecv += (data) =>
                    {
                        if (!_running) return;
                        Accumulate(data.ProcessID, data.size, isDownload: true);
                    };
                    parser.TcpIpSend += (data) =>
                    {
                        if (!_running) return;
                        Accumulate(data.ProcessID, data.size, isDownload: false);
                    };
                    parser.TcpIpRecvIPV6 += (data) =>
                    {
                        if (!_running) return;
                        Accumulate(data.ProcessID, data.size, isDownload: true);
                    };
                    parser.TcpIpSendIPV6 += (data) =>
                    {
                        if (!_running) return;
                        Accumulate(data.ProcessID, data.size, isDownload: false);
                    };

                    // UDP
                    parser.UdpIpRecv += (data) =>
                    {
                        if (!_running) return;
                        Accumulate(data.ProcessID, data.size, isDownload: true);
                    };
                    parser.UdpIpSend += (data) =>
                    {
                        if (!_running) return;
                        Accumulate(data.ProcessID, data.size, isDownload: false);
                    };
                    parser.UdpIpRecvIPV6 += (data) =>
                    {
                        if (!_running) return;
                        Accumulate(data.ProcessID, data.size, isDownload: true);
                    };
                    parser.UdpIpSendIPV6 += (data) =>
                    {
                        if (!_running) return;
                        Accumulate(data.ProcessID, data.size, isDownload: false);
                    };

                    LogService.Info("ETW 内核网络会话已启动");
                    _session.Source.Process(); // 阻塞直到 Stop()
                }
            }
            catch (Exception ex)
            {
                LogService.Error("ETW 会话异常（需要管理员权限）", ex);
            }
            finally
            {
                _session = null;
                LogService.Info("ETW 工作线程已退出");
            }
        }

        public string GetExePath(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return null;
            _exePathCache.TryGetValue(processName, out var path);
            return path;
        }

        private void Accumulate(int pid, int size, bool isDownload)
        {
            if (pid <= 0 || size <= 0) return;

            var name = _pidNameCache.GetOrAdd(pid, id =>
            {
                try
                {
                    var proc = Process.GetProcessById(id);
                    // 缓存 exe 路径
                    try
                    {
                        var exePath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                            _exePathCache.TryAdd(proc.ProcessName, exePath);
                    }
                    catch { }
                    return proc.ProcessName;
                }
                catch
                {
                    return null;
                }
            });

            if (string.IsNullOrEmpty(name)) return;

            var arr = _deltas.GetOrAdd(name, _ => new long[2]);
            if (isDownload)
                Interlocked.Add(ref arr[0], size);
            else
                Interlocked.Add(ref arr[1], size);

            // 同时写入实时速度缓冲区
            var rtArr = _realtimeDeltas.GetOrAdd(name, _ => new long[2]);
            if (isDownload)
                Interlocked.Add(ref rtArr[0], size);
            else
                Interlocked.Add(ref rtArr[1], size);
        }

        /// <summary>
        /// 获取自上次调用以来各进程的平均速度 (bytes/sec)
        /// 返回 Dictionary&lt;进程名, [下载速度, 上传速度]&gt;
        /// </summary>
        public Dictionary<string, double[]> GetRealtimeSpeeds()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRealtimeSnapshot).TotalSeconds;
            _lastRealtimeSnapshot = now;
            if (elapsed <= 0) elapsed = 1;

            var result = new Dictionary<string, double[]>();
            foreach (var key in _realtimeDeltas.Keys.ToArray())
            {
                if (_realtimeDeltas.TryGetValue(key, out var arr))
                {
                    var down = Interlocked.Exchange(ref arr[0], 0);
                    var up = Interlocked.Exchange(ref arr[1], 0);
                    if (down > 0 || up > 0)
                        result[key] = new double[] { down / elapsed, up / elapsed };
                }
            }
            return result;
        }

        public void FlushDeltas()
        {
            var today = DateTime.Now.Date;
            var dateStr = today.ToString(DateFormat);

            // 交换出所有增量
            var snapshot = new Dictionary<string, long[]>();
            foreach (var key in _deltas.Keys.ToArray())
            {
                if (_deltas.TryGetValue(key, out var arr))
                {
                    var down = Interlocked.Exchange(ref arr[0], 0);
                    var up = Interlocked.Exchange(ref arr[1], 0);
                    if (down > 0 || up > 0)
                        snapshot[key] = new long[] { down, up };
                }
            }

            if (snapshot.Count == 0) return;

            lock (_saveLock)
            {
                // 查找或创建今天的记录
                var todayRecord = _history.Records.FirstOrDefault(r => r.Date == dateStr);
                if (todayRecord == null)
                {
                    todayRecord = new DailyAppTrafficRecord { Date = dateStr };
                    _history.Records.Add(todayRecord);
                }

                // 累加到今天的应用记录
                foreach (var kv in snapshot)
                {
                    var appRecord = todayRecord.Apps.FirstOrDefault(a => a.ProcessName == kv.Key);
                    if (appRecord == null)
                    {
                        appRecord = new AppTrafficRecord { ProcessName = kv.Key };
                        todayRecord.Apps.Add(appRecord);
                    }
                    appRecord.DownloadBytes += kv.Value[0];
                    appRecord.UploadBytes += kv.Value[1];
                }

                Save();
            }
        }

        private void Load()
        {
            LogService.Info($"加载应用流量历史文件: {_dataPath}");
            try
            {
                if (File.Exists(_dataPath))
                {
                    var json = File.ReadAllText(_dataPath);
                    _history = JsonSerializer.Deserialize<AppTrafficHistory>(json) ?? new AppTrafficHistory();
                }
                else
                {
                    _history = new AppTrafficHistory();
                }
            }
            catch (JsonException ex)
            {
                LogService.Error("应用流量 JSON 反序列化失败", ex);
                PreserveCorruptedFile();
                _history = new AppTrafficHistory();
            }
            catch (IOException ex)
            {
                LogService.Error("应用流量文件 I/O 错误", ex);
                _history = new AppTrafficHistory();
            }
            catch (UnauthorizedAccessException ex)
            {
                LogService.Error("应用流量文件权限不足", ex);
                _history = new AppTrafficHistory();
            }
        }

        private void PreserveCorruptedFile()
        {
            try
            {
                if (File.Exists(_dataPath))
                {
                    var corruptedPath = _dataPath + ".corrupted";
                    if (File.Exists(corruptedPath))
                        File.Delete(corruptedPath);
                    File.Move(_dataPath, corruptedPath);
                }
            }
            catch
            {
                // 静默处理
            }
        }

        private void Save()
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
                LogService.Error("保存应用流量数据 I/O 错误", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogService.Error("保存应用流量数据权限不足", ex);
            }
            catch (JsonException ex)
            {
                LogService.Error("保存应用流量数据序列化错误", ex);
            }
        }

        private void CleanOldRecords()
        {
            var cutoff = DateTime.Now.Date.AddDays(-MaxHistoryDays).ToString(DateFormat);
            _history.Records.RemoveAll(r =>
                string.Compare(r.Date, cutoff, StringComparison.Ordinal) < 0);
        }
    }
}
