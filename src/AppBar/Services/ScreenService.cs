using System;
using System.Windows;
using System.Windows.Interop;
using AppBar.Core.Services;
using AppBar.Infrastructure;

namespace AppBar.Services
{
    public class ScreenSnapshot
    {
        public ScreenSnapshot(ScreenBounds workArea, double dpiScale)
        {
            WorkArea = workArea;
            DpiScale = dpiScale;
        }

        public ScreenBounds WorkArea { get; private set; }
        public double DpiScale { get; private set; }
    }

    public class ScreenService
    {
        private const uint MonitorDefaultToNearest = 2;

        public ScreenSnapshot GetForWindow(IntPtr hwnd)
        {
            IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            return GetForMonitor(monitor, hwnd);
        }

        public ScreenSnapshot GetForPoint(int x, int y)
        {
            NativeMethods.POINT point = new NativeMethods.POINT { X = x, Y = y };
            IntPtr monitor = NativeMethods.MonitorFromPoint(point, MonitorDefaultToNearest);
            return GetForMonitor(monitor, IntPtr.Zero);
        }

        internal bool TryGetCursor(out NativeMethods.POINT point)
        {
            return NativeMethods.GetCursorPos(out point);
        }

        internal NativeMethods.RECT GetWindowBounds(IntPtr hwnd)
        {
            NativeMethods.RECT rect;
            if (!NativeMethods.GetWindowRect(hwnd, out rect))
            {
                return new NativeMethods.RECT();
            }

            return rect;
        }

        public void SetBounds(IntPtr hwnd, int x, int y, int width, int height, bool activate)
        {
            uint flags = NativeMethods.SwpShowWindow;
            if (!activate)
            {
                flags |= NativeMethods.SwpNoActivate;
            }

            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HwndTopmost,
                x,
                y,
                width,
                height,
                flags);
        }

        public static IntPtr GetHandle(Window window)
        {
            return new WindowInteropHelper(window).Handle;
        }

        private ScreenSnapshot GetForMonitor(IntPtr monitor, IntPtr hwnd)
        {
            NativeMethods.MONITORINFO info = new NativeMethods.MONITORINFO();
            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));

            ScreenBounds workArea;
            if (monitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                workArea = new ScreenBounds(
                    info.rcWork.Left,
                    info.rcWork.Top,
                    info.rcWork.Width,
                    info.rcWork.Height);
            }
            else
            {
                workArea = new ScreenBounds(
                    0,
                    0,
                    Math.Max(1, NativeMethods.GetSystemMetrics(0)),
                    Math.Max(1, NativeMethods.GetSystemMetrics(1)));
            }

            double dpiScale = 1.0;
            if (hwnd != IntPtr.Zero)
            {
                uint dpi = NativeMethods.GetDpiForWindow(hwnd);
                if (dpi > 0)
                {
                    dpiScale = dpi / 96.0;
                }
            }

            return new ScreenSnapshot(workArea, dpiScale);
        }
    }
}

