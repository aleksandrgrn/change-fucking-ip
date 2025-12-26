using System.Text.Json.Serialization;

namespace ProxyManager.Core.Models
{
    public class ProxyConfig
    {
        // Эти свойства ждет UI
        public string Category { get; set; } = "General";
        public string Name { get; set; } = "Unknown";
        public string Address { get; set; } = "";
        public int Port { get; set; } = 8080;

        // Если в JSON ключи называются по-другому, можно добавить атрибуты, например:
        // [JsonPropertyName("ip")]
        // public string Ip { get; set; } 
    }
}