using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public class ItemVersionViewModel
    {
        public string VersionName { get; set; } = string.Empty;
        public string ReleaseInfo { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string JarFileName { get; set; } = string.Empty;
    }

    public sealed partial class ModPluginDetailsPage : Page
    {
        private MainWindow? _mainWindow;
        private MarketplaceItem? _currentItem;
        private string _installedSource = string.Empty;

        public ModPluginDetailsPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is MarketplaceItem item)
            {
                LoadItemDetails(item);
            }
        }

        private void LoadItemDetails(MarketplaceItem item)
        {
            _currentItem = item;

            if (TxtItemTitle != null) TxtItemTitle.Text = item.Title;
            if (TxtItemAuthor != null) TxtItemAuthor.Text = $"Autor: {item.Author}";
            if (TxtItemDownloads != null) TxtItemDownloads.Text = $"Descargas: {item.FormattedDownloads}";
            if (TxtActiveSourceTag != null) TxtActiveSourceTag.Text = $"Fuente: {item.SourceName}";
            if (TxtDescription != null) TxtDescription.Text = !string.IsNullOrEmpty(item.Description) ? item.Description : item.Summary;

            if (TxtCategoryBadge != null)
            {
                TxtCategoryBadge.Text = item.Category == MarketplaceCategory.Plugin ? "PLUGIN" : "MOD";
            }

            if (ImgItemIcon != null && !string.IsNullOrEmpty(item.IconUrl))
            {
                try { ImgItemIcon.Source = new BitmapImage(new Uri(item.IconUrl)); } catch { }
            }

            PopulateSourcesFlyout(item);
            PopulateGallery(item);
            PopulateVersions(item);
        }

        private void PopulateSourcesFlyout(MarketplaceItem item)
        {
            if (FlyoutAvailableSources == null) return;
            FlyoutAvailableSources.Items.Clear();

            var sources = new List<string> { "Modrinth" };
            if (item.Category == MarketplaceCategory.Plugin)
            {
                sources.Add("Spiget");
                sources.Add("Bukkit");
            }
            else
            {
                sources.Add("CurseForge");
            }

            foreach (var src in sources)
            {
                var menuItem = new MenuFlyoutItem
                {
                    Text = $"Fuente: {src}",
                    Tag = src
                };
                menuItem.Click += SourceMenuItem_Click;
                FlyoutAvailableSources.Items.Add(menuItem);
            }
        }

        private async void SourceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string selectedSource && _currentItem != null && _mainWindow?.ActiveProfile != null)
            {
                if (selectedSource.Equals(_currentItem.SourceName, StringComparison.OrdinalIgnoreCase)) return;

                string previousSource = _currentItem.SourceName;

                // Check if installed from previous source
                string folderName = _currentItem.Category == MarketplaceCategory.Plugin ? "plugins" : "mods";
                string targetDir = Path.Combine(_mainWindow.ActiveProfile.DirectoryPath, folderName);
                string existingFile = Path.Combine(targetDir, _currentItem.JarFileName);

                if (File.Exists(existingFile))
                {
                    var confirmDialog = new ContentDialog
                    {
                        Title = "Cambiar Fuente e Instalación",
                        Content = $"El proyecto '{_currentItem.Title}' ya está instalado desde '{previousSource}'.\n¿Deseas eliminar la versión anterior e instalar desde la nueva fuente '{selectedSource}'?",
                        PrimaryButtonText = "Eliminar y Cambiar",
                        CloseButtonText = "Cancelar",
                        XamlRoot = this.XamlRoot
                    };

                    var result = await confirmDialog.ShowAsync();
                    if (result != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    try
                    {
                        File.Delete(existingFile);
                        _mainWindow.AppendConsoleLog($"[Zenith Marketplace] Eliminada versión anterior de {previousSource}: {existingFile}");
                    }
                    catch { }
                }

                _currentItem.SourceName = selectedSource;

                if (TxtActiveSourceTag != null) TxtActiveSourceTag.Text = $"Fuente: {selectedSource}";
                _mainWindow?.AppendConsoleLog($"[Zenith Marketplace] Fuente cambiada a '{selectedSource}' para '{_currentItem.Title}'.");

                // Download from new source
                await PerformInstallationAsync();
            }
        }

        private void PopulateGallery(MarketplaceItem item)
        {
            var screenshots = new List<string>(item.Screenshots);
            if (screenshots.Count == 0 && !string.IsNullOrEmpty(item.IconUrl))
            {
                screenshots.Add(item.IconUrl);
            }

            if (GrdGalleryThumbnails != null)
            {
                GrdGalleryThumbnails.ItemsSource = screenshots;
            }

            if (screenshots.Count > 0 && ImgMainPreview != null)
            {
                try { ImgMainPreview.Source = new BitmapImage(new Uri(screenshots[0])); } catch { }
            }
        }

        private void GrdGalleryThumbnails_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string url && ImgMainPreview != null)
            {
                try { ImgMainPreview.Source = new BitmapImage(new Uri(url)); } catch { }
            }
        }

        private void PopulateVersions(MarketplaceItem item)
        {
            if (LstVersions == null) return;

            var versionList = new List<ItemVersionViewModel>();
            if (item.CompatibleVersions.Count > 0)
            {
                foreach (var ver in item.CompatibleVersions)
                {
                    versionList.Add(new ItemVersionViewModel
                    {
                        VersionName = $"Versión {ver} ({item.SourceName})",
                        ReleaseInfo = $"Compatible con Minecraft {ver} | Loader: {item.SourceName}",
                        DownloadUrl = item.DownloadUrl,
                        JarFileName = item.JarFileName
                    });
                }
            }

            if (versionList.Count == 0)
            {
                versionList.Add(new ItemVersionViewModel
                {
                    VersionName = $"Versión Actual ({item.SourceName})",
                    ReleaseInfo = $"Compatible con la versión activa del servidor",
                    DownloadUrl = item.DownloadUrl,
                    JarFileName = item.JarFileName
                });
            }

            LstVersions.ItemsSource = versionList;
        }

        private void DetailsSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (PanelDescription == null || PanelGallery == null || PanelVersions == null) return;

            PanelDescription.Visibility = Visibility.Collapsed;
            PanelGallery.Visibility = Visibility.Collapsed;
            PanelVersions.Visibility = Visibility.Collapsed;

            var selectedItem = sender.SelectedItem;
            if (selectedItem == TabDescription)
            {
                PanelDescription.Visibility = Visibility.Visible;
            }
            else if (selectedItem == TabGallery)
            {
                PanelGallery.Visibility = Visibility.Visible;
            }
            else if (selectedItem == TabVersions)
            {
                PanelVersions.Visibility = Visibility.Visible;
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                _mainWindow?.NavigateToTag("Plugins");
            }
        }

        private async void SplitButtonSource_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            await PerformInstallationAsync();
        }

        private async Task PerformInstallationAsync()
        {
            if (_currentItem != null && _mainWindow?.ActiveProfile != null && SplitButtonSource != null)
            {
                SplitButtonSource.IsEnabled = false;
                SplitButtonSource.Content = "⏳ Instalando...";
                try
                {
                    string path = await _mainWindow.MarketplaceService.DownloadAndInstallItemAsync(_currentItem, _mainWindow.ActiveProfile.DirectoryPath);
                    _mainWindow.AppendConsoleLog($"[Zenith Marketplace] Instalado '{_currentItem.Title}' en {path}");
                    SplitButtonSource.Content = "¡Instalación Exitosa!";
                }
                catch (Exception ex)
                {
                    _mainWindow.AppendConsoleLog($"[Zenith Marketplace Error] {ex.Message}");
                    SplitButtonSource.Content = "Error al instalar";
                }
                finally
                {
                    await Task.Delay(1500);
                    SplitButtonSource.IsEnabled = true;
                    SplitButtonSource.Content = "🚀 Instalar en Servidor";
                }
            }
        }

        private async void BtnInstallVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ItemVersionViewModel verModel && _currentItem != null && _mainWindow?.ActiveProfile != null)
            {
                btn.IsEnabled = false;
                btn.Content = "⏳ Instalando...";
                try
                {
                    await _mainWindow.MarketplaceService.DownloadAndInstallItemAsync(_currentItem, _mainWindow.ActiveProfile.DirectoryPath);
                    btn.Content = "✓ Instalado";
                    _mainWindow.AppendConsoleLog($"[Zenith Marketplace] Instalada {verModel.VersionName} para '{_currentItem.Title}'.");
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
