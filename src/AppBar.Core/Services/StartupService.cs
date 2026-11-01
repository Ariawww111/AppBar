using System;

namespace AppBar.Core.Services
{
    public class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AppBar";
        private readonly string _executablePath;
        private readonly IRegistryValueStore _registry;

        public StartupService(string executablePath)
            : this(executablePath, new CurrentUserRegistryValueStore())
        {
        }

        public StartupService(string executablePath, IRegistryValueStore registry)
        {
            _executablePath = executablePath;
            if (registry == null)
            {
                throw new ArgumentNullException("registry");
            }

            _registry = registry;
        }

        public bool IsEnabled()
        {
            string value = _registry.GetValue(RunKeyPath, ValueName);
            return string.Equals(value, BuildCommand(), StringComparison.OrdinalIgnoreCase);
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                _registry.SetValue(RunKeyPath, ValueName, BuildCommand());
            }
            else
            {
                _registry.DeleteValue(RunKeyPath, ValueName);
            }
        }

        private string BuildCommand()
        {
            return "\"" + _executablePath + "\"";
        }
    }
}

