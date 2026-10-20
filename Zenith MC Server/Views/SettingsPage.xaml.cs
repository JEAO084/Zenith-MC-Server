using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var settings = app.SettingsService.Settings;
            ServersDirBox.Text = settings.DefaultServersDirectory;
            JavaDirBox.Text = settings.DefaultJavaRuntimesDirectory;
            CpuThreadsBox.Value = settings.MaxCpuThreads;
            RamAlertBox.Value = settings.RamAlertThresholdMb;
            AutoBackupToggle.IsOn = settings.AutoBackupBeforeUpdate;

            RefreshBackups();
        }

        private void RefreshBackups()
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var server = app.ProcessController.CurrentServer ?? (app.ServerManager.Servers.Count > 0 ? app.ServerManager.Servers[0] : null);
            if (server != null)
            {
                var backups = app.BackupService.GetBackupsForServer(server.Id);
                BackupsListView.ItemsSource = backups;
            }
        }

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var server = app.ProcessController.CurrentServer ?? (app.ServerManager.Servers.Count > 0 ? app.ServerManager.Servers[0] : null);
            if (server != null)
            {
                await app.BackupService.CreateBackupAsync(server, "Respaldo Manual desde Ajustes");
                RefreshBackups();
            }
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ServerBackup backup && MainWindow.Current != null)
            {
                var server = MainWindow.Current.ProcessController.CurrentServer ?? (MainWindow.Current.ServerManager.Servers.Count > 0 ? MainWindow.Current.ServerManager.Servers[0] : null);
                if (server != null)
                {
                    await MainWindow.Current.BackupService.RestoreBackupAsync(backup.FilePath, server.ServerPath);
                }
            }
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var updateInfo = await app.UpdateService.CheckForUpdatesAsync();
            UpdateStatusTextBlock.Text = updateInfo.UpdateAvailable
                ? $"Nueva versión disponible: {updateInfo.LatestVersion}"
                : $"Versión Actual: {updateInfo.CurrentVersion} (Sistema al día)";
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var settings = app.SettingsService.Settings;
            settings.DefaultServersDirectory = ServersDirBox.Text;
            settings.DefaultJavaRuntimesDirectory = JavaDirBox.Text;
            settings.MaxCpuThreads = (int)CpuThreadsBox.Value;
            settings.RamAlertThresholdMb = (int)RamAlertBox.Value;
            settings.AutoBackupBeforeUpdate = AutoBackupToggle.IsOn;

            app.SettingsService.SaveSettings(settings);
        }
    }
}
