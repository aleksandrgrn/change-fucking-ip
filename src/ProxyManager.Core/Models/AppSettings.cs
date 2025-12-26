namespace ProxyManager.Core.Models
{
    public class AppSettings
    {
        public bool IsConfigured { get; set; } = false;
        public bool IsDefaultDirect { get; set; } = false;
        public string? DefaultProxyAddress { get; set; }
    }
}
