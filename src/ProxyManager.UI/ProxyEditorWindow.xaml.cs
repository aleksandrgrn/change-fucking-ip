using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using ProxyManager.Core.Models;
using ProxyManager.Core.Services;

namespace ProxyManager.UI
{
    public partial class ProxyEditorWindow : Window
    {
        private readonly ConfigRepository _configRepo;
        public ObservableCollection<ProxyConfig> Proxies { get; set; }

        public ProxyEditorWindow(ConfigRepository configRepo)
        {
            InitializeComponent();
            _configRepo = configRepo;

            // Загружаем данные
            var loaded = _configRepo.Load();
            Proxies = new ObservableCollection<ProxyConfig>(loaded);

            // Привязываем DataGrid
            GridProxies.ItemsSource = Proxies;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Добавляем пустую запись
            Proxies.Add(new ProxyConfig { Name = "Новый прокси", Address = "1.1.1.1:8080", Category = "General" });
            // Скроллим к ней
            if (Proxies.Count > 0)
            {
                GridProxies.ScrollIntoView(Proxies.Last());
                GridProxies.SelectedItem = Proxies.Last();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (GridProxies.SelectedItem is ProxyConfig selected)
            {
                var result = MessageBox.Show($"Удалить прокси '{selected.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    Proxies.Remove(selected);
                }
            }
            else
            {
                MessageBox.Show("Выберите строку для удаления.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Валидация (простая)
            foreach (var p in Proxies)
            {
                if (string.IsNullOrWhiteSpace(p.Address))
                {
                    MessageBox.Show($"Прокси '{p.Name}' не имеет адреса!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                // Сохраняем в файл
                _configRepo.Save(Proxies.ToList());
                DialogResult = true; // Возвращаем успех
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
