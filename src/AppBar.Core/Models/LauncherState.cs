using System;
using System.Collections.Generic;

namespace AppBar.Core.Models
{
    public class LauncherState
    {
        public LauncherState()
        {
            SchemaVersion = 1;
            BubbleX = 24;
            BubbleY = 24;
            BubblePositionInitialized = false;
            StartupConfigured = false;
            Apps = new List<AppShortcut>();
        }

        public int SchemaVersion { get; set; }
        public double BubbleX { get; set; }
        public double BubbleY { get; set; }
        public bool BubblePositionInitialized { get; set; }
        public bool StartupConfigured { get; set; }
        public List<AppShortcut> Apps { get; set; }

        public static LauncherState CreateDefault()
        {
            return new LauncherState();
        }

        public void Normalize()
        {
            if (SchemaVersion <= 0)
            {
                SchemaVersion = 1;
            }

            if (Apps == null)
            {
                Apps = new List<AppShortcut>();
            }

            List<AppShortcut> validApps = new List<AppShortcut>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AppShortcut app in Apps)
            {
                if (app == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(app.Id) || !ids.Add(app.Id))
                {
                    app.Id = Guid.NewGuid().ToString("D");
                    ids.Add(app.Id);
                }

                app.DisplayName = (app.DisplayName ?? string.Empty).Trim();
                app.LaunchPath = (app.LaunchPath ?? string.Empty).Trim();
                app.Arguments = app.Arguments ?? string.Empty;
                app.WorkingDirectory = (app.WorkingDirectory ?? string.Empty).Trim();
                app.SourceShortcutPath = (app.SourceShortcutPath ?? string.Empty).Trim();
                app.IconCachePath = (app.IconCachePath ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(app.DisplayName) && !string.IsNullOrWhiteSpace(app.LaunchPath))
                {
                    app.DisplayName = System.IO.Path.GetFileNameWithoutExtension(app.LaunchPath);
                }

                validApps.Add(app);
            }

            Apps = validApps;
        }
    }
}

