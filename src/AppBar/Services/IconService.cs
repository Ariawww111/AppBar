using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AppBar.Core.Models;

namespace AppBar.Services
{
    public class IconService
    {
        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiLargeIcon = 0x000000000;
        private const string CleanCacheSuffix = ".normalized.png";
        private const int NormalizedIconSize = 64;
        private const double NormalizedIconPadding = 2;

        private readonly string _iconDirectory;

        public IconService(string dataDirectory)
        {
            _iconDirectory = Path.Combine(dataDirectory, "icons");
        }

        public ImageSource LoadOrCreate(AppShortcut shortcut)
        {
            if (shortcut == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(shortcut.IconCachePath) &&
                shortcut.IconCachePath.EndsWith(CleanCacheSuffix, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(shortcut.IconCachePath))
            {
                ImageSource cached = TryLoadBitmap(shortcut.IconCachePath);
                if (cached != null)
                {
                    return cached;
                }
            }

            string source = ResolveIconSource(shortcut);
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                return null;
            }

            try
            {
                SHFILEINFO info = new SHFILEINFO();
                IntPtr result = SHGetFileInfo(
                    source,
                    0,
                    ref info,
                    (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                    ShgfiIcon | ShgfiLargeIcon);
                if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    BitmapSource sourceBitmap = Imaging.CreateBitmapSourceFromHIcon(
                        info.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(96, 96));
                    sourceBitmap.Freeze();
                    BitmapSource bitmap = NormalizeIcon(sourceBitmap);

                    Directory.CreateDirectory(_iconDirectory);
                    string iconPath = Path.Combine(
                        _iconDirectory,
                        shortcut.Id + CleanCacheSuffix);
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (FileStream stream = File.Create(iconPath))
                    {
                        encoder.Save(stream);
                    }

                    shortcut.IconCachePath = iconPath;
                    return bitmap;
                }
                finally
                {
                    DestroyIcon(info.hIcon);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ResolveIconSource(AppShortcut shortcut)
        {
            if (!string.IsNullOrWhiteSpace(shortcut.LaunchPath) &&
                File.Exists(shortcut.LaunchPath))
            {
                return shortcut.LaunchPath;
            }

            return null;
        }

        private static ImageSource TryLoadBitmap(string path)
        {
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static BitmapSource NormalizeIcon(BitmapSource source)
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                return source;
            }

            BitmapSource converted = source;
            if (source.Format != PixelFormats.Bgra32)
            {
                converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }

            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * stride;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[rowOffset + (x * 4) + 3] <= 8)
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return source;
            }

            CroppedBitmap cropped = new CroppedBitmap(
                converted,
                new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
            cropped.Freeze();

            double available = NormalizedIconSize - (NormalizedIconPadding * 2);
            double scale = Math.Min(available / cropped.PixelWidth, available / cropped.PixelHeight);
            double drawWidth = Math.Max(1, Math.Round(cropped.PixelWidth * scale));
            double drawHeight = Math.Max(1, Math.Round(cropped.PixelHeight * scale));
            double drawX = Math.Round((NormalizedIconSize - drawWidth) / 2);
            double drawY = Math.Round((NormalizedIconSize - drawHeight) / 2);

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawImage(cropped, new Rect(drawX, drawY, drawWidth, drawHeight));
            }

            RenderTargetBitmap normalized = new RenderTargetBitmap(
                NormalizedIconSize,
                NormalizedIconSize,
                96,
                96,
                PixelFormats.Pbgra32);
            normalized.Render(visual);
            normalized.Freeze();
            return normalized;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string path,
            uint fileAttributes,
            ref SHFILEINFO fileInfo,
            uint fileInfoSize,
            uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}

