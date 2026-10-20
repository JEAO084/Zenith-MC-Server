using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class ServerWizardPage : Page
    {
        public ServerWizardPage()
        {
            InitializeComponent();
            Loaded += ServerWizardPage_Loaded;
        }

        private void ServerWizardPage_Loaded(object sender, RoutedEventArgs e)
        {
            EngineComboBox.ItemsSource = Enum.GetValues(typeof(ServerEngineType));
            EngineComboBox.SelectedIndex = 0;
        }

        private async void EngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EngineComboBox.SelectedItem is ServerEngineType engineType && MainWindow.Current != null)
            {
                var versions = await MainWindow.Current.EngineService.GetDynamicVersionsAsync(engineType);
                VersionComboBox.ItemsSource = versions;
                if (versions.Count > 0)
                {
                    VersionComboBox.SelectedIndex = 0;
                }
            }
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current == null) return;

            CreateButton.IsEnabled = false;
            CreateButton.Content = "Descargando Motor...";

            string name = string.IsNullOrWhiteSpace(ServerNameBox.Text) ? "Minecraft Server" : ServerNameBox.Text;
            ServerEngineType engine = EngineComboBox.SelectedItem is ServerEngineType t ? t : ServerEngineType.Paper;
            string version = VersionComboBox.SelectedItem is string v ? v : "1.20.4";
            int minRam = (int)MinRamBox.Value;
            int maxRam = (int)MaxRamBox.Value;
            int port = (int)PortBox.Value;

            var server = await MainWindow.Current.ServerManager.CreateServerAsync(name, engine, version, minRam, maxRam, port);

            // Download official engine JAR file dynamically
            await MainWindow.Current.EngineService.DownloadEngineJarAsync(engine, version, server.ExecutableJarPath);

            MainWindow.Current.NavigateTo(typeof(DashboardPage));
        }
    }
}
