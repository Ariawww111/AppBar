using System;
using System.Runtime.InteropServices;
using System.Windows;
using AppBar.Infrastructure;

namespace AppBar.Services
{
    public class AcrylicBackdropService
    {
        private const int WindowCompositionAttributeAccentPolicy = 19;
        private const int AccentEnableAcrylicBlurBehind = 4;
        private const int AccentFlagDrawBorder = 2;

        public bool TryApplyAcrylic(Window window, byte alpha, byte red, byte green, byte blue)
        {
            IntPtr hwnd = ScreenService.GetHandle(window);
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                NativeMethods.AccentPolicy policy = new NativeMethods.AccentPolicy
                {
                    AccentState = AccentEnableAcrylicBlurBehind,
                    AccentFlags = AccentFlagDrawBorder,
                    GradientColor = ToAbgr(alpha, red, green, blue),
                    AnimationId = 0
                };

                int size = Marshal.SizeOf(typeof(NativeMethods.AccentPolicy));
                IntPtr policyPointer = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(policy, policyPointer, false);
                    NativeMethods.WindowCompositionAttributeData data =
                        new NativeMethods.WindowCompositionAttributeData
                        {
                            Attribute = WindowCompositionAttributeAccentPolicy,
                            Data = policyPointer,
                            SizeOfData = size
                        };

                    return NativeMethods.SetWindowCompositionAttribute(hwnd, ref data) != 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(policyPointer);
                }
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        public void ApplyRoundedWindowPreference(Window window)
        {
            IntPtr hwnd = ScreenService.GetHandle(window);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            int preference = NativeMethods.DwmRound;
            try
            {
                NativeMethods.DwmSetWindowAttribute(
                    hwnd,
                    NativeMethods.DwmWindowCornerPreference,
                    ref preference,
                    sizeof(int));
            }
            catch (DllNotFoundException)
            {
            }
        }

        public bool ApplyRoundedWindowRegion(Window window, double radius, double dpiScale)
        {
            IntPtr hwnd = ScreenService.GetHandle(window);
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            NativeMethods.RECT bounds;
            if (!NativeMethods.GetWindowRect(hwnd, out bounds) ||
                bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return false;
            }

            if (dpiScale <= 0)
            {
                dpiScale = 1.0;
            }

            int pixelRadius = Math.Max(1, (int)Math.Round(radius * dpiScale));
            IntPtr region = NativeMethods.CreateRoundRectRgn(
                0,
                0,
                bounds.Width + 1,
                bounds.Height + 1,
                pixelRadius * 2,
                pixelRadius * 2);
            if (region == IntPtr.Zero)
            {
                return false;
            }

            if (NativeMethods.SetWindowRgn(hwnd, region, true) == 0)
            {
                NativeMethods.DeleteObject(region);
                return false;
            }

            return true;
        }

        private static int ToAbgr(byte alpha, byte red, byte green, byte blue)
        {
            return (alpha << 24) | (blue << 16) | (green << 8) | red;
        }
    }
}

