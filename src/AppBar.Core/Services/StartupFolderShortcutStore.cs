using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AppBar.Core.Services
{
    public interface IStartupShortcutStore
    {
        bool IsCurrent(string executablePath);
        void SetCurrent(string executablePath);
        void Remove();
    }

    public class StartupFolderShortcutStore : IStartupShortcutStore
    {
        private const string ShortcutName = "AppBar.lnk";

        public StartupFolderShortcutStore()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (string.IsNullOrWhiteSpace(startupFolder))
            {
                startupFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Startup");
            }

            ShortcutPath = Path.Combine(startupFolder, ShortcutName);
        }

        public string ShortcutPath { get; private set; }

        public bool IsCurrent(string executablePath)
        {
            if (!File.Exists(ShortcutPath) || string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            object shell = null;
            object shortcut = null;
            try
            {
                shell = CreateShell();
                shortcut = CreateShortcut(shell, ShortcutPath);
                string target = Convert.ToString(InvokeGetProperty(shortcut, "TargetPath"));
                if (string.IsNullOrWhiteSpace(target))
                {
                    return false;
                }

                return string.Equals(
                    Path.GetFullPath(Environment.ExpandEnvironmentVariables(target)),
                    Path.GetFullPath(executablePath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        public void SetCurrent(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                throw new FileNotFoundException("应用栏程序不存在。", executablePath);
            }

            string directory = Path.GetDirectoryName(ShortcutPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            object shell = null;
            object shortcut = null;
            try
            {
                shell = CreateShell();
                shortcut = CreateShortcut(shell, ShortcutPath);
                InvokeSetProperty(shortcut, "TargetPath", Path.GetFullPath(executablePath));
                InvokeSetProperty(shortcut, "WorkingDirectory", Path.GetDirectoryName(executablePath));
                InvokeSetProperty(shortcut, "Description", "应用栏");
                InvokeSetProperty(shortcut, "IconLocation", executablePath + ",0");
                InvokeMethod(shortcut, "Save");
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        public void Remove()
        {
            if (File.Exists(ShortcutPath))
            {
                File.Delete(ShortcutPath);
            }
        }

        private static object CreateShell()
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                throw new InvalidOperationException("系统无法创建启动快捷方式。");
            }

            return Activator.CreateInstance(shellType);
        }

        private static object CreateShortcut(object shell, string path)
        {
            return shell.GetType().InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { path });
        }

        private static object InvokeGetProperty(object target, string propertyName)
        {
            return target.GetType().InvokeMember(
                propertyName,
                BindingFlags.GetProperty,
                null,
                target,
                null);
        }

        private static void InvokeSetProperty(object target, string propertyName, object value)
        {
            target.GetType().InvokeMember(
                propertyName,
                BindingFlags.SetProperty,
                null,
                target,
                new[] { value });
        }

        private static void InvokeMethod(object target, string methodName)
        {
            target.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod,
                null,
                target,
                null);
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
    }
}
