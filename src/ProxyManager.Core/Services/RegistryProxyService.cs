using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ProxyManager.Core.Services
{
    [SupportedOSPlatform("windows")] // Указываем, что код только для Windows
    public class RegistryProxyService : ISystemProxyService
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        // --- P/Invoke Magic для мгновенного применения настроек ---
        [DllImport("wininet.dll")]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        public void SetProxy(string address, string? exceptions = null)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null) return;

            // 1. Включаем прокси (1 = включено)
            key.SetValue("ProxyEnable", 1);

            // 2. Устанавливаем адрес
            key.SetValue("ProxyServer", address);

            // 3. Устанавливаем исключения (ТОЛЬКО если переданы явно)
            // Если передан null - не трогаем текущие настройки исключений в реестре
            if (exceptions != null)
            {
                var finalExceptions = exceptions.Contains("<local>") ? exceptions : $"{exceptions};<local>";
                key.SetValue("ProxyOverride", finalExceptions);
            }

            // 4. Применяем изменения
            RefreshSystemSettings();
        }

        public void DisableProxy()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key != null)
            {
                // 0 = выключено
                key.SetValue("ProxyEnable", 0);
            }

            RefreshSystemSettings();
        }

        public void ClearProxy() => DisableProxy();

        public string? GetCurrentProxyAddress()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            return key?.GetValue("ProxyServer")?.ToString();
        }

        public bool IsProxyEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            var value = key?.GetValue("ProxyEnable");

            if (value is int intVal) return intVal == 1;
            return false;
        }

        public string? GetProxyExceptions()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            return key?.GetValue("ProxyOverride")?.ToString();
        }

        private static void RefreshSystemSettings()
        {
            // Уведомляем систему, что настройки изменились
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
    }
}