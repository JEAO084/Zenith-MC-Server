using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class MarketplacePage : Page
    {
        private int _currentPage = 1;
        private int _totalPages = 1;

        public MarketplacePage()
        {
            InitializeComponent();
            Loaded += MarketplacePage_Loaded;
        }

        private async void MarketplacePage_Loaded(object sender, RoutedEventArgs e)
        {
            await PerformSearchAsync();
        }

        private async Task PerformSearchAsync()
        {
            var app = MainWindow.Current;
            if (app == null) return;

            string query = SearchBox.Text;
            MarketplaceCategory category = CategoryComboBox.SelectedIndex == 0 ? MarketplaceCategory.Plugin : MarketplaceCategory.Mod;

            string loader = "Todos";
            if (LoaderComboBox.SelectedItem is ComboBoxItem loaderItem)
            {
                loader = loaderItem.Content.ToString() ?? "Todos";
            }

            string gameVersion = "Todas";
            if (VersionComboBox.SelectedItem is ComboBoxItem verItem)
            {
                gameVersion = verItem.Content.ToString() ?? "Todas";
            }

            MarketplaceSortOption sortOption = SortComboBox.SelectedIndex switch
            {
                1 => MarketplaceSortOption.Relevance,
                2 => MarketplaceSortOption.Recent,
                _ => MarketplaceSortOption.Downloads
            };

            var filters = new MarketplaceFilterOptions
            {
                Query = query,
                Category = category,
                SelectedLoader = loader,
                SelectedGameVersion = gameVersion,
                SortBy = sortOption,
                Page = _currentPage,
                PageSize = 8
            };

            var searchResult = await app.MarketplaceService.SearchItemsAsync(filters);

            ItemsListView.ItemsSource = searchResult.Items;
            _totalPages = searchResult.TotalPages;
            _currentPage = searchResult.CurrentPage;

            PageStatusTextBlock.Text = $"Página {_currentPage} de {_totalPages} ({searchResult.TotalCount} resultados)";
            PrevPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < _totalPages;
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            _currentPage = 1;
            await PerformSearchAsync();
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            await PerformSearchAsync();
        }

        private async void FilterControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                _currentPage = 1;
                await PerformSearchAsync();
            }
        }

        private async void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await PerformSearchAsync();
            }
        }

        private async void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                await PerformSearchAsync();
            }
        }

        private void ItemsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MarketplaceItem item)
            {
                MainWindow.Current?.NavigateTo(typeof(MarketplaceDetailPage), item);
            }
        }

        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarketplaceItem item)
            {
                MainWindow.Current?.NavigateTo(typeof(MarketplaceDetailPage), item);
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarketplaceItem item && MainWindow.Current != null)
            {
                var server = MainWindow.Current.GetActiveServer();

                if (server != null)
                {
                    btn.IsEnabled = false;
                    btn.Content = "Instalando...";

                    bool result = await MainWindow.Current.MarketplaceService.InstallItemAsync(item, server.ServerPath);

                    btn.Content = result ? "Instalado ✓" : "Error";
                }
            }
        }
    }
}
