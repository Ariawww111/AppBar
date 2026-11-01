using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using AppBar.Core.Models;

namespace AppBar.Core.Services
{
    public class ShortcutResolver
    {
        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(new[] { ".lnk", ".exe" }, StringComparer.OrdinalIgnoreCase);

        public bool IsSupported(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && SupportedExtensions.Contains(Path.GetExtension(path));
        }

        public ShortcutResolutionResult Resolve(string sourcePath)
        {
            if (!IsSupported(sourcePath))
            {
                return ShortcutResolutionResult.Failed("仅支持 .lnk 快捷方式和 .exe 程序。");
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(sourcePath);
            }
            catch (Exception)
            {
                return ShortcutResolutionResult.Failed("快捷方式路径无效。");
            }

            if (!File.Exists(fullPath))
            {
                return ShortcutResolutionResult.Failed("快捷方式文件不存在。");
            }

            string extension = Path.GetExtension(fullPath);
            if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveExecutable(fullPath);
            }

            return ResolveLink(fullPath);
        }

        public static string NormalizeTargetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        public static bool ContainsTarget(IEnumerable<AppShortcut> shortcuts, string launchPath)
        {
            string candidate = NormalizeTargetPath(launchPath);
            foreach (AppShortcut shortcut in shortcuts)
            {
                if (shortcut != null &&
                    string.Equals(
                        NormalizeTargetPath(shortcut.LaunchPath),
                        candidate,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static ShortcutResolutionResult ResolveExecutable(string path)
        {
            return ShortcutResolutionResult.Succeeded(new AppShortcut
            {
                DisplayName = Path.GetFileNameWithoutExtension(path),
                LaunchPath = path,
                WorkingDirectory = Path.GetDirectoryName(path),
                SourceShortcutPath = path
            });
        }

        private static ShortcutResolutionResult ResolveLink(string path)
        {
            object shell = null;
            object link = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return ShortcutResolutionResult.Failed("系统无法解析 .lnk 快捷方式。");
                }

                shell = Activator.CreateInstance(shellType);
                link = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { path });

                Type linkType = link.GetType();
                string target = Convert.ToString(
                    linkType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null));
                string arguments = Convert.ToString(
                    linkType.InvokeMember("Arguments", BindingFlags.GetProperty, null, link, null));
                string workingDirectory = Convert.ToString(
                    linkType.InvokeMember("WorkingDirectory", BindingFlags.GetProperty, null, link, null));

                target = Environment.ExpandEnvironmentVariables((target ?? string.Empty).Trim());
                if (string.IsNullOrWhiteSpace(target) ||
                    !string.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(target))
                {
                    return ShortcutResolutionResult.Failed(
                        "该快捷方式没有可用的桌面程序目标。首版不支持商店应用或脚本。");
                }

                string working = Environment.ExpandEnvironmentVariables((workingDirectory ?? string.Empty).Trim());
                if (string.IsNullOrWhiteSpace(working) || !Directory.Exists(working))
                {
                    working = Path.GetDirectoryName(target);
                }

                return ShortcutResolutionResult.Succeeded(new AppShortcut
                {
                    DisplayName = Path.GetFileNameWithoutExtension(path),
                    LaunchPath = Path.GetFullPath(target),
                    Arguments = arguments ?? string.Empty,
                    WorkingDirectory = working,
                    SourceShortcutPath = path
                });
            }
            catch (Exception ex)
            {
                return ShortcutResolutionResult.Failed("无法读取快捷方式：" + ex.Message);
            }
            finally
            {
                ReleaseComObject(link);
                ReleaseComObject(shell);
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
    }

    public class ShortcutResolutionResult
    {
        private ShortcutResolutionResult(bool success, AppShortcut shortcut, string errorMessage)
        {
            Success = success;
            Shortcut = shortcut;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; private set; }
        public AppShortcut Shortcut { get; private set; }
        public string ErrorMessage { get; private set; }

        public static ShortcutResolutionResult Succeeded(AppShortcut shortcut)
        {
            return new ShortcutResolutionResult(true, shortcut, string.Empty);
        }

        public static ShortcutResolutionResult Failed(string message)
        {
            return new ShortcutResolutionResult(false, null, message);
        }
    }
}

