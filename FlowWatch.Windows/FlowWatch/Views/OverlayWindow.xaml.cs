using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using FlowWatch.Helpers;
using FlowWatch.Services;
using FlowWatch.ViewModels;

namespace FlowWatch.Views
{
    public partial class OverlayWindow : Window
    {
        private static readonly TimeSpan TopmostSnapshotThrottle = TimeSpan.FromSeconds(2);

        private OverlayViewModel _vm;
        private bool _suppressSave;
        private DispatcherTimer _autoHideTimer;
        private DispatcherTimer _topmostWatchdogTimer;
        private bool _isAutoHideVisible = true;
        private readonly Stopwatch _startupStopwatch;
        private HwndSource _hwndSource;
        private HwndSourceHook _hwndHook;
        private bool _systemEventsSubscribed;
        private bool _topmostRecoveryScheduled;
        private bool _isRecoveringTopmost;
        private string _pendingTopmostRecoveryReason;
        private string _lastTopmostSnapshotKey;
        private DateTime _lastTopmostSnapshotAtUtc;

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

        public void ReassertTopmostWithDiagnostics()
        {
            CaptureTopmostSnapshot("TrayManual-Requested", true);
            RecoverTopmost("TrayManual");
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                NativeInterop.SetToolWindow(hwnd);
                _hwndSource = HwndSource.FromHwnd(hwnd);
                if (_hwndSource != null)
                {
                    _hwndHook = OnWindowMessage;
                    _hwndSource.AddHook(_hwndHook);
                }
                SubscribeSystemEvents();
                RecoverTopmost("SourceInitialized");
            }

            var tier = RenderCapability.Tier >> 16;
            LogService.Info(
                $"OverlayWindow SourceInitialized at {_startupStopwatch.ElapsedMilliseconds} ms (RenderTier={tier})");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var loadedStartMs = _startupStopwatch.ElapsedMilliseconds;

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

            Dispatcher.BeginInvoke(
                new Action(() => RecoverTopmost("Loaded-BeginInvoke")),
                DispatcherPriority.ApplicationIdle);

            LogService.Info(
                $"OverlayWindow Loaded completed in {_startupStopwatch.ElapsedMilliseconds} ms (handler={_startupStopwatch.ElapsedMilliseconds - loadedStartMs} ms)");
        }

