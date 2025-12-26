using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProxyManager.Core.Models;

namespace ProxyManager.Core.Services
{
    public class ConfigRepository
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigRepository(string filePath)
        {
            _filePath = filePath;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Проверяет, существует ли файл конфигурации.
        /// </summary>
        public bool Exists() => File.Exists(_filePath);

        /// <summary>
        /// Загружает список прокси из JSON-файла.
        /// Поддерживает два формата:
        /// 1. Старый: { "version": ..., "proxies": [...] }
        /// 2. Новый: [...]
        /// </summary>
        public List<ProxyConfig> Load()
        {
            if (!File.Exists(_filePath))
            {
                return new List<ProxyConfig>();
            }

            try
            {
                var json = File.ReadAllText(_filePath);

                // Пробуем определить формат JSON
                var doc = JsonNode.Parse(json);

                if (doc is JsonArray)
                {
                    // Новый формат: просто массив
                    return JsonSerializer.Deserialize<List<ProxyConfig>>(json, _jsonOptions)
                           ?? new List<ProxyConfig>();
                }
                else if (doc is JsonObject obj && obj.ContainsKey("proxies"))
                {
                    // Старый формат: { "proxies": [...] }
                    var proxiesNode = obj["proxies"];
                    if (proxiesNode != null)
                    {
                        var proxiesJson = proxiesNode.ToJsonString();
                        return JsonSerializer.Deserialize<List<ProxyConfig>>(proxiesJson, _jsonOptions)
                               ?? new List<ProxyConfig>();
                    }
                }

                return new List<ProxyConfig>();
            }
            catch
            {
                return new List<ProxyConfig>();
            }
        }

        /// <summary>
        /// Сохраняет список прокси в JSON-файл (новый формат — просто массив).
        /// </summary>
        public void Save(List<ProxyConfig> proxies)
        {
            var json = JsonSerializer.Serialize(proxies, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
    }
}