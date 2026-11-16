using System;

namespace AppBar.Core.Services
{
    public class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AppBar";
        private readonly string _executablePath;
        private readonly IRegistryValueStore _registry;
        private readonly IStartupShortcutStore _shortcut;

        public StartupService(string executablePath)
            : this(
                executablePath,
                new CurrentUserRegistryValueStore(),
                new StartupFolderShortcutStore())
        {
        }

        public StartupService(
            string executablePath,
            IRegistryValueStore registry,
            IStartupShortcutStore shortcut)
        {
            _executablePath = executablePath;
            if (registry == null)
            {
                throw new ArgumentNullException("registry");
            }

            if (shortcut == null)
            {
                throw new ArgumentNullException("shortcut");
            }

            _registry = registry;
            _shortcut = shortcut;
        }

        public bool IsEnabled()
        {
            string value = _registry.GetValue(RunKeyPath, ValueName);
            bool registryEnabled =
                string.Equals(value, BuildCommand(), StringComparison.OrdinalIgnoreCase);
            return registryEnabled || _shortcut.IsCurrent(_executablePath);
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                _registry.SetValue(RunKeyPath, ValueName, BuildCommand());
                _shortcut.SetCurrent(_executablePath);
            }
            else
            {
                _registry.DeleteValue(RunKeyPath, ValueName);
                _shortcut.Remove();
            }
        }

        private string BuildCommand()
        {
            return "\"" + _executablePath + "\"";
        }
    }
}

