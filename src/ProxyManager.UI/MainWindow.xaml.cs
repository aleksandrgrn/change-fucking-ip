using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ProxyManager.Core.Models;
using ProxyManager.Core.Services;

namespace ProxyManager.UI
{
    public partial class MainWindow : Window
    {
        private readonly ConfigRepository _configRepo;
        private readonly RegistryProxyService _proxyService;
        private readonly VbsConverterService _vbsConverter;
        private readonly IpCheckService _ipCheckService;
        private List<ProxyConfig> _allProxies = new();
        private List<ProxyConfig> _filteredProxies = new();

        private static readonly string ConfigFileName = Path.Combine(AppContext.BaseDirectory, "proxies.json");

        private string? _currentAppliedProxy = null;
        private bool _initialProxyEnabled;
        private string? _initialProxyAddress;
        private string? _initialProxyExceptions;

        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;
        private static readonly string SettingsFileName = Path.Combine(AppContext.BaseDirectory, "settings.json");

        private System.Windows.Threading.DispatcherTimer _watchdogTimer;

        public MainWindow()
        {
            InitializeComponent();

            _configRepo = new ConfigRepository(ConfigFileName);
            _proxyService = new RegistryProxyService();
            _vbsConverter = new VbsConverterService();
            _ipCheckService = new IpCheckService();
            _settingsService = new SettingsService(SettingsFileName);
            _appSettings = _settingsService.Load();

            // Инициализация Watchdog таймера (раз в 5 секунд)
            _watchdogTimer = new System.Windows.Threading.DispatcherTimer();
            _watchdogTimer.Tick += WatchdogTimer_Tick;
            _watchdogTimer.Interval = TimeSpan.FromSeconds(5);
            _watchdogTimer.Start();

            Log("Запуск программы...");
            Log("Watchdog запущен (интервал 5 сек).");

            // Сохраняем исходные настройки прокси
            try
            {
                _initialProxyEnabled = _proxyService.IsProxyEnabled();
                _initialProxyAddress = _proxyService.GetCurrentProxyAddress();
                _initialProxyExceptions = _proxyService.GetProxyExceptions();

                if (_initialProxyEnabled)
                    Log($"Исходное состояние: Прокси включен ({_initialProxyAddress})");
                else
                    Log("Исходное состояние: Прокси отключен/не задан");
            }
            catch (Exception ex)
            {
                Log($"Ошибка чтения настроек: {ex.Message}");
            }

            LoadData();

            // Проверка и предложение сохранить Default Proxy, если еще об этом не думали
            CheckAndPromptDefaultProxy();

            Log("Ожидание действий от семьи...");

            // Фокус на поле ввода прокси
            CmbProxies.Focus();
        }

        private void CheckAndPromptDefaultProxy()
        {
            if (_appSettings.IsConfigured) return;

            // Если настройки еще не сохранены (первый запуск с новой фичей)
            // Спрашиваем пользователя
            string currentState = _initialProxyEnabled
                ? $"Прокси включен ({_initialProxyAddress})"
                : "Прямое соединение (без прокси)";

            var result = MessageBox.Show(
                $"Текущее состояние сети: {currentState}.\n\n" +
                "Хотите запомнить это состояние как \"По умолчанию\" для кнопки Сброс?\n" +
                "Это состояние будет восстанавливаться даже после перезапуска программы.",
                "Настройка по умолчанию",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (_initialProxyEnabled)
                {
                    _appSettings.IsDefaultDirect = false;
                    _appSettings.DefaultProxyAddress = _initialProxyAddress;
                }
                else
                {
                    _appSettings.IsDefaultDirect = true;
                    _appSettings.DefaultProxyAddress = null;
                }

                _appSettings.IsConfigured = true;
                _settingsService.Save(_appSettings);
                Log("✅ Состояние по умолчанию сохранено.");
            }
            // Если Нет - просто оставляем IsConfigured = false, спросим потом или в момент сброса
        }

