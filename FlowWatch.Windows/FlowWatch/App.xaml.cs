using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using FlowWatch.Models;
using FlowWatch.Services;
using FlowWatch.Views;

namespace FlowWatch
{
    public partial class App : Application
    {
        private Mutex _mutex;
        private TaskbarIcon _trayIcon;
        private OverlayWindow _overlayWindow;
        private SettingsWindow _settingsWindow;
        private StatisticsWindow _statisticsWindow;
        private AppTrafficWindow _appTrafficWindow;
        private MenuItem _autoHideItem;
        private MenuItem _lockItem;
        private UpdateWindow _updateWindow;
        private UpdateInfo _pendingUpdateInfo;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global unhandled exception handlers
            DispatcherUnhandledException += (s, args) =>
            {
                LogService.Error("DispatcherUnhandledException", args.Exception);
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    LogService.Error("AppDomain.UnhandledException", ex);
            };

            LogService.Info("========== FlowWatch 启动 ==========");
            LogService.CleanOldLogs();

            try
            {
                // Single instance check
                bool createdNew;
                _mutex = new Mutex(true, "FlowWatch_SingleInstance", out createdNew);
                if (!createdNew)
                {
                    LogService.Warn("检测到已有实例运行，退出");
                    // Apply language before showing MessageBox
                    var tempSettings = SettingsService.Instance.Settings;
                    LocalizationService.Instance.ApplyLanguage(tempSettings.Language ?? "auto");
                    MessageBox.Show(
                        LocalizationService.Instance.Get("Common.AlreadyRunning"),
                        "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }

                // Initialize settings
                LogService.Info("初始化设置服务...");
                var settings = SettingsService.Instance.Settings;
                LogService.Info($"设置加载完成 (RefreshInterval={settings.RefreshInterval}, LockOnTop={settings.LockOnTop}, AutoHide={settings.AutoHide})");

                // Apply language
                LocalizationService.Instance.ApplyLanguage(settings.Language ?? "auto");
                LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

                // Apply auto-launch setting
                AutoLaunchService.SetAutoLaunch(settings.AutoLaunch);
                LogService.Info($"自启动设置: {settings.AutoLaunch}");

                // Create tray icon
                LogService.Info("创建托盘图标...");
                CreateTrayIcon();
                LogService.Info("托盘图标创建完成");

                // Create overlay window
                LogService.Info("创建悬浮窗...");
                _overlayWindow = new OverlayWindow();
                _overlayWindow.Show();
                LogService.Info("悬浮窗已显示");

                // Create settings window (hidden)
                LogService.Info("创建设置窗口...");
                _settingsWindow = new SettingsWindow();
                LogService.Info("设置窗口创建完成");

                // Start network monitoring
                LogService.Info("启动网络监控服务...");
                NetworkMonitorService.Instance.Start(settings.RefreshInterval);
                LogService.Info("网络监控服务已启动");

                // Start traffic history recording
                LogService.Info("启动流量历史服务...");
                TrafficHistoryService.Instance.Start();
                LogService.Info("流量历史服务已启动");

                // Create statistics window (hidden, after TrafficHistoryService started)
                LogService.Info("创建统计窗口...");
                _statisticsWindow = new StatisticsWindow();
                LogService.Info("统计窗口创建完成");

                // Start per-process traffic monitoring (ETW)
                LogService.Info("启动应用流量监控服务...");
                ProcessTrafficService.Instance.Start();
                LogService.Info("应用流量监控服务已启动");

                // Create app traffic window (hidden, after ProcessTrafficService started)
                LogService.Info("创建应用流量窗口...");
                _appTrafficWindow = new AppTrafficWindow();
                LogService.Info("应用流量窗口创建完成");

                // Start auto update check
                UpdateService.Instance.UpdateAvailable += OnUpdateAvailable;
                UpdateService.Instance.StartAutoCheck();
                LogService.Info("更新检查服务已启动");

                LogService.Info("========== 启动流程完成 ==========");
            }
            catch (Exception ex)
            {
                LogService.Error("启动过程发生致命错误", ex);
                MessageBox.Show(
                    LocalizationService.Instance.Format("Common.StartupFailed", ex.Message),
                    "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void CreateTrayIcon()
        {
            var loc = LocalizationService.Instance;

            _trayIcon = new TaskbarIcon();
            _trayIcon.ToolTipText = loc.Get("Tray.Tooltip");

            // Load icon from embedded resource
            try
            {
                var iconUri = new Uri("pack://application:,,,/Resources/icon.ico");
                var streamInfo = GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    _trayIcon.Icon = new Icon(streamInfo.Stream);
                }
            }
            catch
            {
                // Fallback: try loading from file next to exe
                try
                {
                    var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var iconPath = Path.Combine(exeDir, "Resources", "icon.ico");
                    if (File.Exists(iconPath))
                    {
                        _trayIcon.Icon = new Icon(iconPath);
                    }
                }
                catch { }
            }

            BuildTrayContextMenu();

            _trayIcon.TrayMouseDoubleClick += (s, ev) => ShowSettings();

            // Listen for settings changes to update menu state
            SettingsService.Instance.SettingsChanged += OnSettingsChangedForTray;
        }

        private void BuildTrayContextMenu()
        {
            var loc = LocalizationService.Instance;

            var contextMenu = new ContextMenu();

            var settingsItem = new MenuItem { Header = loc.Get("Tray.Settings") };
            settingsItem.Click += (s, ev) => ShowSettings();

            var statisticsItem = new MenuItem { Header = loc.Get("Tray.Statistics") };
            statisticsItem.Click += (s, ev) => ShowStatistics();

            var appTrafficItem = new MenuItem { Header = loc.Get("Tray.AppTraffic") };
            appTrafficItem.Click += (s, ev) => ShowAppTraffic();

            _autoHideItem = new MenuItem
            {
                Header = loc.Get("Tray.AutoHide"),
                IsCheckable = true,
                IsChecked = SettingsService.Instance.Settings.AutoHide
            };
            _autoHideItem.Click += (s, ev) =>
            {
                SettingsService.Instance.Update(st =>
                {
                    st.AutoHide = _autoHideItem.IsChecked;
                });
            };

            _lockItem = new MenuItem
            {
                Header = loc.Get("Tray.LockOnTop"),
                IsCheckable = true,
                IsChecked = SettingsService.Instance.Settings.LockOnTop
            };
            _lockItem.Click += (s, ev) =>
            {
                SettingsService.Instance.Update(st =>
                {
                    st.LockOnTop = _lockItem.IsChecked;
                });
            };

            var checkUpdateItem = new MenuItem { Header = loc.Get("Tray.CheckUpdate") };
            checkUpdateItem.Click += async (s, ev) => await ManualCheckForUpdate();

            var separator = new Separator();

            var exitItem = new MenuItem { Header = loc.Get("Tray.Exit") };
            exitItem.Click += (s, ev) => ExitApplication();

            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(statisticsItem);
            contextMenu.Items.Add(appTrafficItem);
            contextMenu.Items.Add(_autoHideItem);
            contextMenu.Items.Add(_lockItem);
            contextMenu.Items.Add(checkUpdateItem);
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenu = contextMenu;
        }

        private void OnLanguageChanged()
        {
            Dispatcher.Invoke(() =>
            {
                if (_trayIcon != null)
                {
                    _trayIcon.ToolTipText = LocalizationService.Instance.Get("Tray.Tooltip");
                    BuildTrayContextMenu();
                }
            });
        }

        private void OnSettingsChangedForTray()
        {
            Dispatcher.Invoke(() =>
            {
                var settings = SettingsService.Instance.Settings;
                if (_autoHideItem != null) _autoHideItem.IsChecked = settings.AutoHide;
                if (_lockItem != null) _lockItem.IsChecked = settings.LockOnTop;
            });
        }

        private void ShowSettings()
        {
            if (_settingsWindow == null)
                _settingsWindow = new SettingsWindow();
            _settingsWindow.Show();
        }

        private void ShowStatistics()
        {
            if (_statisticsWindow == null)
                _statisticsWindow = new StatisticsWindow();
            _statisticsWindow.Show();
        }

        private void ShowAppTraffic()
        {
            if (_appTrafficWindow == null)
                _appTrafficWindow = new AppTrafficWindow();
            _appTrafficWindow.Show();
        }

        private void OnUpdateAvailable(UpdateInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                var settings = SettingsService.Instance.Settings;
                if (settings.SkippedVersion == info.TagName) return;

                _pendingUpdateInfo = info;

                if (_trayIcon != null)
                {
                    var loc = LocalizationService.Instance;
                    _trayIcon.ShowBalloonTip(
                        loc.Get("Update.BalloonTitle"),
                        loc.Format("Update.BalloonText", info.Version.ToString(3)),
                        BalloonIcon.Info);
                    _trayIcon.TrayBalloonTipClicked += OnBalloonClicked;
                }
            });
        }

        private void OnBalloonClicked(object sender, RoutedEventArgs e)
        {
            _trayIcon.TrayBalloonTipClicked -= OnBalloonClicked;
            if (_pendingUpdateInfo != null)
                ShowUpdateWindow(_pendingUpdateInfo);
        }

        public void ShowUpdateWindowFromSettings(UpdateInfo info)
        {
            ShowUpdateWindow(info);
        }

        private void ShowUpdateWindow(UpdateInfo info)
        {
            if (_updateWindow != null)
            {
                _updateWindow.Activate();
                return;
            }

            _updateWindow = new UpdateWindow();
            _updateWindow.SetUpdateInfo(info);
            _updateWindow.Closed += (s, ev) => _updateWindow = null;
            _updateWindow.Show();
        }

        private async System.Threading.Tasks.Task ManualCheckForUpdate()
        {
            var loc = LocalizationService.Instance;
            try
            {
                var info = await UpdateService.Instance.CheckForUpdateAsync();
                if (info != null)
                {
                    var skipped = SettingsService.Instance.Settings.SkippedVersion;
                    if (skipped == info.TagName)
                    {
                        var currentVersion = UpdateService.Instance.GetCurrentVersion().ToString(3);
                        MessageBox.Show(
                            loc.Format("Update.AlreadyLatest", currentVersion),
                            "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ShowUpdateWindow(info);
                    }
                }
                else
                {
                    var currentVersion = UpdateService.Instance.GetCurrentVersion().ToString(3);
                    MessageBox.Show(
                        loc.Format("Update.AlreadyLatest", currentVersion),
                        "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch
            {
                MessageBox.Show(
                    loc.Get("Update.CheckFailed"),
                    "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ExitForUpdate()
        {
            LogService.Info("========== FlowWatch 更新退出 ==========");
            UpdateService.Instance.UpdateAvailable -= OnUpdateAvailable;
            UpdateService.Instance.StopAutoCheck();
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
            ProcessTrafficService.Instance.Stop();
            TrafficHistoryService.Instance.Stop();
            NetworkMonitorService.Instance.Stop();
            SettingsService.Instance.SettingsChanged -= OnSettingsChangedForTray;
            _overlayWindow?.Cleanup();
            _settingsWindow?.ForceClose();
            _statisticsWindow?.ForceClose();
            _appTrafficWindow?.ForceClose();
            _updateWindow?.Close();

            _trayIcon?.Dispose();
            _trayIcon = null;

            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _mutex = null;

            LogService.Info("清理完成，正在关闭以进行更新");
            Shutdown();
        }

        private void ExitApplication()
        {
            LogService.Info("========== FlowWatch 退出 ==========");
            UpdateService.Instance.UpdateAvailable -= OnUpdateAvailable;
            UpdateService.Instance.StopAutoCheck();
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
            ProcessTrafficService.Instance.Stop();
            TrafficHistoryService.Instance.Stop();
            NetworkMonitorService.Instance.Stop();
            SettingsService.Instance.SettingsChanged -= OnSettingsChangedForTray;
            _overlayWindow?.Cleanup();
            _settingsWindow?.ForceClose();
            _statisticsWindow?.ForceClose();
            _appTrafficWindow?.ForceClose();

            _trayIcon?.Dispose();
            _trayIcon = null;

            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _mutex = null;

            LogService.Info("清理完成，正在关闭");
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