        private void OnContentRendered(object sender, EventArgs e)
        {
            LogService.Info($"OverlayWindow ContentRendered at {_startupStopwatch.ElapsedMilliseconds} ms");
            CaptureTopmostSnapshot("ContentRendered", false);
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (_suppressSave) return;

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
                Cursor = Cursors.Arrow;
                StartTopmostWatchdog();
                RecoverTopmost("ApplyLockState");
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
                Cursor = Cursors.SizeAll;
                CaptureTopmostSnapshot("ApplyLockState-Unlock", true);
            }
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
            ObserveAndScheduleRecovery("Activated");
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            ObserveAndScheduleRecovery("Deactivated");
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                ObserveAndScheduleRecovery("IsVisibleChanged-Visible");
            }
        }

        private void StartTopmostWatchdog()
        {
            if (_topmostWatchdogTimer == null)
            {
                _topmostWatchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
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
            if (!ShouldMonitorTopmost()) return;

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            var isTopMost = NativeInterop.IsTopMost(hwnd);
            if (!Topmost || !isTopMost)
            {
                LogService.Warn(
                    $"Overlay topmost dropped, recovering. reason=Watchdog-Mismatch, WpfTopmost={Topmost}, Win32Topmost={isTopMost}");
                CaptureTopmostSnapshot("Watchdog-Mismatch", true);
                RecoverTopmost("Watchdog-Mismatch");
            }
        }

        private void ObserveAndScheduleRecovery(string reason)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => ObserveAndScheduleRecovery(reason)), DispatcherPriority.Background);
                return;
            }

            if (!ShouldMonitorTopmost()) return;

            CaptureTopmostSnapshot(reason, false);
            ScheduleTopmostRecovery(reason);
        }

        private void ScheduleTopmostRecovery(string reason)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => ScheduleTopmostRecovery(reason)), DispatcherPriority.Background);
                return;
            }

            if (!ShouldMonitorTopmost()) return;

            _pendingTopmostRecoveryReason = reason;
            if (_topmostRecoveryScheduled) return;

            _topmostRecoveryScheduled = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _topmostRecoveryScheduled = false;
                var scheduledReason = _pendingTopmostRecoveryReason ?? reason;
                _pendingTopmostRecoveryReason = null;

                if (!ShouldMonitorTopmost()) return;
                RecoverTopmost(scheduledReason);
            }), DispatcherPriority.ApplicationIdle);
        }

        private void RecoverTopmost(string reason)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => RecoverTopmost(reason)), DispatcherPriority.Send);
                return;
            }

            var settings = SettingsService.Instance.Settings;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (!settings.LockOnTop)
            {
                CaptureTopmostSnapshot($"{reason}-Skip(LockOff)", false);
                return;
            }

            if (_isRecoveringTopmost) return;

            _isRecoveringTopmost = true;
            try
            {
                CaptureTopmostSnapshot($"{reason}-BeforeRecover", true);

                NativeInterop.ShowWindow(hwnd, NativeInterop.SW_SHOWNA);

                var notTopmostOk = NativeInterop.SetWindowPos(
                    hwnd,
                    NativeInterop.HWND_NOTOPMOST,
                    0, 0, 0, 0,
                    NativeInterop.SWP_NOMOVE | NativeInterop.SWP_NOSIZE | NativeInterop.SWP_NOACTIVATE);
                var notTopmostError = notTopmostOk ? 0 : Marshal.GetLastWin32Error();

                var topmostOk = NativeInterop.SetWindowPos(
                    hwnd,
                    NativeInterop.HWND_TOPMOST,
                    0, 0, 0, 0,
                    NativeInterop.SWP_NOMOVE | NativeInterop.SWP_NOSIZE | NativeInterop.SWP_NOACTIVATE | NativeInterop.SWP_SHOWWINDOW);
                var topmostError = topmostOk ? 0 : Marshal.GetLastWin32Error();

                Topmost = true;

                if (!notTopmostOk || !topmostOk)
                {
                    LogService.Warn(
                        $"RecoverTopmost encountered Win32 failure. reason={reason}, notTopmostOk={notTopmostOk}, notTopmostError={notTopmostError}, topmostOk={topmostOk}, topmostError={topmostError}");
                }

                CaptureTopmostSnapshot($"{reason}-AfterRecover", true);
            }
            finally
            {
                _isRecoveringTopmost = false;
            }
        }

        private void CaptureTopmostSnapshot(string reason, bool force)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => CaptureTopmostSnapshot(reason, force)), DispatcherPriority.Background);
                return;
            }

            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = NativeInterop.GetExStyle(hwnd);
            var win32Topmost = (exStyle & NativeInterop.WS_EX_TOPMOST) != 0;
            var foregroundHwnd = NativeInterop.GetForegroundWindow();
            var prevHwnd = hwnd == IntPtr.Zero ? IntPtr.Zero : NativeInterop.GetWindow(hwnd, NativeInterop.GW_HWNDPREV);
            var nextHwnd = hwnd == IntPtr.Zero ? IntPtr.Zero : NativeInterop.GetWindow(hwnd, NativeInterop.GW_HWNDNEXT);

            var key = string.Join("|",
                reason,
                FormatHwnd(hwnd),
                exStyle.ToString("X", CultureInfo.InvariantCulture),
                FormatHwnd(foregroundHwnd),
                FormatHwnd(prevHwnd),
                FormatHwnd(nextHwnd));
            var now = DateTime.UtcNow;
            if (!force &&
                string.Equals(key, _lastTopmostSnapshotKey, StringComparison.Ordinal) &&
                now - _lastTopmostSnapshotAtUtc < TopmostSnapshotThrottle)
            {
                return;
            }

            _lastTopmostSnapshotKey = key;
            _lastTopmostSnapshotAtUtc = now;

            var settings = SettingsService.Instance.Settings;
            LogService.Info(
                $"OverlayTopmostSnapshot reason={reason}, lock={settings.LockOnTop}, visible={IsVisible}, opacity={FormatDouble(Opacity)}, " +
                $"wpfTopmost={Topmost}, hwnd={FormatHwnd(hwnd)}, exStyle=0x{exStyle:X}, win32Topmost={win32Topmost}, " +
                $"bounds=({FormatDouble(Left)},{FormatDouble(Top)},{FormatDouble(ActualWidth)}x{FormatDouble(ActualHeight)}), " +
                $"foreground={DescribeWindow(foregroundHwnd, true)}, prev={DescribeWindow(prevHwnd, false)}, next={DescribeWindow(nextHwnd, false)}");
        }

        private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_isRecoveringTopmost) return IntPtr.Zero;

            switch ((uint)msg)
            {
                case NativeInterop.WM_WINDOWPOSCHANGING:
                case NativeInterop.WM_WINDOWPOSCHANGED:
                    if (!ShouldMonitorTopmost() || lParam == IntPtr.Zero) break;

                    WINDOWPOS windowPos;
                    try
                    {
                        windowPos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                    }
                    catch
                    {
                        break;
                    }

                    if (!IsSuspiciousWindowPos(windowPos)) break;

                    var reason = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} flags=0x{1:X} insertAfter={2}",
                        (uint)msg == NativeInterop.WM_WINDOWPOSCHANGING ? "WM_WINDOWPOSCHANGING" : "WM_WINDOWPOSCHANGED",
                        windowPos.flags,
                        FormatInsertAfter(windowPos.hwndInsertAfter));
                    CaptureTopmostSnapshot(reason, false);
                    ScheduleTopmostRecovery(reason);
                    break;

                case NativeInterop.WM_DISPLAYCHANGE:
                    ObserveAndScheduleRecovery("WM_DISPLAYCHANGE");
                    break;
            }

            return IntPtr.Zero;
        }

        private void SubscribeSystemEvents()
        {
            if (_systemEventsSubscribed) return;

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            _systemEventsSubscribed = true;
        }

        private void UnsubscribeSystemEvents()
        {
            if (!_systemEventsSubscribed) return;

            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _systemEventsSubscribed = false;
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            ObserveAndScheduleRecovery("DisplaySettingsChanged");
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            ObserveAndScheduleRecovery($"PowerModeChanged:{e.Mode}");
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            ObserveAndScheduleRecovery($"SessionSwitch:{e.Reason}");
        }

        private bool ShouldMonitorTopmost()
        {
            return SettingsService.Instance.Settings.LockOnTop && IsVisible;
        }

        private static bool IsSuspiciousWindowPos(WINDOWPOS windowPos)
        {
            var isZOrderChange = (windowPos.flags & NativeInterop.SWP_NOZORDER) == 0;
            if (!isZOrderChange) return false;

            return windowPos.hwndInsertAfter != NativeInterop.HWND_TOPMOST;
        }

        private string DescribeWindow(IntPtr hwnd, bool includeProcess)
        {
            if (hwnd == IntPtr.Zero) return "0x0";

            var title = SanitizeForLog(NativeInterop.GetWindowTextString(hwnd));
            var className = SanitizeForLog(NativeInterop.GetClassNameString(hwnd));
            if (!includeProcess)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} title=\"{1}\" class=\"{2}\"",
                    FormatHwnd(hwnd),
                    title,
                    className);
            }

            var processId = NativeInterop.GetProcessId(hwnd);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} title=\"{1}\" class=\"{2}\" pid={3} process=\"{4}\"",
                FormatHwnd(hwnd),
                title,
                className,
                processId,
                SanitizeForLog(GetProcessName(processId)));
        }

        private static string GetProcessName(uint processId)
        {
            if (processId == 0) return string.Empty;

            try
            {
                return Process.GetProcessById((int)processId).ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SanitizeForLog(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var sanitized = value.Replace("\r", " ").Replace("\n", " ").Replace("\"", "'");
            sanitized = sanitized.Trim();
            if (sanitized.Length > 120)
            {
                sanitized = sanitized.Substring(0, 117) + "...";
            }

            return sanitized;
        }

        private static string FormatHwnd(IntPtr hwnd)
        {
            return hwnd == IntPtr.Zero
                ? "0x0"
                : string.Format(CultureInfo.InvariantCulture, "0x{0:X}", hwnd.ToInt64());
        }

        private static string FormatInsertAfter(IntPtr hwnd)
        {
            if (hwnd == NativeInterop.HWND_TOPMOST) return "HWND_TOPMOST";
            if (hwnd == NativeInterop.HWND_NOTOPMOST) return "HWND_NOTOPMOST";
            return FormatHwnd(hwnd);
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public void Cleanup()
        {
            _autoHideTimer?.Stop();
            _topmostWatchdogTimer?.Stop();
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
            UnsubscribeSystemEvents();
            if (_hwndSource != null && _hwndHook != null)
            {
                _hwndSource.RemoveHook(_hwndHook);
                _hwndHook = null;
                _hwndSource = null;
            }
            _vm?.Cleanup();
        }
    }
}