        private void WatchdogTimer_Tick(object? sender, EventArgs e)
        {
            // Проверяем, активна ли галочка Watchdog
            if (ChkWatchdog.IsChecked != true) return;

            // Если приложение считает, что прокси должен быть применен
            if (!string.IsNullOrEmpty(_currentAppliedProxy))
            {
                bool needRestore = false;

                // Читаем текущее состояние реестра
                bool isEnabled = _proxyService.IsProxyEnabled();
                string? address = _proxyService.GetCurrentProxyAddress();

                if (!isEnabled)
                {
                    // Прокси выключен (кто-то вырубил галочку)
                    needRestore = true;
                }
                else if (!string.Equals(address, _currentAppliedProxy, StringComparison.OrdinalIgnoreCase))
                {
                    // Адрес изменился (кто-то поменял адрес)
                    needRestore = true;
                }

                if (needRestore)
                {
                    Log($"⚠ Watchdog: настройки сбиты! Восстанавливаю {_currentAppliedProxy}...");
                    try
                    {
                        // Передаем null, чтобы НЕ затирать текущие исключения (оставляем как есть)
                        _proxyService.SetProxy(_currentAppliedProxy, null);

                        Log("✅ Watchdog: прокси успешно восстановлен.");
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Watchdog Error: {ex.Message}");
                    }
                }
            }
        }

        private void LoadData()
        {


            // Проверяем, существует ли файл конфигурации
            if (!_configRepo.Exists())
            {
                Log("Файл proxies.json не найден. Запуск первичной настройки...");
                HandleFirstRun();
            }
            else
            {
                LoadProxies();
            }
        }

        private void HandleFirstRun()
        {
            var result = MessageBox.Show(
                "База прокси не найдена.\n\nХотите импортировать из старых VBS-файлов?",
                "Первый запуск",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ImportFromVbs();
            }
            else
            {
                Log("Импорт отменён. Список прокси пуст.");
                _allProxies = new List<ProxyConfig>();
                UpdateProxyList();
            }
        }

