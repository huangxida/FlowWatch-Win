using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
        private bool _isAutoHideVisible = true;

        public OverlayWindow()
        {
            InitializeComponent();
            _vm = (OverlayViewModel)DataContext;

            Loaded += OnLoaded;
            LocationChanged += OnLocationChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Restore saved position
            _suppressSave = true;
            var settings = SettingsService.Instance.Settings;
            if (settings.OverlayX.HasValue && settings.OverlayY.HasValue)
            {
                Left = settings.OverlayX.Value;
                Top = settings.OverlayY.Value;
            }
            _suppressSave = false;

            // Apply initial states
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeInterop.SetToolWindow(hwnd);

            ApplyLockState();
            ApplyAutoHideState();

            SettingsService.Instance.SettingsChanged += OnSettingsChanged;
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
            Topmost = settings.LockOnTop;

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

        public void Cleanup()
        {
            _autoHideTimer?.Stop();
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
            _vm?.Cleanup();
        }
    }
}
