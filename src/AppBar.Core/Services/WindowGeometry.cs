using System;

namespace AppBar.Core.Services
{
    public struct ScreenBounds
    {
        private readonly int _x;
        private readonly int _y;
        private readonly int _width;
        private readonly int _height;

        public ScreenBounds(int x, int y, int width, int height)
        {
            _x = x;
            _y = y;
            _width = width;
            _height = height;
        }

        public int X { get { return _x; } }
        public int Y { get { return _y; } }
        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
        public int Right { get { return _x + _width; } }
        public int Bottom { get { return _y + _height; } }
    }

    public struct SizedPlacement
    {
        private readonly int _x;
        private readonly int _y;
        private readonly int _width;
        private readonly int _height;

        public SizedPlacement(int x, int y, int width, int height)
        {
            _x = x;
            _y = y;
            _width = width;
            _height = height;
        }

        public int X { get { return _x; } }
        public int Y { get { return _y; } }
        public int Width { get { return _width; } }
        public int Height { get { return _height; } }
    }

    public static class WindowGeometry
    {
        public static SizedPlacement ClampBubble(
            int x,
            int y,
            int bubbleWidth,
            int bubbleHeight,
            ScreenBounds workArea)
        {
            int minX = workArea.X + 8;
            int minY = workArea.Y + 8;
            int maxX = workArea.Right - bubbleWidth - 8;
            int maxY = workArea.Bottom - bubbleHeight - 8;

            if (maxX < minX)
            {
                maxX = workArea.X;
            }

            if (maxY < minY)
            {
                maxY = workArea.Y;
            }

            return new SizedPlacement(
                Clamp(x, minX, maxX),
                Clamp(y, minY, maxY),
                bubbleWidth,
                bubbleHeight);
        }

        public static SizedPlacement CalculatePanelSize(ScreenBounds workArea, double dpiScale)
        {
            if (dpiScale <= 0)
            {
                dpiScale = 1.0;
            }

            int margin = Scale(24, dpiScale);
            int minWidth = Math.Min(Scale(560, dpiScale), Math.Max(1, workArea.Width - margin));
            int minHeight = Math.Min(Scale(280, dpiScale), Math.Max(1, workArea.Height - margin));
            int maxWidth = Math.Min(Scale(800, dpiScale), Math.Max(1, workArea.Width - margin));
            int maxHeight = Math.Min(Scale(360, dpiScale), Math.Max(1, workArea.Height - margin));

            int width = Clamp((int)Math.Round(workArea.Width * 0.36), minWidth, maxWidth);
            int height = Clamp((int)Math.Round(workArea.Height * 0.30), minHeight, maxHeight);
            return new SizedPlacement(0, 0, width, height);
        }

        public static SizedPlacement PlacePanel(
            ScreenBounds workArea,
            ScreenBounds bubble,
            int panelWidth,
            int panelHeight,
            double dpiScale)
        {
            int margin = Scale(12, dpiScale);
            int gap = Scale(8, dpiScale);

            int centeredX = bubble.X + (bubble.Width - panelWidth) / 2;
            int x = Clamp(
                centeredX,
                workArea.X + margin,
                Math.Max(workArea.X + margin, workArea.Right - panelWidth - margin));

            int belowY = bubble.Bottom + gap;
            int aboveY = bubble.Y - gap - panelHeight;
            int y;

            if (belowY + panelHeight <= workArea.Bottom - margin)
            {
                y = belowY;
            }
            else if (aboveY >= workArea.Y + margin)
            {
                y = aboveY;
            }
            else
            {
                y = Clamp(
                    belowY,
                    workArea.Y + margin,
                    Math.Max(workArea.Y + margin, workArea.Bottom - panelHeight - margin));
            }

            return new SizedPlacement(x, y, panelWidth, panelHeight);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            return Math.Max(min, Math.Min(max, value));
        }

        private static int Scale(int value, double scale)
        {
            return (int)Math.Round(value * scale);
        }
    }
}