        private void ImportFromVbs()
        {
            // Используем OpenFolderDialog (WPF/.NET 8)
            var dialog = new OpenFolderDialog
            {
                Title = "Выберите папку с VBS-файлами",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                var folderPath = dialog.FolderName;
                Log($"Импорт из папки: {folderPath}");

                try
                {
                    var proxies = _vbsConverter.Convert(folderPath);

                    if (proxies.Count == 0)
                    {
                        Log("⚠️ VBS-файлы не найдены или не содержат IP-адресов.");
                        MessageBox.Show("В выбранной папке не найдено VBS-файлов с прокси.",
                                        "Импорт", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Сохраняем в JSON
                    _configRepo.Save(proxies);
                    Log($"✅ Импортировано {proxies.Count} прокси. Сохранено в {ConfigFileName}");

                    // Загружаем в UI
                    _allProxies = proxies;
                    UpdateProxyList();

                    MessageBox.Show($"Успешно импортировано {proxies.Count} прокси!",
                                    "Импорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Log($"❌ Ошибка импорта: {ex.Message}");
                    MessageBox.Show($"Ошибка при импорте:\n{ex.Message}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                Log("Выбор папки отменён.");
            }
        }

        private void LoadProxies()
        {
            try
            {
                _allProxies = _configRepo.Load();
                if (_allProxies.Count == 0)
                {
                    Log("⚠️ Файл proxies.json пуст.");
                    return;
                }

                UpdateProxyList();

                Log($"Загружено прокси: {_filteredProxies.Count} шт.");
            }
            catch (Exception ex)
            {
                Log($"Критическая ошибка загрузки: {ex.Message}");
            }
        }

        private void UpdateProxyList()
        {
            // Фильтруем пустые элементы (включая строки из пробелов) и сортируем
            _filteredProxies = _allProxies
                .Where(p => !string.IsNullOrWhiteSpace(p.Name?.Trim()) &&
                            !string.IsNullOrWhiteSpace(p.Address?.Trim()))
                .OrderBy(p => p.Name)
                .ToList();
            CmbProxies.ItemsSource = _filteredProxies;
            CmbProxies.DisplayMemberPath = "Name";
            // Не выбираем элемент по умолчанию — поле пустое, ждёт ввода
        }

        // При открытии dropdown — ВСЕГДА показываем ВСЕ элементы
        private void CmbProxies_DropDownOpened(object sender, EventArgs e)
        {
            // Сохраняем текущий выбранный элемент
            var selectedItem = CmbProxies.SelectedItem;

            // Показываем ПОЛНЫЙ список (без пустых)
            _filteredProxies = _allProxies
                .Where(p => !string.IsNullOrWhiteSpace(p.Name?.Trim()) &&
                            !string.IsNullOrWhiteSpace(p.Address?.Trim()))
                .OrderBy(p => p.Name)
                .ToList();

            // Принудительно очищаем и обновляем
            CmbProxies.ItemsSource = null;
            CmbProxies.Items.Clear();
            CmbProxies.ItemsSource = _filteredProxies;

            // Восстанавливаем выбор
            if (selectedItem != null)
            {
                CmbProxies.SelectedItem = selectedItem;
            }
        }

        private void BtnEditProxies_Click(object sender, RoutedEventArgs e)
        {
            // Открываем редактор
            var editor = new ProxyEditorWindow(_configRepo);
            editor.Owner = this; // Чтобы было поверх

            bool? result = editor.ShowDialog();

            if (result == true)
            {
                // Если сохранили изменения -> перезагружаем список
                Log("ℹ️ Список прокси был обновлен.");
                LoadProxies();
            }
        }



        // Enter в ComboBox = применить прокси
        private async void CmbProxies_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await ApplySelectedProxy();
            }
        }

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            await ApplySelectedProxy();
        }

        private async Task ApplySelectedProxy()
        {
            var proxy = CmbProxies.SelectedItem as ProxyConfig;

            if (proxy == null && !string.IsNullOrWhiteSpace(CmbProxies.Text))
            {
                proxy = _allProxies.FirstOrDefault(p =>
                    p.Name.Equals(CmbProxies.Text, StringComparison.OrdinalIgnoreCase));
            }

            if (proxy == null)
            {
                Log("⚠️ Прокси не выбран или не найден!");
                return;
            }

            try
            {
                // Address уже содержит порт (http://ip:port)
                string fullAddress = proxy.Address;
                Log($"Применяю прокси: {proxy.Name} ({fullAddress})...");

                // Передаем null, чтобы НЕ менять список исключений (оставляем то, что сейчас в системе)
                _proxyService.SetProxy(fullAddress, null);

                // Запоминаем текущий прокси для IpCheckService
                _currentAppliedProxy = fullAddress;

                Log("✅ УСПЕШНО: Прокси установлен в реестре.");

                // Автопроверка IP после смены
                await CheckIpAsync();
            }
            catch (Exception ex)
            {
                Log($"❌ ОШИБКА: {ex.Message}");
            }
        }

        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Если настройки "по умолчанию" не заданы, спросим сейчас
                if (!_appSettings.IsConfigured)
                {
                    // Получаем текущее состояние
                    bool currentEnabled = _proxyService.IsProxyEnabled();
                    string? currentAddr = _proxyService.GetCurrentProxyAddress();

                    string statusMsg = currentEnabled
                        ? $"ТЕКУЩИЙ ПРОКСИ: {currentAddr}"
                        : "ТЕКУЩЕЕ СОСТОЯНИЕ: Прямое соединение (без прокси)";

                    var result = MessageBox.Show(
                        $"Прокси по умолчанию еще не задан.\n\n" +
                        $"{statusMsg}\n\n" +
                        "Хотите сохранить ЭТО состояние как дефолтное для кнопки Сброс?",
                        "Сохранение настроек",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) return;

                    if (result == MessageBoxResult.Yes)
                    {
                        if (currentEnabled)
                        {
                            _appSettings.IsDefaultDirect = false;
                            _appSettings.DefaultProxyAddress = currentAddr;
                        }
                        else
                        {
                            _appSettings.IsDefaultDirect = true;
                            _appSettings.DefaultProxyAddress = null;
                        }
                        _appSettings.IsConfigured = true;
                        _settingsService.Save(_appSettings);
                        Log("✅ Текущие настройки сохранены как 'По умолчанию'.");
                    }
                    // Если No — не сохраняем, просто выполняем сброс (используем логику ниже)
                }

                // ВЫПОЛНЕНИЕ СБРОСА
                if (_appSettings.IsConfigured)
                {
                    // У нас есть сохраненный пресет
                    if (_appSettings.IsDefaultDirect)
                    {
                        Log("Отключаю прокси (По умолчанию: Direct)...");
                        _proxyService.DisableProxy();
                        _currentAppliedProxy = null;
                        Log("✅ Прокси отключен.");
                    }
                    else if (!string.IsNullOrEmpty(_appSettings.DefaultProxyAddress))
                    {
                        Log($"Восстанавливаю прокси по умолчанию: {_appSettings.DefaultProxyAddress}...");

                        // Используем сохраненные при старте исключения (или дефолт), но не меняем их
                        // (Важное условие: исключения не трогаем, только IP)
                        string exceptions = _initialProxyExceptions ?? "localhost;127.0.0.1";
                        _proxyService.SetProxy(_appSettings.DefaultProxyAddress, exceptions);

                        _currentAppliedProxy = _appSettings.DefaultProxyAddress;
                        Log("✅ Прокси по умолчанию восстановлен.");
                    }
                }
                else
                {
                    // Fallback: Если пользователь отказался сохранять, просто вырубаем прокси (Direct)
                    Log("Сброс на прямое соединение...");
                    _proxyService.DisableProxy();
                    _currentAppliedProxy = null;
                    Log("✅ Прокси отключен.");
                }

                // Автопроверка IP после сброса
                await CheckIpAsync();
            }
            catch (Exception ex)
            {
                Log($"❌ ОШИБКА: {ex.Message}");
            }
        }

