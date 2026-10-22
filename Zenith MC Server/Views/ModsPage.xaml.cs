using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class ModsPage : Page
    {
        private MainWindow? _mainWindow;
        private int _currentPage = 1;
        private bool _isInitialized = false;

        public ModsPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
            if (!_isInitialized)
            {
                _isInitialized = true;
                ConfigureDefaultFilters();
                _ = RefreshCatalogAsync();
            }
        }

        private void ConfigureDefaultFilters()
        {
            if (_mainWindow?.ActiveProfile == null) return;
            var profile = _mainWindow.ActiveProfile;

            if (TxtActiveServerFilterBadge != null)
            {
                TxtActiveServerFilterBadge.Text = $"Servidor: {profile.SoftwareType} {profile.Version}";
            }
        }

        private async Task RefreshCatalogAsync()
        {
            if (_mainWindow?.ActiveProfile == null || GrdModItems == null) return;

            string ver = _mainWindow.ActiveProfile.Version;
            string loader = _mainWindow.ActiveProfile.SoftwareType;
            string source = (CmbSource?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Fuentes";
            string search = TxtSearch?.Text ?? "";
            string category = (CmbCategory?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Categorías";

            var items = await _mainWindow.MarketplaceService.GetFeaturedItemsAsync(
                MarketplaceCategory.Mod, ver, loader, source, search, "Downloads", category,
                overrideVersionFilter: "Todas", page: _currentPage, pageSize: 20);

            GrdModItems.ItemsSource = items;

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

        private async void CmbSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            _currentPage = 1;
            await RefreshCatalogAsync();
        }

        private async void CmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            _currentPage = 1;
            await RefreshCatalogAsync();
        }

        private async void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            _currentPage = 1;
            await RefreshCatalogAsync();
        }

        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            _currentPage = 1;
            await RefreshCatalogAsync();
        }

        private void GrdModItems_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MarketplaceItem item)
            {
                OpenItemDetails(item);
            }
        }

        private void BtnOpenDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarketplaceItem item)
            {
                OpenItemDetails(item);
            }
        }

        private void OpenItemDetails(MarketplaceItem item)
        {
            _mainWindow?.OpenModPluginDetails(item);
        }
    }
}
