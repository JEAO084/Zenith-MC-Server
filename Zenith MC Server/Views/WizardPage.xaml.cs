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
        private List<JavaVersionOption> _availableJavaOptions = [];

        public WizardPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
            _ = LoadWizardDataAsync();
        }

        private async Task LoadWizardDataAsync()
        {
            if (_mainWindow == null) return;
            bool showSnapshots = WzdChkSnapshots?.IsChecked == true;
            _wizardMojangVersions = await _mainWindow.DownloadService.GetMojangVersionsAsync(showSnapshots);

            _availableJavaOptions = _mainWindow.JavaService.GetAvailableJavaVersions();

            DispatcherQueue.TryEnqueue(() =>
            {
                if (WzdCmbJavaVersion != null)
                {
                    WzdCmbJavaVersion.ItemsSource = _availableJavaOptions.Select(j => $"{j.Name} {(j.IsInstalled ? "[Instalado]" : "[Descargar]")}").ToList();
                }

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
            await LoadWizardDataAsync();
        }

        private async void WzdCmbVersion_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            if (_mainWindow == null || WzdCmbVersion == null) return;
            if (WzdCmbVersion.SelectedIndex >= 0 && WzdCmbVersion.SelectedIndex < _wizardMojangVersions.Count)
            {
                var ver = _wizardMojangVersions[WzdCmbVersion.SelectedIndex];
                int reqJava = _mainWindow.DownloadService.GetRecommendedJavaVersion(ver.Id);

                var wzdTxtJavaRequirement = FindName("WzdTxtJavaRequirement") as TextBlock ?? WzdTxtJavaRequirement;
                if (wzdTxtJavaRequirement != null)
                {
                    wzdTxtJavaRequirement.Text = $"Requisito Estricto: Java {reqJava} (Para Minecraft {ver.Id})";
                }

                var installedJava = _mainWindow.JavaService.GetAvailableJavaVersions().FirstOrDefault(j => j.MajorVersion == reqJava && File.Exists(j.InstalledPath));
                var wzdTxtJavaStatus = FindName("WzdTxtJavaStatus") as TextBlock ?? WzdTxtJavaStatus;
                if (wzdTxtJavaStatus != null)
                {
                    wzdTxtJavaStatus.Text = installedJava != null ? $"Estado: Java {reqJava} detectado e instalado localmente." : $"Estado: Java {reqJava} no detectado. Se descargará automáticamente.";
                }

                // Auto-select corresponding Java version in ComboBox
                if (WzdCmbJavaVersion != null && _availableJavaOptions.Count > 0)
                {
                    int matchIdx = _availableJavaOptions.FindIndex(j => j.MajorVersion == reqJava);
                    if (matchIdx >= 0)
                    {
                        WzdCmbJavaVersion.SelectedIndex = matchIdx;
                    }
                    else
                    {
                        WzdCmbJavaVersion.SelectedIndex = 0;
                    }
                }

                var loaders = _mainWindow.DownloadService.GetCompatibleLoadersForVersion(ver.Id);
                if (WzdCmbLoader != null)
                {
                    WzdCmbLoader.ItemsSource = loaders;
                    if (loaders.Count > 0) WzdCmbLoader.SelectedIndex = 0;
                }

                await UpdateLoaderVersionsAsync();
            }
        }

        private async void WzdCmbLoader_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            await UpdateLoaderVersionsAsync();
        }

        private void WzdCmbJavaVersion_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
        {
            if (WzdCmbJavaVersion == null || WzdCmbJavaVersion.SelectedIndex < 0 || WzdCmbJavaVersion.SelectedIndex >= _availableJavaOptions.Count) return;

            var selectedJava = _availableJavaOptions[WzdCmbJavaVersion.SelectedIndex];
            var wzdTxtJavaStatus = FindName("WzdTxtJavaStatus") as TextBlock ?? WzdTxtJavaStatus;
            if (wzdTxtJavaStatus != null)
            {
                wzdTxtJavaStatus.Text = selectedJava.IsInstalled ? $"Estado: {selectedJava.Name} instalado." : $"Estado: {selectedJava.Name} no instalado. Se descargará automáticamente.";
            }
        }

        private async Task UpdateLoaderVersionsAsync()
        {
            if (_mainWindow == null || WzdCmbVersion == null || WzdCmbLoader == null || WzdCmbLoaderVersion == null) return;

            string selectedMcVer = "1.20.4";
            if (WzdCmbVersion.SelectedIndex >= 0 && WzdCmbVersion.SelectedIndex < _wizardMojangVersions.Count)
            {
                selectedMcVer = _wizardMojangVersions[WzdCmbVersion.SelectedIndex].Id;
            }

            string selectedLoader = "Paper";
            if (WzdCmbLoader.SelectedItem != null)
            {
                if (WzdCmbLoader.SelectedItem is ComboBoxItem item)
                    selectedLoader = item.Content?.ToString()?.Split(' ')[0] ?? "Paper";
                else
                    selectedLoader = WzdCmbLoader.SelectedItem.ToString()?.Split(' ')[0] ?? "Paper";
            }

            var loaderVersions = await _mainWindow.DownloadService.GetLoaderVersionsAsync(selectedLoader, selectedMcVer);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (WzdCmbLoaderVersion != null)
                {
                    WzdCmbLoaderVersion.ItemsSource = loaderVersions;
                    if (loaderVersions.Count > 0)
                    {
                        WzdCmbLoaderVersion.SelectedIndex = 0;
                    }
                }
            });
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
                    if (WzdCmbLoader.SelectedItem is ComboBoxItem loaderItem)
                        loader = loaderItem.Content?.ToString()?.Split(' ')[0] ?? "Paper";
                    else
                        loader = WzdCmbLoader.SelectedItem.ToString()?.Split(' ')[0] ?? "Paper";
                }

                string loaderVer = "Latest";
                if (WzdCmbLoaderVersion?.SelectedItem != null)
                {
                    loaderVer = WzdCmbLoaderVersion.SelectedItem.ToString() ?? "Latest";
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

                // 1. Ensure Java JDK runtime from user selection or recommended default
                JavaVersionOption? targetOption = null;
                if (WzdCmbJavaVersion != null && WzdCmbJavaVersion.SelectedIndex >= 0 && WzdCmbJavaVersion.SelectedIndex < _availableJavaOptions.Count)
                {
                    targetOption = _availableJavaOptions[WzdCmbJavaVersion.SelectedIndex];
                }

                if (targetOption == null)
                {
                    int reqJava = _mainWindow.DownloadService.GetRecommendedJavaVersion(version);
                    targetOption = _availableJavaOptions.FirstOrDefault(j => j.MajorVersion == reqJava) ?? _availableJavaOptions.FirstOrDefault();
                }

                if (targetOption != null)
                {
                    var wzdTxtProgressStatus = FindName("WzdTxtProgressStatus") as TextBlock ?? WzdTxtProgressStatus;
                    if (wzdTxtProgressStatus != null) wzdTxtProgressStatus.Text = $"Verificando / Descargando {targetOption.Name}...";

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

                // 2. Download server jar file for selected loader and loader version
                var progressStatus = FindName("WzdTxtProgressStatus") as TextBlock ?? WzdTxtProgressStatus;
                if (progressStatus != null) progressStatus.Text = $"Descargando motor {loader} ({loaderVer}) para Minecraft {version}...";
                try
                {
                    string downloadedJarPath = await _mainWindow.DownloadService.DownloadServerJarForVersionAsync(loader, version, newProfile.DirectoryPath, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => { if (WzdProgressBar != null) WzdProgressBar.Value = 40 + (p * 0.6); });
                    }, loaderVer);
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
