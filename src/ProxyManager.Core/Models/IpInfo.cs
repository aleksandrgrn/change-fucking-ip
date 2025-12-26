using System.Text.Json.Serialization;

namespace ProxyManager.Core.Models
{
    public class IpInfo
    {
        [JsonPropertyName("query")]
        public string? Ip { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("isp")]
        public string? Isp { get; set; }

        [JsonPropertyName("org")]
        public string? Organization { get; set; }

        public string Location => $"{City}, {Country}";
    }
}
