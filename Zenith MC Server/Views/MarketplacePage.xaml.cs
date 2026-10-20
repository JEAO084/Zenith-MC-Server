using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class MarketplacePage : Page
    {
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

            var items = await app.MarketplaceService.SearchItemsAsync(query, category);
            ItemsListView.ItemsSource = items;
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            await PerformSearchAsync();
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearchAsync();
        }

        private async void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await PerformSearchAsync();
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarketplaceItem item && MainWindow.Current != null)
            {
                var server = MainWindow.Current.ProcessController.CurrentServer ??
                             (MainWindow.Current.ServerManager.Servers.Count > 0 ? MainWindow.Current.ServerManager.Servers[0] : null);

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
