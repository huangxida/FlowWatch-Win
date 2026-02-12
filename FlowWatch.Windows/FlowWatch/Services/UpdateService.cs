using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowWatch.Models;

namespace FlowWatch.Services
{
    public class UpdateService
    {
        private static readonly Lazy<UpdateService> _instance = new Lazy<UpdateService>(() => new UpdateService());
        public static UpdateService Instance => _instance.Value;

        private const string GitHubApiUrl = "https://api.github.com/repos/huangxida/FlowWatch-Win/releases/latest";
        private const string AssetPattern = "-win-x64.zip";
        private const int CheckIntervalMs = 24 * 60 * 60 * 1000; // 24 hours
        private const int FirstCheckDelayMs = 5 * 60 * 1000; // 5 minutes

        private readonly HttpClient _httpClient;
        private readonly string _updatesDir;
        private Timer _checkTimer;
        private bool _isChecking;

        public event Action<UpdateInfo> UpdateAvailable;
        public event Action<double> DownloadProgressChanged;

        private UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FlowWatch-UpdateChecker");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            _updatesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowWatch", "updates");
        }

        public Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        public void StartAutoCheck()
        {
            if (_checkTimer != null) return;

            var settings = SettingsService.Instance.Settings;
            if (!settings.AutoCheckUpdate) return;

            // Calculate first check delay
            int delay = FirstCheckDelayMs;
            if (!string.IsNullOrEmpty(settings.LastUpdateCheck))
            {
                if (DateTime.TryParse(settings.LastUpdateCheck, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var lastCheck))
                {
                    var elapsed = DateTime.UtcNow - lastCheck;
                    if (elapsed.TotalMilliseconds < CheckIntervalMs)
                    {
                        delay = Math.Max(FirstCheckDelayMs, (int)(CheckIntervalMs - elapsed.TotalMilliseconds));
                    }
                }
            }

            _checkTimer = new Timer(OnAutoCheckTimer, null, delay, CheckIntervalMs);
            LogService.Info($"自动更新检查已启动，首次检查延迟 {delay / 1000} 秒");
        }

        public void StopAutoCheck()
        {
            _checkTimer?.Dispose();
            _checkTimer = null;
            LogService.Info("自动更新检查已停止");
        }

        private async void OnAutoCheckTimer(object state)
        {
            try
            {
                var info = await CheckForUpdateAsync();
                if (info != null)
                {
                    var settings = SettingsService.Instance.Settings;
                    if (settings.SkippedVersion == info.TagName)
                    {
                        LogService.Info($"版本 {info.TagName} 已被用户跳过");
                        return;
                    }

                    UpdateAvailable?.Invoke(info);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("自动检查更新失败", ex);
            }
        }

        public async Task<UpdateInfo> CheckForUpdateAsync()
        {
            if (_isChecking) return null;
            _isChecking = true;

            try
            {
                LogService.Info("开始检查更新...");
                var response = await _httpClient.GetAsync(GitHubApiUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    LogService.Warn("GitHub API 返回空数据");
                    return null;
                }

                // Update last check time
                SettingsService.Instance.Update(s =>
                    s.LastUpdateCheck = DateTime.UtcNow.ToString("o"));

                // Parse version from tag (v1.2.3 -> 1.2.3)
                var tagVersion = release.TagName.TrimStart('v');
                if (!Version.TryParse(tagVersion, out var remoteVersion))
                {
                    LogService.Warn($"无法解析版本号: {release.TagName}");
                    return null;
                }

                var currentVersion = GetCurrentVersion();
                // Compare major.minor.build only (ignore revision)
                var currentComparable = new Version(currentVersion.Major, currentVersion.Minor,
                    Math.Max(0, currentVersion.Build));
                var remoteComparable = new Version(remoteVersion.Major, remoteVersion.Minor,
                    Math.Max(0, remoteVersion.Build));

                if (remoteComparable <= currentComparable)
                {
                    LogService.Info($"当前版本 {currentComparable} 已是最新（远程 {remoteComparable}）");
                    return null;
                }

                // Find the win-x64 zip asset
                var asset = release.Assets?.FirstOrDefault(a =>
                    a.Name != null && a.Name.EndsWith(AssetPattern, StringComparison.OrdinalIgnoreCase));

                if (asset == null)
                {
                    LogService.Warn("未找到匹配的发布资产");
                    return null;
                }

                var info = new UpdateInfo
                {
                    Version = remoteVersion,
                    TagName = release.TagName,
                    ReleaseNotes = release.Body,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    FileName = asset.Name,
                    FileSize = asset.Size,
                    PublishedAt = release.PublishedAt
                };

                LogService.Info($"发现新版本: {info.TagName} (当前: {currentComparable})");
                return info;
            }
            catch (Exception ex)
            {
                LogService.Error("检查更新失败", ex);
                throw;
            }
            finally
            {
                _isChecking = false;
            }
        }

        public async Task<string> DownloadUpdateAsync(UpdateInfo info, CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(_updatesDir))
                Directory.CreateDirectory(_updatesDir);

            var zipPath = Path.Combine(_updatesDir, info.FileName);

            // Clean up previous downloads
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            LogService.Info($"开始下载更新: {info.DownloadUrl}");

            using (var response = await _httpClient.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? info.FileSize;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long bytesRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                        bytesRead += read;

                        if (totalBytes > 0)
                        {
                            var progress = (double)bytesRead / totalBytes;
                            DownloadProgressChanged?.Invoke(progress);
                        }
                    }
                }
            }

            LogService.Info($"下载完成: {zipPath}");
            return zipPath;
        }

        public void LaunchUpdateAndExit(string zipPath)
        {
            var scriptPath = GenerateUpdateScript(zipPath);
            LogService.Info($"启动更新脚本: {scriptPath}");

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);
        }

        private string GenerateUpdateScript(string zipPath)
        {
            var appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var exePath = Assembly.GetExecutingAssembly().Location;
            var backupDir = Path.Combine(_updatesDir, "backup");
            var extractDir = Path.Combine(_updatesDir, "extracted");
            var logPath = Path.Combine(_updatesDir, "update.log");
            var scriptPath = Path.Combine(_updatesDir, "update.ps1");

            var script = new StringBuilder();
            script.AppendLine("# FlowWatch Auto-Update Script");
            script.AppendLine($"$ErrorActionPreference = 'Stop'");
            script.AppendLine();
            script.AppendLine($"$appDir = '{EscapePS(appDir)}'");
            script.AppendLine($"$exePath = '{EscapePS(exePath)}'");
            script.AppendLine($"$zipPath = '{EscapePS(zipPath)}'");
            script.AppendLine($"$backupDir = '{EscapePS(backupDir)}'");
            script.AppendLine($"$extractDir = '{EscapePS(extractDir)}'");
            script.AppendLine($"$logPath = '{EscapePS(logPath)}'");
            script.AppendLine();
            script.AppendLine(@"function Log($msg) {
    $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    ""[$ts] $msg"" | Out-File -Append -FilePath $logPath -Encoding utf8
}");
            script.AppendLine();
            script.AppendLine(@"try {
    Log 'Update script started'

    # Wait for FlowWatch to exit (max 30 seconds)
    $timeout = 30
    $waited = 0
    while ($waited -lt $timeout) {
        $procs = Get-Process -Name 'FlowWatch' -ErrorAction SilentlyContinue
        if (-not $procs) { break }
        Start-Sleep -Seconds 1
        $waited++
    }
    if ($waited -ge $timeout) {
        Log 'Timeout waiting for FlowWatch to exit, force killing...'
        Get-Process -Name 'FlowWatch' -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
    Log 'FlowWatch process exited'

    # Backup current files
    if (Test-Path $backupDir) { Remove-Item $backupDir -Recurse -Force }
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Get-ChildItem -Path $appDir -File | ForEach-Object {
        Copy-Item $_.FullName -Destination $backupDir -Force
    }
    Log 'Backup completed'

    # Extract ZIP to temp directory
    if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
    New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force
    Log 'ZIP extracted'

    # Find the actual files (may be in a subdirectory)
    $sourceDir = $extractDir
    $subDirs = Get-ChildItem -Path $extractDir -Directory
    if ($subDirs.Count -eq 1) {
        $sourceDir = $subDirs[0].FullName
    }

    # Copy new files to app directory
    Get-ChildItem -Path $sourceDir -File | ForEach-Object {
        Copy-Item $_.FullName -Destination $appDir -Force
    }
    Log 'Files updated'

    # Clean up temp files
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue
    Log 'Cleanup completed'

    # Restart FlowWatch
    Log 'Restarting FlowWatch...'
    Start-Process -FilePath $exePath
    Log 'Update completed successfully'

} catch {
    Log ""Update failed: $_""

    # Rollback from backup
    if (Test-Path $backupDir) {
        Log 'Rolling back from backup...'
        try {
            Get-ChildItem -Path $backupDir -File | ForEach-Object {
                Copy-Item $_.FullName -Destination $appDir -Force
            }
            Log 'Rollback completed'
            Start-Process -FilePath $exePath
            Log 'Restarted old version'
        } catch {
            Log ""Rollback failed: $_""
        }
    }
}");

            File.WriteAllText(scriptPath, script.ToString(), Encoding.UTF8);
            return scriptPath;
        }

        private static string EscapePS(string path)
        {
            return path.Replace("'", "''");
        }
    }
}
