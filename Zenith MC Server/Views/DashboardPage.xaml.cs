using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
            Loaded += DashboardPage_Loaded;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshServers();
        }

        private void RefreshServers()
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var servers = app.ServerManager.Servers.ToList();
            ServersListView.ItemsSource = servers;
            EmptyStatePanel.Visibility = servers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            app.RefreshActiveServerComboBox();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ServerInstance server && MainWindow.Current != null)
            {
                var javaRuntime = await MainWindow.Current.JavaService.EnsureJavaRuntimeAsync(
                    MainWindow.Current.JavaService.DetermineRequiredJavaVersion(server.Version));

                await MainWindow.Current.ProcessController.StartServerAsync(server, javaRuntime.ExecutablePath);
                RefreshServers();
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current != null)
            {
                await MainWindow.Current.ProcessController.StopServerAsync();
                RefreshServers();
            }
        }

        private void ConsoleButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.NavigateTo(typeof(ServerConsolePage));
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ServerInstance server && MainWindow.Current != null)
            {
                MainWindow.Current.ServerManager.DeleteServer(server.Id, deleteFiles: true);
                RefreshServers();
            }
        }

        private void CreateServer_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Current?.NavigateTo(typeof(ServerWizardPage));
        }
    }
}
