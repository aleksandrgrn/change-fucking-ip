using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ProxyManager.Core.Models;

namespace ProxyManager.Core.Services
{
    /// <summary>
    /// Сервис конвертации legacy VBS-скриптов в список ProxyConfig.
    /// </summary>
    public class VbsConverterService
    {
        // Regex для поиска IP:Port
        private static readonly Regex IpRegex = new(
            @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):(\d+)",
            RegexOptions.Compiled);

        /// <summary>
        /// Конвертирует все VBS-файлы из указанной папки в список ProxyConfig.
        /// </summary>
        /// <param name="folderPath">Путь к папке с VBS-файлами.</param>
        /// <returns>Список ProxyConfig.</returns>
        public List<ProxyConfig> Convert(string folderPath)
        {
            var result = new List<ProxyConfig>();

            if (!Directory.Exists(folderPath))
                return result;

            var files = Directory.GetFiles(folderPath, "*.vbs");
            var parentFolder = new DirectoryInfo(folderPath).Name;

            foreach (var file in files)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var match = IpRegex.Match(content);

                    if (match.Success)
                    {
                        string ip = match.Groups[1].Value;
                        string portStr = match.Groups[2].Value;
                        int port = int.Parse(portStr);

                        var proxy = new ProxyConfig
                        {
                            Category = parentFolder,
                            Name = Path.GetFileNameWithoutExtension(file),
                            Address = $"{ip}:{port}",
                            Port = port
                        };

                        result.Add(proxy);
                    }
                    // Если IP не найден — просто пропускаем файл
                }
                catch
                {
                    // Файл битый — пропускаем
                }
            }

            return result;
        }
    }
}
