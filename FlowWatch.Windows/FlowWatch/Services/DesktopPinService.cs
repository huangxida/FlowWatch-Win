using System;
using System.Windows.Threading;
using FlowWatch.Helpers;

namespace FlowWatch.Services
{
    /// <summary>
    /// Pins the overlay at the desktop Z-level by placing it just below the
    /// desktop-icons window (SHELLDLL_DefView parent). No SetParent — safe
    /// for WPF AllowsTransparency / WS_EX_LAYERED windows.
    /// </summary>
    public static class DesktopPinService
    {
        private static bool _isPinned;
        private static IntPtr _hwnd;
        private static IntPtr _desktopIconsParent;
        private static DispatcherTimer _timer;

        public static bool IsPinned => _isPinned;

        public static bool PinToDesktop(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            if (_isPinned && _hwnd == hwnd) return true;

            _hwnd = hwnd;
            _desktopIconsParent = FindDesktopIconsParent();
            if (_desktopIconsParent == IntPtr.Zero)
                return false;

            PlaceAtDesktopLevel();

            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _timer.Tick += (s, e) => PlaceAtDesktopLevel();
            }
            _timer.Start();

            _isPinned = true;
            return true;
        }

        public static bool UnpinFromDesktop(IntPtr hwnd)
        {
            if (!_isPinned) return true;

            _timer?.Stop();

            _isPinned = false;
            _hwnd = IntPtr.Zero;
            _desktopIconsParent = IntPtr.Zero;
            return true;
        }

        /// <summary>
        /// Find the top-level window that contains SHELLDLL_DefView (desktop icons).
        /// This is usually a WorkerW or Progman.
        /// </summary>
        private static IntPtr FindDesktopIconsParent()
        {
            IntPtr found = IntPtr.Zero;
            NativeInterop.EnumWindows((topHandle, lParam) =>
            {
                var shellView = NativeInterop.FindWindowEx(topHandle, IntPtr.Zero,
                    "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    found = topHandle;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero)
                found = NativeInterop.FindWindow("Progman", null);

            return found;
        }

        /// <summary>
        /// Place the overlay just below the desktop-icons window in Z-order.
        /// SetWindowPos(hwnd, hWndInsertAfter) positions hwnd right after
        /// hWndInsertAfter — i.e. one level below it.
        /// </summary>
        private static void PlaceAtDesktopLevel()
        {
            if (_hwnd == IntPtr.Zero || _desktopIconsParent == IntPtr.Zero) return;

            NativeInterop.SetWindowPos(_hwnd, _desktopIconsParent, 0, 0, 0, 0,
                NativeInterop.SWP_NOMOVE | NativeInterop.SWP_NOSIZE |
                NativeInterop.SWP_NOACTIVATE);
        }
    }
}
