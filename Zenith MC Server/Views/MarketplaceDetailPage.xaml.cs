using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class MarketplaceDetailPage : Page
    {
        private MarketplaceItem? _currentItem;

        public MarketplaceDetailPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is MarketplaceItem item)
            {
                _currentItem = item;
                PopulateDetails();
            }
        }

        private void PopulateDetails()
        {
            if (_currentItem == null) return;

            TitleTextBlock.Text = _currentItem.Title;
            CategoryTextBlock.Text = _currentItem.Category.ToString();
            SummaryTextBlock.Text = _currentItem.Summary;
            AuthorTextBlock.Text = $"Autor: {_currentItem.Author}";
            DownloadsTextBlock.Text = $"Descargas: {_currentItem.Downloads:N0}";
            VersionTextBlock.Text = $"Versión: {_currentItem.LatestVersion}";

            if (!string.IsNullOrWhiteSpace(_currentItem.IconUrl))
            {
                try
                {
                    ItemIconImage.Source = new BitmapImage(new Uri(_currentItem.IconUrl));
                }
                catch { }
            }

            LoadersTextBlock.Text = _currentItem.SupportedLoaders.Count > 0
                ? string.Join(", ", _currentItem.SupportedLoaders)
                : "Todos los Loaders estándar";

            GameVersionsTextBlock.Text = _currentItem.SupportedGameVersions.Count > 0
                ? string.Join(", ", _currentItem.SupportedGameVersions)
                : "1.20.4, 1.20.1, 1.19.4, 1.18.2";

            DetailedDescriptionTextBlock.Text = string.IsNullOrWhiteSpace(_currentItem.DetailedDescription)
                ? _currentItem.Summary
                : _currentItem.DetailedDescription;

            if (_currentItem.GalleryImageUrls != null && _currentItem.GalleryImageUrls.Any())
            {
                GalleryBorder.Visibility = Visibility.Visible;
                GalleryGridView.ItemsSource = _currentItem.GalleryImageUrls;
            }
            else
            {
                GalleryBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                MainWindow.Current?.NavigateTo(typeof(MarketplacePage));
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentItem != null && MainWindow.Current != null)
            {
                var server = MainWindow.Current.GetActiveServer();

                if (server != null)
                {
                    InstallButton.IsEnabled = false;
                    InstallButton.Content = "Instalando .jar...";

                    bool result = await MainWindow.Current.MarketplaceService.InstallItemAsync(_currentItem, server.ServerPath);

                    InstallButton.Content = result ? "Instalado con Éxito ✓" : "Error en Instalación";
                }
            }
        }
    }
}
