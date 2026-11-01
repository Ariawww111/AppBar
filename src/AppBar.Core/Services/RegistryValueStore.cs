using Microsoft.Win32;

namespace AppBar.Core.Services
{
    public interface IRegistryValueStore
    {
        string GetValue(string keyPath, string valueName);
        void SetValue(string keyPath, string valueName, string value);
        void DeleteValue(string keyPath, string valueName);
    }

    public class CurrentUserRegistryValueStore : IRegistryValueStore
    {
        public string GetValue(string keyPath, string valueName)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, false))
            {
                return key == null ? null : key.GetValue(valueName) as string;
            }
        }

        public void SetValue(string keyPath, string valueName, string value)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (key == null)
                {
                    throw new System.InvalidOperationException("无法打开当前用户启动项。");
                }

                key.SetValue(valueName, value, RegistryValueKind.String);
            }
        }

        public void DeleteValue(string keyPath, string valueName)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(valueName, false);
                }
            }
        }
    }
}

