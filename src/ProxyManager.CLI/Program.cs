using System.Runtime.Versioning;
using System;
using ProxyManager.Core.Services;

[assembly: SupportedOSPlatform("windows")]

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("--- ТЕСТ РЕЕСТРА ---");

        var service = new RegistryProxyService();
        var testIp = "127.0.0.1:8888";

        Console.WriteLine($"Включаем прокси: {testIp}");
        Console.WriteLine("Нажми ENTER...");
        Console.ReadLine();

        try
        {
            service.SetProxy(testIp);
            Console.WriteLine("✅ Прокси включен! Проверяй настройки Windows.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
        }

        Console.WriteLine("\nНажми ENTER для отключения...");
        Console.ReadLine();

        service.DisableProxy();
        Console.WriteLine("❌ Прокси отключен.");
    }
}