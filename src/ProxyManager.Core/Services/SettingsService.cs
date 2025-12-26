using System;
using System.IO;
using System.Text.Json;
using ProxyManager.Core.Models;

namespace ProxyManager.Core.Services
{
    public class SettingsService
    {
        private readonly string _filePath;

        public SettingsService(string filePath)
        {
            _filePath = filePath;
        }

        public AppSettings Load()
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch
            {
                // Если файл поврежден, возвращаем дефолт
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception)
            {
                // Логирование ошибки (можно добавить позже)
            }
        }
    }
}
