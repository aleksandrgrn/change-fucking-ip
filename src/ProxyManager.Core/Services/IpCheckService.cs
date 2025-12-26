using System.Net.Http.Json;
using System.Net;
using ProxyManager.Core.Models;

namespace ProxyManager.Core.Services
{
    public class IpCheckService
    {
        public async Task<IpInfo?> GetIpInfoAsync(string? proxyAddress = null)
        {
            try
            {
                var handler = new HttpClientHandler();

                if (!string.IsNullOrEmpty(proxyAddress))
                {
                    var proxy = new WebProxy(proxyAddress);
                    // Важно: явно задаем дефолтные креды для самого прокси, как в старой версии
                    proxy.Credentials = CredentialCache.DefaultNetworkCredentials;

                    handler.Proxy = proxy;
                    handler.UseProxy = true;
                }

                // Используем дефолтные учетные данные (для корп. прокси)
                handler.UseDefaultCredentials = true;

                // Создаем HttpClient с явно заданным (или дефолтным) handler
                using var httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // Используем ip-api.com (бесплатный, без ключа)
                var info = await httpClient.GetFromJsonAsync<IpInfo>("http://ip-api.com/json/");
                return info;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> GetHostnameAsync(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return "-";

            try
            {
                var entry = await Dns.GetHostEntryAsync(ip);
                return entry.HostName;
            }
            catch
            {
                return "-";
            }
        }
    }
}
