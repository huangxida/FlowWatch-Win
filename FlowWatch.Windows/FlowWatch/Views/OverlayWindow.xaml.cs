using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FlowWatch.Helpers;
using FlowWatch.Services;
using FlowWatch.ViewModels;

namespace FlowWatch.Views
{
    public partial class OverlayWindow : Window
    {
        private OverlayViewModel _vm;
        private bool _suppressSave;
        private DispatcherTimer _autoHideTimer;
        private DispatcherTimer _topmostWatchdogTimer;
        private bool _isAutoHideVisible = true;
        private readonly Stopwatch _startupStopwatch;

        public OverlayWindow()
        {
            _startupStopwatch = Stopwatch.StartNew();
            LogService.Info("OverlayWindow ctor start");
            InitializeComponent();
            LogService.Info($"OverlayWindow InitializeComponent completed in {_startupStopwatch.ElapsedMilliseconds} ms");
            _vm = (OverlayViewModel)DataContext;
            LogService.Info($"OverlayWindow DataContext ready in {_startupStopwatch.ElapsedMilliseconds} ms");

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            ContentRendered += OnContentRendered;
            LocationChanged += OnLocationChanged;
            Activated += OnActivated;
            Deactivated += OnDeactivated;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                NativeInterop.SetToolWindow(hwnd);
                EnsureTopmost("SourceInitialized");
            }

            var tier = RenderCapability.Tier >> 16;
            LogService.Info(
                $"OverlayWindow SourceInitialized at {_startupStopwatch.ElapsedMilliseconds} ms (RenderTier={tier})");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var loadedStartMs = _startupStopwatch.ElapsedMilliseconds;

            // Restore saved position
            _suppressSave = true;
            var settings = SettingsService.Instance.Settings;
            if (settings.OverlayX.HasValue && settings.OverlayY.HasValue)
            {
                Left = settings.OverlayX.Value;
                Top = settings.OverlayY.Value;
            }
            _suppressSave = false;

            ApplyLockState();
            ApplyAutoHideState();

            SettingsService.Instance.SettingsChanged += OnSettingsChanged;

            // Re-apply after first render cycle to mitigate startup z-order races.
            Dispatcher.BeginInvoke(
                new Action(() => EnsureTopmost("Loaded-BeginInvoke")),
                DispatcherPriority.ApplicationIdle);

            LogService.Info(
                $"OverlayWindow Loaded completed in {_startupStopwatch.ElapsedMilliseconds} ms (handler={_startupStopwatch.ElapsedMilliseconds - loadedStartMs} ms)");
        }

