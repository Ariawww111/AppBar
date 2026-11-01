using System;
using System.Diagnostics;
using System.IO;
using AppBar.Core.Models;

namespace AppBar.Core.Services
{
    public class AppLauncher
    {
        public AppLaunchResult Launch(AppShortcut shortcut)
        {
            if (shortcut == null)
            {
                return AppLaunchResult.Failed("应用信息无效。");
            }

            try
            {
                string executable = shortcut.LaunchPath;
                if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = shortcut.Arguments ?? string.Empty,
                        UseShellExecute = true
                    };

                    if (!string.IsNullOrWhiteSpace(shortcut.WorkingDirectory) &&
                        Directory.Exists(shortcut.WorkingDirectory))
                    {
                        startInfo.WorkingDirectory = shortcut.WorkingDirectory;
                    }
                    else if (Directory.Exists(Path.GetDirectoryName(executable)))
                    {
                        startInfo.WorkingDirectory = Path.GetDirectoryName(executable);
                    }

                    Process.Start(startInfo);
                    return AppLaunchResult.Succeeded();
                }

                if (!string.IsNullOrWhiteSpace(shortcut.SourceShortcutPath) &&
                    File.Exists(shortcut.SourceShortcutPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = shortcut.SourceShortcutPath,
                        UseShellExecute = true
                    });
                    return AppLaunchResult.Succeeded();
                }

                return AppLaunchResult.Failed("找不到目标程序，文件可能已移动或删除。");
            }
            catch (Exception ex)
            {
                return AppLaunchResult.Failed("无法启动应用：" + ex.Message);
            }
        }
    }

    public class AppLaunchResult
    {
        private AppLaunchResult(bool success, string errorMessage)
        {
            Success = success;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }

        public static AppLaunchResult Succeeded()
        {
            return new AppLaunchResult(true, string.Empty);
        }

        public static AppLaunchResult Failed(string message)
        {
            return new AppLaunchResult(false, message);
        }
    }
}

