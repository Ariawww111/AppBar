using System;

namespace AppBar.Core.Models
{
    public class AppShortcut
    {
        public AppShortcut()
        {
            Id = Guid.NewGuid().ToString("D");
            DisplayName = string.Empty;
            LaunchPath = string.Empty;
            Arguments = string.Empty;
            WorkingDirectory = string.Empty;
            SourceShortcutPath = string.Empty;
            IconCachePath = string.Empty;
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string LaunchPath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string SourceShortcutPath { get; set; }
        public string IconCachePath { get; set; }

        public AppShortcut Clone()
        {
            return new AppShortcut
            {
                Id = Id,
                DisplayName = DisplayName,
                LaunchPath = LaunchPath,
                Arguments = Arguments,
                WorkingDirectory = WorkingDirectory,
                SourceShortcutPath = SourceShortcutPath,
                IconCachePath = IconCachePath
            };
        }
    }
}

