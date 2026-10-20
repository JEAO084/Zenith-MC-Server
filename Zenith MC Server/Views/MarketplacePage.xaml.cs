using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class MarketplacePage : Page
    {
        private MainWindow? _mainWindow;
        private int _currentPage = 1;

        public MarketplacePage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
            _ = RefreshCatalogAsync();
        }

        private async Task RefreshCatalogAsync()
        {
            if (_mainWindow == null || _mainWindow.ActiveProfile == null || GrdMarketplaceItems == null) return;

            string ver = (CmbVersion?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Versiones";
            if (ver == "Todas las Versiones") ver = _mainWindow.ActiveProfile.Version;

            string category = (CmbCategory?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Categorías";
            string loader = _mainWindow.ActiveProfile.SoftwareType;
            string source = (CmbSource?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Fuentes";
            string search = TxtSearch?.Text ?? "";
            string sort = (CmbSort != null && CmbSort.SelectedIndex == 1) ? "Name" : "Downloads";

            var items = await _mainWindow.MarketplaceService.GetFeaturedItemsAsync(
                MarketplaceCategory.Plugin, ver, loader, source, search, sort, category,
                overrideVersionFilter: "Todas", page: _currentPage, pageSize: 20);

            GrdMarketplaceItems.ItemsSource = items;

            if (TxtPageNumber != null) TxtPageNumber.Text = $"Página {_currentPage}";
            if (BtnPrevPage != null) BtnPrevPage.IsEnabled = _currentPage > 1;
        }

        private async void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await RefreshCatalogAsync();
            }
        }

        private async void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            await RefreshCatalogAsync();
        }

        private async void CmbSource_SelectionChanged(object sender, SelectionChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }
        private async void CmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }
        private async void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }
        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }
        private async void CmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }

        private async void BtnInstallItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarketplaceItem item && _mainWindow?.ActiveProfile != null)
            {
                btn.IsEnabled = false;
                btn.Content = "⏳";
                try
                {
                    await _mainWindow.MarketplaceService.DownloadAndInstallItemAsync(item, _mainWindow.ActiveProfile.DirectoryPath);
                    btn.Content = "✓";
                    _mainWindow.AppendConsoleLog($"[Zenith Marketplace] Instalado '{item.Title}' ({item.JarFileName}).");
                }
                catch (Exception ex)
                {
                    btn.Content = "Error";
                    _mainWindow.AppendConsoleLog($"[Zenith Marketplace Error] {ex.Message}");
                }
            }
        }
    }
}