        private void OnContentRendered(object sender, EventArgs e)
        {
            LogService.Info($"OverlayWindow ContentRendered at {_startupStopwatch.ElapsedMilliseconds} ms");
            LogTopmostState("ContentRendered");
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (_suppressSave) return;

            // Save position directly to settings without triggering SettingsChanged
            var settings = SettingsService.Instance.Settings;
            settings.OverlayX = Left;
            settings.OverlayY = Top;
            SettingsService.Instance.Save();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_vm.IsLocked)
            {
                DragMove();
            }
        }

        private void OnSettingsChanged()
        {
            Dispatcher.Invoke(() =>
            {
                ApplyLockState();
                ApplyAutoHideState();
            });
        }

        private void ApplyLockState()
        {
            var settings = SettingsService.Instance.Settings;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            NativeInterop.SetClickThrough(hwnd, settings.LockOnTop);
            if (settings.LockOnTop)
            {
                EnsureTopmost("ApplyLockState");
                StartTopmostWatchdog();
            }
            else
            {
                StopTopmostWatchdog();
                Topmost = false;
                NativeInterop.SetWindowPos(
                    hwnd,
                    NativeInterop.HWND_NOTOPMOST,
                    0, 0, 0, 0,
                    NativeInterop.SWP_NOMOVE | NativeInterop.SWP_NOSIZE | NativeInterop.SWP_NOACTIVATE);
                LogTopmostState("ApplyLockState-Unlock");
            }

            Cursor = settings.LockOnTop ? Cursors.Arrow : Cursors.SizeAll;
        }

        private void ApplyAutoHideState()
        {
            var settings = SettingsService.Instance.Settings;

            if (settings.AutoHide)
            {
                if (_autoHideTimer == null)
                {
                    _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                    _autoHideTimer.Tick += OnAutoHideTimerTick;
                }
                _autoHideTimer.Start();
                // Check current state immediately
                CheckMousePosition();
            }
            else
            {
                _autoHideTimer?.Stop();
                Opacity = 1.0;
                _isAutoHideVisible = true;
            }
        }

        private void OnAutoHideTimerTick(object sender, EventArgs e)
        {
            CheckMousePosition();
        }

        private void CheckMousePosition()
        {
            NativeInterop.GetCursorPos(out var pt);
            var screenPoint = new Point(pt.X, pt.Y);
            var wpfPoint = PointFromScreen(screenPoint);

            // Add a small margin (8px) around the window for easier targeting
            bool isMouseOver = wpfPoint.X >= -8 && wpfPoint.Y >= -8 &&
                               wpfPoint.X <= ActualWidth + 8 && wpfPoint.Y <= ActualHeight + 8;

            if (isMouseOver && !_isAutoHideVisible)
            {
                _isAutoHideVisible = true;
                Opacity = 1.0;
            }
            else if (!isMouseOver && _isAutoHideVisible)
            {
                _isAutoHideVisible = false;
                Opacity = 0.1;
            }
        }

        private void OnActivated(object sender, EventArgs e)
        {
            EnsureTopmost("Activated");
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            EnsureTopmost("Deactivated");
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                EnsureTopmost("IsVisibleChanged-Visible");
            }
        }

        private void StartTopmostWatchdog()
        {
            if (_topmostWatchdogTimer == null)
            {
                _topmostWatchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _topmostWatchdogTimer.Tick += OnTopmostWatchdogTick;
            }
            _topmostWatchdogTimer.Start();
        }

        private void StopTopmostWatchdog()
        {
            _topmostWatchdogTimer?.Stop();
        }

        private void OnTopmostWatchdogTick(object sender, EventArgs e)
        {
            var settings = SettingsService.Instance.Settings;
            if (!settings.LockOnTop) return;

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            var isTopMost = NativeInterop.IsTopMost(hwnd);
            if (!Topmost || !isTopMost)
            {
                LogService.Warn(
                    $"Overlay topmost dropped, recovering. reason=Watchdog, WpfTopmost={Topmost}, Win32Topmost={isTopMost}");
                EnsureTopmost("Watchdog-Recover");
            }
        }

        private void EnsureTopmost(string reason)
        {
            var settings = SettingsService.Instance.Settings;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (!settings.LockOnTop)
            {
                LogTopmostState($"{reason}-Skip(LockOff)");
                return;
            }

            Topmost = false;
            Topmost = true;
            NativeInterop.SetWindowPos(
                hwnd,
                NativeInterop.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeInterop.SWP_NOMOVE | NativeInterop.SWP_NOSIZE | NativeInterop.SWP_NOACTIVATE);

            LogTopmostState(reason);
        }

        private void LogTopmostState(string reason)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                LogService.Info($"OverlayTopmost reason={reason}, hwnd=0");
                return;
            }

            var exStyle = NativeInterop.GetExStyle(hwnd);
            var isTopMost = (exStyle & NativeInterop.WS_EX_TOPMOST) != 0;
            LogService.Info(
                $"OverlayTopmost reason={reason}, lock={SettingsService.Instance.Settings.LockOnTop}, " +
                $"wpfTopmost={Topmost}, hwnd=0x{hwnd.ToInt64():X}, exStyle=0x{exStyle:X}, win32Topmost={isTopMost}");
        }

        public void Cleanup()
        {
            _autoHideTimer?.Stop();
            _topmostWatchdogTimer?.Stop();
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
            _vm?.Cleanup();
        }
    }
}
