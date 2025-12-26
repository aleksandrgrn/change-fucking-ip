namespace ProxyManager.Core.Services
{
    public interface ISystemProxyService
    {
        /// <summary>
        /// Включает прокси с заданными параметрами
        /// </summary>
        /// <param name="address">Адрес и порт (напр. "192.168.1.1:8080")</param>
        /// <param name="exceptions">Список исключений (напр. "localhost;127.0.0.1;*.corp")</param>
        void SetProxy(string address, string exceptions = "");

        /// <summary>
        /// Полностью отключает использование прокси (Direct connection)
        /// </summary>
        void DisableProxy();

        /// <summary>
        /// Получает текущий адрес прокси из реестра (для проверки)
        /// </summary>
        string? GetCurrentProxyAddress();
    }
}