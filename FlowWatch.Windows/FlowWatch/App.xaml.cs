using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
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
        private MenuItem _pinItem;
        private MenuItem _lockItem;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Single instance check
            bool createdNew;
            _mutex = new Mutex(true, "FlowWatch_SingleInstance", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("FlowWatch 已在运行中。", "FlowWatch", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Initialize settings
            var settings = SettingsService.Instance.Settings;

            // Apply auto-launch setting
            AutoLaunchService.SetAutoLaunch(settings.AutoLaunch);

            // Create tray icon
            CreateTrayIcon();

            // Create overlay window
            _overlayWindow = new OverlayWindow();
            _overlayWindow.Show();

            // Create settings window (hidden)
            _settingsWindow = new SettingsWindow();

            // Start network monitoring
            NetworkMonitorService.Instance.Start(settings.RefreshInterval);

            // Start traffic history recording
            TrafficHistoryService.Instance.Start();
        }

        private void CreateTrayIcon()
        {
            _trayIcon = new TaskbarIcon();
            _trayIcon.ToolTipText = "FlowWatch 网速监控";

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

            // Context menu
            var contextMenu = new ContextMenu();

            var settingsItem = new MenuItem { Header = "设置" };
            settingsItem.Click += (s, ev) => ShowSettings();

            _pinItem = new MenuItem
            {
                Header = "固定桌面",
                IsCheckable = true,
                IsChecked = SettingsService.Instance.Settings.PinToDesktop
            };
            _pinItem.Click += (s, ev) =>
            {
                SettingsService.Instance.Update(st =>
                {
                    st.PinToDesktop = _pinItem.IsChecked;
                    if (_pinItem.IsChecked) st.LockOnTop = false;
                });
            };

            _lockItem = new MenuItem
            {
                Header = "置顶",
                IsCheckable = true,
                IsChecked = SettingsService.Instance.Settings.LockOnTop
            };
            _lockItem.Click += (s, ev) =>
            {
                SettingsService.Instance.Update(st =>
                {
                    st.LockOnTop = _lockItem.IsChecked;
                    if (_lockItem.IsChecked) st.PinToDesktop = false;
                });
            };

            var separator = new Separator();

            var exitItem = new MenuItem { Header = "退出" };
            exitItem.Click += (s, ev) => ExitApplication();

            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(_pinItem);
            contextMenu.Items.Add(_lockItem);
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenu = contextMenu;
            _trayIcon.TrayMouseDoubleClick += (s, ev) => ShowSettings();

            // Listen for settings changes to update menu state
            SettingsService.Instance.SettingsChanged += OnSettingsChangedForTray;
        }

        private void OnSettingsChangedForTray()
        {
            Dispatcher.Invoke(() =>
            {
                var settings = SettingsService.Instance.Settings;
                if (_pinItem != null) _pinItem.IsChecked = settings.PinToDesktop;
                if (_lockItem != null) _lockItem.IsChecked = settings.LockOnTop;
            });
        }

        private void ShowSettings()
        {
            if (_settingsWindow == null)
                _settingsWindow = new SettingsWindow();
            _settingsWindow.Show();
        }

        private void ExitApplication()
        {
            TrafficHistoryService.Instance.Stop();
            NetworkMonitorService.Instance.Stop();
            SettingsService.Instance.SettingsChanged -= OnSettingsChangedForTray;
            _overlayWindow?.Cleanup();
            _settingsWindow?.ForceClose();

            _trayIcon?.Dispose();
            _trayIcon = null;

            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _mutex = null;

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
