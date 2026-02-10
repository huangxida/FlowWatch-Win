using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FlowWatch.Helpers;
using FlowWatch.Services;
using FlowWatch.ViewModels;

namespace FlowWatch.Views
{
    public partial class OverlayWindow : Window
    {
        private OverlayViewModel _vm;
        private bool _suppressSave;

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
            ApplyPinState();

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
                ApplyPinState();
            });
        }

        private void ApplyLockState()
        {
            var settings = SettingsService.Instance.Settings;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            bool locked = settings.LockOnTop || settings.PinToDesktop;
            NativeInterop.SetClickThrough(hwnd, locked);
            Topmost = settings.LockOnTop && !settings.PinToDesktop;

            Cursor = locked ? Cursors.Arrow : Cursors.SizeAll;
        }

        private void ApplyPinState()
        {
            var settings = SettingsService.Instance.Settings;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (settings.PinToDesktop)
            {
                DesktopPinService.PinToDesktop(hwnd);
            }
            else if (DesktopPinService.IsPinned)
            {
                DesktopPinService.UnpinFromDesktop(hwnd);
            }
        }

        public void Cleanup()
        {
            SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
            _vm?.Cleanup();
        }
    }
}
