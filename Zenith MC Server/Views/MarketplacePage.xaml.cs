using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class MarketplacePage : Page
    {
        private MainWindow? _mainWindow;
        private MarketplaceItem? _selectedItem;
        private int _currentPage = 1;

        public MarketplacePage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
            ConfigureDefaultFilters();
            _ = RefreshCatalogAsync();
        }

        private void ConfigureDefaultFilters()
        {
            if (_mainWindow == null || _mainWindow.ActiveProfile == null) return;
            var profile = _mainWindow.ActiveProfile;

            bool isPluginSupport = IsPluginMode(profile.SoftwareType);
            if (CmbCategoryType != null)
            {
                CmbCategoryType.SelectedIndex = isPluginSupport ? 0 : 1;
            }

            if (TxtActiveServerFilterBadge != null)
            {
                TxtActiveServerFilterBadge.Text = $"Servidor: {profile.SoftwareType} {profile.Version}";
            }
        }

        private bool IsPluginMode(string softwareType)
        {
            string lower = softwareType.ToLower();
            return !lower.Contains("fabric") && !lower.Contains("forge");
        }

        private async Task RefreshCatalogAsync()
        {
            if (_mainWindow == null || _mainWindow.ActiveProfile == null || GrdMarketplaceItems == null) return;

            bool isPluginCategory = CmbCategoryType != null && CmbCategoryType.SelectedIndex == 0;
            MarketplaceCategory cat = isPluginCategory ? MarketplaceCategory.Plugin : MarketplaceCategory.Mod;

            string ver = _mainWindow.ActiveProfile.Version;
            string loader = _mainWindow.ActiveProfile.SoftwareType;
            string source = (CmbSource?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Fuentes";
            string search = TxtSearch?.Text ?? "";

            var items = await _mainWindow.MarketplaceService.GetFeaturedItemsAsync(
                cat, ver, loader, source, search, "Downloads", "Todas las Categorías",
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

        private async void CmbCategoryType_SelectionChanged(object sender, SelectionChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }
        private async void CmbSource_SelectionChanged(object sender, SelectionChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }
        private async void CmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }
        private async void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }
        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) { _currentPage = 1; await RefreshCatalogAsync(); }

        private void GrdMarketplaceItems_ItemClick(object sender, ItemClickEventArgs e)
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
            _selectedItem = item;
            if (TxtDetailTitle != null) TxtDetailTitle.Text = item.Title;
            if (TxtDetailAuthor != null) TxtDetailAuthor.Text = $"Autor: {item.Author}";
            if (TxtDetailDownloads != null) TxtDetailDownloads.Text = $"Descargas: {item.FormattedDownloads}";
            if (TxtDetailSourceTag != null) TxtDetailSourceTag.Text = $"Fuente: {item.SourceName}";
            if (TxtDetailDescription != null) TxtDetailDescription.Text = !string.IsNullOrEmpty(item.Description) ? item.Description : item.Summary;

            if (ImgDetailIcon != null && !string.IsNullOrEmpty(item.IconUrl))
            {
                try { ImgDetailIcon.Source = new BitmapImage(new Uri(item.IconUrl)); } catch { }
            }

            if (ViewCatalogList != null) ViewCatalogList.Visibility = Visibility.Collapsed;
            if (ViewItemDetails != null) ViewItemDetails.Visibility = Visibility.Visible;
        }

        private void BtnBackToCatalog_Click(object sender, RoutedEventArgs e)
        {
            if (ViewItemDetails != null) ViewItemDetails.Visibility = Visibility.Collapsed;
            if (ViewCatalogList != null) ViewCatalogList.Visibility = Visibility.Visible;
        }

        private async void BtnDetailInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem != null && _mainWindow?.ActiveProfile != null && BtnDetailInstall != null)
            {
                BtnDetailInstall.IsEnabled = false;
                BtnDetailInstall.Content = "⏳ Instalando...";
                try
                {
                    string path = await _mainWindow.MarketplaceService.DownloadAndInstallItemAsync(_selectedItem, _mainWindow.ActiveProfile.DirectoryPath);
                    _mainWindow.AppendConsoleLog($"[Zenith Marketplace] Instalado '{_selectedItem.Title}' en {path}");
                    BtnDetailInstall.Content = "¡Instalación Exitosa!";
                }
                catch (Exception ex)
                {
                    _mainWindow.AppendConsoleLog($"[Zenith Marketplace Error] {ex.Message}");
                    BtnDetailInstall.Content = "Error al instalar";
                }
                finally
                {
                    await Task.Delay(1500);
                    BtnDetailInstall.IsEnabled = true;
                    BtnDetailInstall.Content = "🚀 Instalar en Servidor";
                }
            }
        }

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

        private async void BtnSwitchSourceModrinth_Click(object sender, RoutedEventArgs e)
        {
            if (CmbSource != null) CmbSource.SelectedIndex = 1;
            BtnBackToCatalog_Click(sender, e);
            await RefreshCatalogAsync();
        }

        private async void BtnSwitchSourceSpiget_Click(object sender, RoutedEventArgs e)
        {
            if (CmbSource != null) CmbSource.SelectedIndex = 2;
            BtnBackToCatalog_Click(sender, e);
            await RefreshCatalogAsync();
        }
    }
}
