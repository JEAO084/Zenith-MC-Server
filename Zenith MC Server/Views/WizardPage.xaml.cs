using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Zenith.Core.Models;
using Zenith.Core.Services;

namespace Zenith_MC_Server.Views
{
    public sealed partial class WizardPage : Page
    {
        private MainWindow? _mainWindow;
        private List<MinecraftVersionInfo> _wizardMojangVersions = [];

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

        private async void WzdChkSnapshots_CheckedChanged(object _sender, RoutedEventArgs _e)
        {
            await LoadWizardVersionsAsync();
        }

        private void WzdCmbVersion_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            if (_mainWindow == null || WzdCmbVersion == null) return;
            if (WzdCmbVersion.SelectedIndex >= 0 && WzdCmbVersion.SelectedIndex < _wizardMojangVersions.Count)
            {
                var ver = _wizardMojangVersions[WzdCmbVersion.SelectedIndex];
                int reqJava = _mainWindow.DownloadService.GetRecommendedJavaVersion(ver.Id);

                var wzdTxtJavaRequirement = FindName("WzdTxtJavaRequirement") as TextBlock;
                if (wzdTxtJavaRequirement != null)
                {
                    wzdTxtJavaRequirement.Text = $"Requisito Estricto: Java {reqJava} (Para Minecraft {ver.Id})";
                }

                var installedJava = _mainWindow.JavaService.GetAvailableJavaVersions().FirstOrDefault(j => j.MajorVersion == reqJava && File.Exists(j.InstalledPath));
                var wzdTxtJavaStatus = FindName("WzdTxtJavaStatus") as TextBlock;
                if (wzdTxtJavaStatus != null)
                {
                    wzdTxtJavaStatus.Text = installedJava != null ? $"Estado: Java {reqJava} detectado e instalado localmente." : $"Estado: Java {reqJava} no detectado. Se descargará automáticamente.";
                }

                var loaders = _mainWindow.DownloadService.GetCompatibleLoadersForVersion(ver.Id);
                if (WzdCmbLoader != null)
                {
                    WzdCmbLoader.ItemsSource = loaders;
                    if (loaders.Count > 0) WzdCmbLoader.SelectedIndex = 0;
                }
            }
        }

        private async void BtnImportExistingServer_Click(object _sender, RoutedEventArgs _e)
        {
            if (_mainWindow == null) return;

            try
            {
                var folderPicker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                folderPicker.FileTypeFilter.Add("*");

                IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, windowHandle);

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    string folderPath = folder.Path;
                    string serverName = $"📂 {folder.Name}";

                    var jarFiles = Directory.GetFiles(folderPath, "*.jar");
                    string jarName = jarFiles.Length > 0 ? Path.GetFileName(jarFiles[0]) : "server.jar";

                    var newProfile = _mainWindow.ServerManager.CreateProfile(serverName, "Paper", "1.20.4", 4);
                    newProfile.DirectoryPath = folderPath;
                    newProfile.JarFileName = jarName;

                    _mainWindow.ServerManager.UpdateProfile(newProfile);
                    _mainWindow.ServerManager.SelectProfile(newProfile.Id);
                    _mainWindow.RefreshServerProfilesUI();
                    _mainWindow.NavigateToTag("Dashboard");
                    _mainWindow.AppendConsoleLog($"[Zenith Import] Servidor '{folder.Name}' importado desde {folderPath}");
                }
            }
            catch (Exception ex)
            {
                _mainWindow.AppendConsoleLog($"[Zenith Import Notice] {ex.Message}");
            }
        }

        private async void BtnFinishWizard_Click(object _sender, RoutedEventArgs _e)
        {
            if (_mainWindow == null || BtnFinishWizard == null || WzdProgressBar == null) return;

            BtnFinishWizard.IsEnabled = false;
            BtnFinishWizard.Content = "⏳ Creando Servidor...";
            BtnFinishWizard.Visibility = Visibility.Collapsed;
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

                string loader = "Paper";
                if (WzdCmbLoader?.SelectedItem != null)
                {
                    loader = WzdCmbLoader.SelectedItem.ToString() ?? "Paper";
                }
                else if (WzdCmbLoader?.Items.Count > 0)
                {
                    loader = WzdCmbLoader.Items[0].ToString() ?? "Paper";
                }

                int ramGb = 4;
                if (WzdCmbRam?.SelectedItem is ComboBoxItem ramItem)
                {
                    _ = int.TryParse(ramItem.Content.ToString()?.Replace("GB", "").Trim(), out ramGb);
                }

                int port = 25565;
                if (WzdTxtPort != null)
                {
                    _ = int.TryParse(WzdTxtPort.Text.Trim(), out port);
                }

                string displayName = $"{icon} {name}";
                var newProfile = _mainWindow.ServerManager.CreateProfile(displayName, loader, version, ramGb);
                newProfile.SoftwareType = loader;

                // 1. Ensure internal Java JDK runtime
                int reqJava = _mainWindow.DownloadService.GetRecommendedJavaVersion(version);
                if (WzdTxtProgressStatus != null) WzdTxtProgressStatus.Text = $"Verificando / Descargando Java {reqJava} interno...";

                var javaOptions = _mainWindow.JavaService.GetAvailableJavaVersions();
                var targetOption = javaOptions.FirstOrDefault(j => j.MajorVersion == reqJava) ?? javaOptions.FirstOrDefault();

                if (targetOption != null)
                {
                    if (!File.Exists(targetOption.InstalledPath))
                    {
                        try
                        {
                            string installedExe = await _mainWindow.JavaService.DownloadAndExtractJavaAsync(targetOption, p =>
                            {
                                DispatcherQueue.TryEnqueue(() => { if (WzdProgressBar != null) WzdProgressBar.Value = p * 0.4; });
                            });
                            newProfile.JavaExecutablePath = installedExe;
                        }
                        catch (Exception ex)
                        {
                            _mainWindow.AppendConsoleLog($"[Zenith Java Warning] {ex.Message}");
                        }
                    }
                    else
                    {
                        newProfile.JavaExecutablePath = targetOption.InstalledPath;
                    }
                }

                // 2. Download server jar file for selected loader
                if (WzdTxtProgressStatus != null) WzdTxtProgressStatus.Text = $"Descargando motor {loader} {version}...";
                try
                {
                    string downloadedJarPath = await _mainWindow.DownloadService.DownloadServerJarForVersionAsync(loader, version, newProfile.DirectoryPath, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => { if (WzdProgressBar != null) WzdProgressBar.Value = 40 + (p * 0.6); });
                    });
                    newProfile.JarFileName = Path.GetFileName(downloadedJarPath);
                }
                catch (Exception ex)
                {
                    _mainWindow.AppendConsoleLog($"[Zenith Download Warning] {ex.Message}");
                }

                var toggleAikars = FindName("WzdToggleAikarsFlags") as FrameworkElement ?? WzdToggleAikarsFlags;
                bool isAikarsOn = false;
                if (toggleAikars is ToggleSwitch ts)
                {
                    isAikarsOn = ts.IsOn;
                }
                else if (toggleAikars is ToggleButton tb)
                {
                    isAikarsOn = tb.IsChecked == true;
                }

                if (isAikarsOn)
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
