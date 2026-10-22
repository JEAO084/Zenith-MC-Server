using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            var chkSnapshots = FindName("WzdChkSnapshots") as CheckBox;
            bool showSnapshots = chkSnapshots?.IsChecked == true;
            _wizardMojangVersions = await _mainWindow.DownloadService.GetMojangVersionsAsync(showSnapshots);

            DispatcherQueue.TryEnqueue(() =>
            {
                var cmbVersion = FindName("WzdCmbVersion") as ComboBox;
                if (cmbVersion != null)
                {
                    cmbVersion.ItemsSource = _wizardMojangVersions.Select(v => $"{v.Id} [{v.Type.ToUpper()}]").ToList();
                    if (cmbVersion.Items.Count > 0)
                    {
                        cmbVersion.SelectedIndex = 0;
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
            if (_mainWindow == null) return;
            var cmbVersion = FindName("WzdCmbVersion") as ComboBox;
            if (cmbVersion == null) return;

            if (cmbVersion.SelectedIndex >= 0 && cmbVersion.SelectedIndex < _wizardMojangVersions.Count)
            {
                var ver = _wizardMojangVersions[cmbVersion.SelectedIndex];
                int reqJava = _mainWindow.DownloadService.GetRecommendedJavaVersion(ver.Id);

                var txtJavaReq = FindName("WzdTxtJavaRequirement") as TextBlock;
                if (txtJavaReq != null)
                {
                    txtJavaReq.Text = $"Requisito Estricto: Java {reqJava} (Para Minecraft {ver.Id})";
                }

                var installedJava = _mainWindow.JavaService.GetAvailableJavaVersions().FirstOrDefault(j => j.MajorVersion == reqJava && File.Exists(j.InstalledPath));
                var txtJavaStatus = FindName("WzdTxtJavaStatus") as TextBlock;
                if (txtJavaStatus != null)
                {
                    txtJavaStatus.Text = installedJava != null ? $"Estado: Java {reqJava} detectado e instalado localmente." : $"Estado: Java {reqJava} no detectado. Se descargará automáticamente.";
                }

                var loaders = _mainWindow.DownloadService.GetCompatibleLoadersForVersion(ver.Id);
                var cmbLoader = FindName("WzdCmbLoader") as ComboBox;
                if (cmbLoader != null)
                {
                    cmbLoader.ItemsSource = loaders;
                    if (loaders.Count > 0) cmbLoader.SelectedIndex = 0;
                }
            }
        }

        private async void BtnImportExistingServer_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;

            try
            {
                var folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
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

        private async void BtnFinishWizard_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;

            var btnFinish = FindName("BtnFinishWizard") as Button;
            var progressBar = FindName("WzdProgressBar") as ProgressBar;
            var txtProgressStatus = FindName("WzdTxtProgressStatus") as TextBlock;

            if (btnFinish != null)
            {
                btnFinish.IsEnabled = false;
                btnFinish.Content = "⏳ Creando Servidor...";
                btnFinish.Visibility = Visibility.Collapsed;
            }
            if (progressBar != null)
            {
                progressBar.Visibility = Visibility.Visible;
            }

            try
            {
                var txtName = FindName("WzdServerName") as TextBox;
                string name = txtName?.Text.Trim() ?? "Nuevo Servidor";
                if (string.IsNullOrWhiteSpace(name)) name = "Nuevo Servidor";

                string icon = "⛏️";
                var cmbIcon = FindName("WzdServerIcon") as ComboBox;
                if (cmbIcon?.SelectedItem is ComboBoxItem iconItem)
                {
                    icon = iconItem.Content.ToString()?.Split(' ')[0] ?? "⛏️";
                }

                string version = "1.20.4";
                var cmbVersion = FindName("WzdCmbVersion") as ComboBox;
                if (cmbVersion != null && cmbVersion.SelectedIndex >= 0 && cmbVersion.SelectedIndex < _wizardMojangVersions.Count)
                {
                    version = _wizardMojangVersions[cmbVersion.SelectedIndex].Id;
                }

                string loader = "Paper";
                var cmbLoader = FindName("WzdCmbLoader") as ComboBox;
                if (cmbLoader?.SelectedItem != null)
                {
                    loader = cmbLoader.SelectedItem.ToString() ?? "Paper";
                }
                else if (cmbLoader?.Items.Count > 0)
                {
                    loader = cmbLoader.Items[0].ToString() ?? "Paper";
                }

                int ramGb = 4;
                var cmbRam = FindName("WzdCmbRam") as ComboBox;
                if (cmbRam?.SelectedItem is ComboBoxItem ramItem)
                {
                    int.TryParse(ramItem.Content.ToString()?.Replace("GB", "").Trim(), out ramGb);
                }

                int port = 25565;
                var txtPort = FindName("WzdTxtPort") as TextBox;
                if (txtPort != null)
                {
                    int.TryParse(txtPort.Text.Trim(), out port);
                }

                string displayName = $"{icon} {name}";
                var newProfile = _mainWindow.ServerManager.CreateProfile(displayName, loader, version, ramGb);
                newProfile.SoftwareType = loader;

                // 1. Ensure internal Java JDK runtime
                int reqJava = _mainWindow.DownloadService.GetRecommendedJavaVersion(version);
                if (txtProgressStatus != null) txtProgressStatus.Text = $"Verificando / Descargando Java {reqJava} interno...";

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
                                DispatcherQueue.TryEnqueue(() => { if (progressBar != null) progressBar.Value = p * 0.4; });
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
                if (txtProgressStatus != null) txtProgressStatus.Text = $"Descargando motor {loader} {version}...";
                try
                {
                    string downloadedJarPath = await _mainWindow.DownloadService.DownloadServerJarForVersionAsync(loader, version, newProfile.DirectoryPath, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => { if (progressBar != null) progressBar.Value = 40 + (p * 0.6); });
                    });
                    newProfile.JarFileName = Path.GetFileName(downloadedJarPath);
                }
                catch (Exception ex)
                {
                    _mainWindow.AppendConsoleLog($"[Zenith Download Warning] {ex.Message}");
                }

                var toggleAikars = FindName("WzdToggleAikarsFlags");
                bool aikarsOn = (toggleAikars as ToggleSwitch)?.IsOn == true ||
                                (toggleAikars as Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)?.IsChecked == true;

                if (aikarsOn)
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
                if (btnFinish != null)
                {
                    btnFinish.IsEnabled = true;
                    btnFinish.Content = "🚀 Finalizar y Crear Servidor";
                    btnFinish.Visibility = Visibility.Visible;
                }
                if (progressBar != null)
                {
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
