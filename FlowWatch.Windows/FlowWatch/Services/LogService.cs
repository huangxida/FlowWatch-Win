using System;
using System.IO;
using System.Text;

namespace FlowWatch.Services
{
    /// <summary>
    /// 简易文件日志服务，输出到 %LOCALAPPDATA%\FlowWatch\logs\
    /// </summary>
    public static class LogService
    {
        private static readonly object _lock = new object();
        private static readonly string _logDir;
        private static readonly string _logPath;
        private static bool _initialized;

        static LogService()
        {
            _logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowWatch", "logs");
            _logPath = Path.Combine(_logDir, $"flowwatch_{DateTime.Now:yyyyMMdd}.log");
        }

        private static void EnsureDirectory()
        {
            if (_initialized) return;
            try
            {
                if (!Directory.Exists(_logDir))
                    Directory.CreateDirectory(_logDir);
                _initialized = true;
            }
            catch
            {
                // 无法创建目录时静默失败
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", $"{message}\n  Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Write("ERROR", $"  InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n  StackTrace: {ex.InnerException.StackTrace}");
            }
        }

        public static void Debug(string message)
        {
#if DEBUG
            Write("DEBUG", message);
#endif
        }

        private static void Write(string level, string message)
        {
            lock (_lock)
            {
                try
                {
                    EnsureDirectory();
                    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logPath, line, Encoding.UTF8);
                }
                catch
                {
                    // 日志写入失败不应影响主程序
                }
            }
        }

        /// <summary>
        /// 清理 7 天前的日志文件
        /// </summary>
        public static void CleanOldLogs()
        {
            try
            {
                if (!Directory.Exists(_logDir)) return;
                var cutoff = DateTime.Now.AddDays(-7);
                foreach (var file in Directory.GetFiles(_logDir, "flowwatch_*.log"))
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // 清理失败不影响运行
            }
        }
    }
}