        private void BtnClearDefault_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы точно хотите забыть настройки 'Прокси по умолчанию'?\nПри следующем запуске/сбросе программа снова спросит вас.",
                "Сброс настроек", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _appSettings = new AppSettings(); // сброс в дефолт (Configured=false)
                _settingsService.Save(_appSettings);
                Log("ℹ️ Настройки 'Прокси по умолчанию' очищены.");
            }
        }

        private async void BtnCheckIp_Click(object sender, RoutedEventArgs e)
        {
            await CheckIpAsync();
        }

        private async Task CheckIpAsync()
        {
            try
            {
                TxtIp.Text = "Проверка...";
                TxtLocation.Text = "-";
                TxtHostname.Text = "-";
                Log("⏳ Проверка IP-адреса...");

                // Передаем текущий прокси (если есть), чтобы HttpClient использовал именно его
                // игнорируя кэши системы
                IpInfo? info = null;

                // 1. Пробуем через текущий (или заданный) прокси
                try
                {
                    info = await _ipCheckService.GetIpInfoAsync(_currentAppliedProxy);
                }
                catch (Exception ex)
                {
                    Log($"Сбой основной проверки: {ex.Message}");
                }

                if (info != null)
                {
                    TxtIp.Text = info.Ip;
                    TxtLocation.Text = info.Location;

                    // Запрос Hostname
                    var host = await _ipCheckService.GetHostnameAsync(info.Ip);
                    TxtHostname.Text = host;

                    Log($"✅ IP: {info.Ip} | {info.Location}");
                }
                else
                {
                    TxtIp.Text = "Ошибка";
                    Log("❌ Не удалось получить информацию об IP (прокси недоступен?).");
                }
            }
            catch (Exception ex)
            {
                TxtIp.Text = "Ошибка";
                Log($"❌ Ошибка проверки IP: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            ListLog.Items.Add($"[{time}] {message}");

            // Автопрокрутка к последней записи
            if (ListLog.Items.Count > 0)
            {
                ListLog.ScrollIntoView(ListLog.Items[ListLog.Items.Count - 1]);
            }
        }
    }
}