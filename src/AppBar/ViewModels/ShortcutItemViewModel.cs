using System;
using System.IO;
using System.Windows.Media;
using AppBar.Core.Models;
using AppBar.Services;

namespace AppBar.ViewModels
{
    public class ShortcutItemViewModel : ObservableObject
    {
        private static readonly Color[] AccentPalette =
        {
            Color.FromRgb(202, 105, 82),
            Color.FromRgb(55, 137, 123),
            Color.FromRgb(73, 104, 156),
            Color.FromRgb(183, 131, 54),
            Color.FromRgb(127, 94, 144)
        };

        public ShortcutItemViewModel(AppShortcut shortcut, IconService iconService)
        {
            if (shortcut == null)
            {
                throw new ArgumentNullException("shortcut");
            }

            Shortcut = shortcut;
            Icon = iconService.LoadOrCreate(shortcut);
            FallbackText = BuildFallbackText(shortcut.DisplayName);
            FallbackBrush = CreateFallbackBrush(shortcut.DisplayName);
            IsMissing = !string.IsNullOrWhiteSpace(shortcut.LaunchPath) &&
                        !File.Exists(shortcut.LaunchPath) &&
                        !File.Exists(shortcut.SourceShortcutPath);
        }

        public AppShortcut Shortcut { get; private set; }
        public string Id { get { return Shortcut.Id; } }
        public string DisplayName { get { return Shortcut.DisplayName; } }
        public ImageSource Icon { get; private set; }
        public string FallbackText { get; private set; }
        public SolidColorBrush FallbackBrush { get; private set; }
        public bool IsMissing { get; private set; }
        public bool HasIcon { get { return Icon != null; } }

        public string ToolTipText
        {
            get
            {
                return IsMissing
                    ? DisplayName + "\n目标程序已移动或删除"
                    : DisplayName;
            }
        }

        private static string BuildFallbackText(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            string trimmed = name.Trim();
            return trimmed.Substring(0, 1).ToUpperInvariant();
        }

        private static SolidColorBrush CreateFallbackBrush(string name)
        {
            int hash = 17;
            if (name != null)
            {
                foreach (char character in name)
                {
                    hash = unchecked(hash * 31 + character);
                }
            }

            int index = Math.Abs(hash % AccentPalette.Length);
            SolidColorBrush brush = new SolidColorBrush(AccentPalette[index]);
            brush.Freeze();
            return brush;
        }
    }
}
