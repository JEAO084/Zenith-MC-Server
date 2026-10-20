using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class WizardPage : Page
    {
        private MainWindow? _mainWindow;
        private List<MinecraftVersionInfo> _wizardMojangVersions = new();

        public WizardPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
            _ = LoadWizardVersionsAsync();
        }

        private async Task LoadWizardVersionsAsync()
        {
            if (_mainWindow == null) return;
            bool showSnapshots = WzdChkSnapshots.IsChecked == true;
            _wizardMojangVersions = await _mainWindow.DownloadService.GetMojangVersionsAsync(showSnapshots);

            DispatcherQueue.TryEnqueue(() =>
            {
                if (WzdCmbVersion != null)
                {
                    WzdCmbVersion.ItemsSource = _wizardMojangVersions.Select(v => $"{v.Id} [{v.Type.ToUpper()}]").ToList();
                    if (WzdCmbVersion.Items.Count > 0)
                    {
                        WzdCmbVersion.SelectedIndex = 0;
                    }
                }
            });
        }

        private async void WzdChkSnapshots_CheckedChanged(object sender, RoutedEventArgs e)
        {
            await LoadWizardVersionsAsync();
        }

        private void WzdCmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_mainWindow == null || WzdCmbVersion == null) return;
            if (WzdCmbVersion.SelectedIndex >= 0 && WzdCmbVersion.SelectedIndex < _wizardMojangVersions.Count)
            {
                var ver = _wizardMojangVersions[WzdCmbVersion.SelectedIndex];
                int reqJava = _mainWindow.DownloadService.GetRecommendedJavaVersion(ver.Id);

                if (WzdTxtJavaRequirement != null)
                {
                    WzdTxtJavaRequirement.Text = $"Requisito Estricto: Java {reqJava} (Para Minecraft {ver.Id})";
                }

                var installedJava = _mainWindow.JavaService.GetAvailableJavaVersions().FirstOrDefault(j => j.MajorVersion == reqJava && File.Exists(j.InstalledPath));
                if (WzdTxtJavaStatus != null)
                {
                    WzdTxtJavaStatus.Text = installedJava != null ? $"Estado: Java {reqJava} detectado e instalado localmente." : $"Estado: Java {reqJava} no detectado. Se descargará automáticamente.";
                }

                var loaders = _mainWindow.DownloadService.GetCompatibleLoadersForVersion(ver.Id);
                if (WzdCmbLoader != null)
                {
                    WzdCmbLoader.ItemsSource = loaders;
                    if (loaders.Count > 0) WzdCmbLoader.SelectedIndex = 0;
                }
            }
        }

        private async void BtnFinishWizard_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null || BtnFinishWizard == null || WzdProgressBar == null) return;

            BtnFinishWizard.IsEnabled = false;
            BtnFinishWizard.Content = "⏳ Creando Servidor...";
            WzdProgressBar.Visibility = Visibility.Visible;

            try
            {
                string name = WzdServerName?.Text.Trim() ?? "Nuevo Servidor";
                if (string.IsNullOrWhiteSpace(name)) name = "Nuevo Servidor";

                string icon = "⛏️";
                if (WzdServerIcon?.SelectedItem is ComboBoxItem iconItem)
                {
                    icon = iconItem.Content.ToString()?.Split(' ')[0] ?? "⛏️";
                }

                string version = "1.20.4";
                if (WzdCmbVersion != null && WzdCmbVersion.SelectedIndex >= 0 && WzdCmbVersion.SelectedIndex < _wizardMojangVersions.Count)
                {
                    version = _wizardMojangVersions[WzdCmbVersion.SelectedIndex].Id;
                }

                string loader = WzdCmbLoader?.SelectedItem?.ToString() ?? "Paper";
                int ramGb = 4;
                if (WzdCmbRam?.SelectedItem is ComboBoxItem ramItem)
                {
                    int.TryParse(ramItem.Content.ToString()?.Replace("GB", "").Trim(), out ramGb);
                }

                int port = 25565;
                if (WzdTxtPort != null)
                {
                    int.TryParse(WzdTxtPort.Text.Trim(), out port);
                }

                string displayName = $"{icon} {name}";
                var newProfile = _mainWindow.ServerManager.CreateProfile(displayName, loader, version, ramGb);

                if (WzdTxtProgressStatus != null) WzdTxtProgressStatus.Text = $"Obteniendo {loader} {version}...";
                try
                {
                    string downloadedJarPath = await _mainWindow.DownloadService.DownloadServerJarForVersionAsync(loader, version, newProfile.DirectoryPath, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => { if (WzdProgressBar != null) WzdProgressBar.Value = p; });
                    });
                    newProfile.JarFileName = Path.GetFileName(downloadedJarPath);
                }
                catch { }

                if (WzdToggleAikarsFlags != null && WzdToggleAikarsFlags.IsOn)
                {
                    newProfile.CustomJvmArgs = _mainWindow.ConfigService.GetAikarsJvmFlags(ramGb);
                }

                _mainWindow.ServerManager.UpdateProfile(newProfile);
                _mainWindow.ServerManager.SelectProfile(newProfile.Id);
                _mainWindow.RefreshServerProfilesUI();
                _mainWindow.NavigateToTag("Dashboard");
            }
            finally
            {
                BtnFinishWizard.IsEnabled = true;
                BtnFinishWizard.Content = "🚀 Finalizar y Crear Servidor";
                WzdProgressBar.Visibility = Visibility.Collapsed;
            }
        }
    }
}
