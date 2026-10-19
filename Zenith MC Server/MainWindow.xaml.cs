using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Zenith.Core.Models;
using Zenith.Core.Services;

namespace Zenith_MC_Server
{
    public sealed partial class MainWindow : Window
    {
        private readonly ServerManagerService _serverManager;
        private readonly JavaInstallerService _javaService;
        private readonly ServerDownloadService _downloadService;
        private readonly ServerConfigService _configService;
        private readonly FileManagerService _fileManager;
        private readonly ServerProcessManager _processManager;

        private ServerPropertiesConfig _currentProperties;
        private ServerFileItem? _selectedFileItem;
        private List<MinecraftVersionInfo> _allMojangVersions = new();
        private List<MinecraftVersionInfo> _wizardMojangVersions = new();

        public MainWindow()
        {
            _serverManager = new ServerManagerService();
            _javaService = new JavaInstallerService();
            _downloadService = new ServerDownloadService();
            _configService = new ServerConfigService();
            _fileManager = new FileManagerService();
            _processManager = new ServerProcessManager();
            _currentProperties = new ServerPropertiesConfig();

            this.InitializeComponent();

            // Custom Title Bar Integration
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            _processManager.OutputReceived += OnServerOutputReceived;
            _processManager.ProcessExited += OnServerProcessExited;

            RefreshServerProfilesUI();
            LoadActiveServerUI();
            UpdateStatusIndicator(false);

            // Fetch Mojang versions in background
            _ = LoadMojangVersionsAsync();
            _ = LoadWizardVersionsAsync();
        }

        #region Multi-Server Manager & Wizard

        private void RefreshServerProfilesUI()
        {
            if (_serverManager == null) return;
            var profiles = _serverManager.GetProfiles();
            CmbServerSelector.ItemsSource = profiles;
            if (_serverManager.ActiveProfile != null)
            {
                CmbServerSelector.SelectedItem = _serverManager.ActiveProfile;
            }
        }

        private void CmbServerSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_serverManager != null && CmbServerSelector.SelectedItem is ServerProfile profile)
            {
                _serverManager.SelectProfile(profile.Id);
                LoadActiveServerUI();
            }
        }

        private void BtnOpenWizard_Click(object sender, RoutedEventArgs e)
        {
            SwitchToView("Wizard");
        }

        private async Task LoadWizardVersionsAsync()
        {
            if (_downloadService == null) return;
            bool showSnapshots = WzdChkSnapshots.IsChecked == true;
            _wizardMojangVersions = await _downloadService.GetMojangVersionsAsync(showSnapshots);

            DispatcherQueue.TryEnqueue(() =>
            {
                WzdCmbVersion.ItemsSource = _wizardMojangVersions.Select(v => $"{v.Id} [{v.Type.ToUpper()}]").ToList();
                if (WzdCmbVersion.Items.Count > 0)
                {
                    WzdCmbVersion.SelectedIndex = 0;
                }
            });
        }

        private async void WzdChkSnapshots_CheckedChanged(object sender, RoutedEventArgs e)
        {
            await LoadWizardVersionsAsync();
        }

        private void WzdCmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WzdCmbVersion.SelectedIndex >= 0 && WzdCmbVersion.SelectedIndex < _wizardMojangVersions.Count)
            {
                var ver = _wizardMojangVersions[WzdCmbVersion.SelectedIndex];
                int reqJava = _downloadService.GetRecommendedJavaVersion(ver.Id);

                WzdTxtJavaRequirement.Text = $"Requisito Estricto: Java {reqJava} (Para Minecraft {ver.Id})";

                var installedJava = _javaService?.GetAvailableJavaVersions().FirstOrDefault(j => j.MajorVersion == reqJava && File.Exists(j.InstalledPath));
                if (installedJava != null)
                {
                    WzdTxtJavaStatus.Text = $"Estado: Java {reqJava} detectado e instalado localmente.";
                }
                else
                {
                    WzdTxtJavaStatus.Text = $"Estado: Java {reqJava} no detectado. Se descargará e instalará automáticamente.";
                }

                // Update loader compatibility
                var loaders = _downloadService.GetCompatibleLoadersForVersion(ver.Id);
                WzdCmbLoader.ItemsSource = loaders;
                if (loaders.Count > 0) WzdCmbLoader.SelectedIndex = 0;
            }
        }

        private async void BtnFinishWizard_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _downloadService == null) return;

            string name = WzdServerName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) name = "Nuevo Servidor";

            string icon = "⛏️";
            if (WzdServerIcon.SelectedItem is ComboBoxItem iconItem)
            {
                icon = iconItem.Content.ToString() ?? "⛏️";
            }

            string version = "1.20.4";
            if (WzdCmbVersion.SelectedIndex >= 0 && WzdCmbVersion.SelectedIndex < _wizardMojangVersions.Count)
            {
                version = _wizardMojangVersions[WzdCmbVersion.SelectedIndex].Id;
            }

            string loader = "Paper";
            if (WzdCmbLoader.SelectedItem != null)
            {
                loader = WzdCmbLoader.SelectedItem.ToString() ?? "Paper";
            }

            int ramGb = 4;
            if (WzdCmbRam.SelectedItem is ComboBoxItem ramItem)
            {
                int.TryParse(ramItem.Content.ToString()?.Replace("GB", "").Trim(), out ramGb);
            }

            int port = 25565;
            int.TryParse(WzdTxtPort.Text.Trim(), out port);

            // 1. Create Profile
            var newProfile = _serverManager.CreateProfile($"{icon} {name}", loader, version, ramGb);

            // 2. Set Aikar's Flags
            if (WzdToggleAikarsFlags.IsOn)
            {
                newProfile.CustomJvmArgs = _configService.GetAikarsJvmFlags(ramGb);
            }

            // 3. Set Java Runtime
            int reqJava = _downloadService.GetRecommendedJavaVersion(version);
            var javaVerOption = _javaService.GetAvailableJavaVersions().FirstOrDefault(j => j.MajorVersion == reqJava);
            if (javaVerOption != null)
            {
                if (File.Exists(javaVerOption.InstalledPath))
                {
                    newProfile.JavaExecutablePath = javaVerOption.InstalledPath;
                }
                else
                {
                    try
                    {
                        string downloadedJavaPath = await _javaService.DownloadAndExtractJavaAsync(javaVerOption);
                        newProfile.JavaExecutablePath = downloadedJavaPath;
                    }
                    catch { }
                }
            }

            // 4. Set EULA & Port
            _configService.SetEulaAccepted(newProfile.DirectoryPath, WzdToggleEula.IsOn);
            var props = _configService.LoadProperties(newProfile.DirectoryPath);
            props.SetProperty("server-port", port.ToString());
            props.SetProperty("motd", $"{name} - Servidor Minecraft");
            _configService.SaveProperties(newProfile.DirectoryPath, props);

            _serverManager.UpdateProfile(newProfile);
            RefreshServerProfilesUI();
            LoadActiveServerUI();

            AppendConsoleLog($"[Zenith Wizard] ¡Servidor '{newProfile.Name}' creado exitosamente con Java {reqJava}!");
            SwitchToView("Dashboard");
        }

        private void BtnCreateServerProfile_Click(object sender, RoutedEventArgs e)
        {
            SwitchToView("Wizard");
        }

        private void LoadActiveServerUI()
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            TxtActiveServerName.Text = $"Servidor: {profile.Name}";
            TxtActiveSoftware.Text = $"Motor: {profile.SoftwareType} {profile.Version}";

            string serverDir = profile.DirectoryPath;
            if (!Directory.Exists(serverDir))
            {
                Directory.CreateDirectory(serverDir);
            }

            _currentProperties = _configService.LoadProperties(serverDir);
            TxtActivePort.Text = $"Puerto: {_currentProperties.GetProperty("server-port", "25565")}";

            LoadConfigurationToUI();
            RefreshFileList();
            LoadEngineConfigs();
        }

        #endregion

        #region Navigation View Switching

        private void SwitchToView(string tag)
        {
            ViewDashboard.Visibility = tag == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
            ViewWizard.Visibility = tag == "Wizard" ? Visibility.Visible : Visibility.Collapsed;
            ViewDownloads.Visibility = tag == "Downloads" ? Visibility.Visible : Visibility.Collapsed;
            ViewConfig.Visibility = tag == "Config" ? Visibility.Visible : Visibility.Collapsed;
            ViewFileManager.Visibility = tag == "FileManager" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString() ?? "";
                SwitchToView(tag);

                if (tag == "Config")
                {
                    LoadConfigurationToUI();
                    LoadEngineConfigs();
                }
                else if (tag == "FileManager")
                {
                    RefreshFileList();
                }
            }
        }

        #endregion

        #region Dashboard & Process Controls

        private void UpdateStatusIndicator(bool isRunning)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusIndicatorDot.Fill = new SolidColorBrush(isRunning ? Color.FromArgb(255, 34, 197, 94) : Color.FromArgb(255, 239, 68, 68));
                StatusIndicatorText.Text = isRunning ? "Servidor En Ejecución" : "Servidor Detenido";
                BtnStartServer.IsEnabled = !isRunning;
                BtnStopServer.IsEnabled = isRunning;
            });
        }

        private void BtnStartServer_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _configService == null || _processManager == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            try
            {
                string serverDir = profile.DirectoryPath;
                if (!Directory.Exists(serverDir))
                {
                    Directory.CreateDirectory(serverDir);
                }

                var jarFiles = Directory.GetFiles(serverDir, "*.jar");
                if (jarFiles.Length == 0)
                {
                    AppendConsoleLog("[Zenith] Error: No se encontró ningún archivo server.jar en la carpeta del servidor activo. Ve a 'Descargas' para instalar Paper, Spigot o Vanilla.");
                    return;
                }

                string jarPath = Path.GetFileName(jarFiles[0]);
                TxtActiveSoftware.Text = $"Motor: {jarPath}";

                if (!_configService.CheckEulaAccepted(serverDir))
                {
                    AppendConsoleLog("[Zenith] ADVERTENCIA: El archivo eula.txt no ha sido aceptado. Ve a 'Configuración Visual' para activar eula=true.");
                }

                int ramGb = profile.AllocatedRamGb;

                // Resolve Java executable path
                string javaPath = profile.JavaExecutablePath;
                if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
                {
                    var installedJava = _javaService?.GetAvailableJavaVersions().FirstOrDefault(v => File.Exists(v.InstalledPath));
                    javaPath = installedJava != null ? installedJava.InstalledPath : "java";
                }

                AppendConsoleLog($"[Zenith] Iniciando servidor '{profile.Name}' ({jarPath}) con {ramGb}GB RAM usando Java: {javaPath}...");
                bool started = _processManager.StartServer(javaPath, serverDir, jarPath, ramGb, profile.CustomJvmArgs);
                if (started)
                {
                    UpdateStatusIndicator(true);
                }
            }
            catch (Exception ex)
            {
                AppendConsoleLog($"[Zenith] Error al iniciar el servidor: {ex.Message}");
            }
        }

        private async void BtnStopServer_Click(object sender, RoutedEventArgs e)
        {
            if (_processManager == null) return;
            AppendConsoleLog("[Zenith] Enviando comando 'stop' al servidor...");
            await _processManager.StopServerAsync();
            UpdateStatusIndicator(false);
        }

        private void CmbRamAllocation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_serverManager != null && _serverManager.ActiveProfile != null && CmbRamAllocation.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Content.ToString()?.Replace("GB", "").Trim(), out int parsedRam))
                {
                    _serverManager.ActiveProfile.AllocatedRamGb = parsedRam;
                    _serverManager.UpdateProfile(_serverManager.ActiveProfile);
                }
            }
        }

        private void BtnSendCommand_Click(object sender, RoutedEventArgs e)
        {
            SendConsoleInput();
        }

        private void TxtConsoleInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendConsoleInput();
            }
        }

        private void SendConsoleInput()
        {
            string cmd = TxtConsoleInput.Text.Trim();
            if (!string.IsNullOrEmpty(cmd) && _processManager != null)
            {
                _processManager.SendCommand(cmd);
                TxtConsoleInput.Text = string.Empty;
            }
        }

        private void OnServerOutputReceived(string line)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                AppendConsoleLog(line);
            });
        }

        private void OnServerProcessExited(int exitCode)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                AppendConsoleLog($"[Zenith] El servidor se ha detenido con código de salida: {exitCode}");
                UpdateStatusIndicator(false);
            });
        }

        private void AppendConsoleLog(string message)
        {
            TxtConsoleLog.Text += $"{message}\n";
            ConsoleScrollViewer.ChangeView(null, ConsoleScrollViewer.ScrollableHeight, null);
        }

        #endregion

        #region Asynchronous Downloads & Versions

        private async Task LoadMojangVersionsAsync()
        {
            if (_downloadService == null) return;
            bool showSnapshots = ChkIncludeSnapshots.IsChecked == true;
            _allMojangVersions = await _downloadService.GetMojangVersionsAsync(showSnapshots);

            DispatcherQueue.TryEnqueue(() =>
            {
                CmbMojangVersions.ItemsSource = _allMojangVersions.Select(v => $"{v.Id} [{v.Type.ToUpper()}]").ToList();
                if (CmbMojangVersions.Items.Count > 0)
                {
                    CmbMojangVersions.SelectedIndex = 0;
                }
            });
        }

        private async void BtnFetchMojangVersions_Click(object sender, RoutedEventArgs e)
        {
            await LoadMojangVersionsAsync();
        }

        private async void ChkIncludeSnapshots_CheckedChanged(object sender, RoutedEventArgs e)
        {
            await LoadMojangVersionsAsync();
        }

        private void CmbMojangVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbMojangVersions.SelectedIndex >= 0 && CmbMojangVersions.SelectedIndex < _allMojangVersions.Count)
            {
                var selectedVersion = _allMojangVersions[CmbMojangVersions.SelectedIndex];
                TxtRecommendedJavaNotice.Text = $"Versión recomendada para Minecraft {selectedVersion.Id}: Java {selectedVersion.RecommendedJavaVersion}";
            }
        }

        private async void BtnDownloadJava21_Click(object sender, RoutedEventArgs e) => await HandleJavaDownloadAsync(21);
        private async void BtnDownloadJava17_Click(object sender, RoutedEventArgs e) => await HandleJavaDownloadAsync(17);
        private async void BtnDownloadJava8_Click(object sender, RoutedEventArgs e) => await HandleJavaDownloadAsync(8);

        private async Task HandleJavaDownloadAsync(int majorVersion)
        {
            if (_javaService == null) return;
            var versions = _javaService.GetAvailableJavaVersions();
            var target = versions.FirstOrDefault(v => v.MajorVersion == majorVersion);
            if (target != null)
            {
                GlobalDownloadProgressPanel.Visibility = Visibility.Visible;
                TxtDownloadStatus.Text = $"Descargando e instalando JDK {majorVersion} de forma asíncrona...";

                try
                {
                    string path = await _javaService.DownloadAndExtractJavaAsync(target, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => DownloadProgressBar.Value = p);
                    });

                    TxtDownloadStatus.Text = $"¡Java {majorVersion} instalado exitosamente en: {path}!";
                }
                catch (Exception ex)
                {
                    TxtDownloadStatus.Text = $"Error al descargar Java: {ex.Message}";
                }
                finally
                {
                    GlobalDownloadProgressPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void BtnInstallPaper_Click(object sender, RoutedEventArgs e) => await HandleJarDownloadAsync(ServerSoftwareType.Paper);
        private async void BtnInstallPurpur_Click(object sender, RoutedEventArgs e) => await HandleJarDownloadAsync(ServerSoftwareType.Purpur);
        private async void BtnInstallVanilla_Click(object sender, RoutedEventArgs e) => await HandleJarDownloadAsync(ServerSoftwareType.Vanilla);

        private async Task HandleJarDownloadAsync(ServerSoftwareType type)
        {
            if (_serverManager == null || _downloadService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            var presets = _downloadService.GetPresetServerJars();
            var target = presets.FirstOrDefault(j => j.SoftwareType == type);
            if (target != null)
            {
                GlobalDownloadProgressPanel.Visibility = Visibility.Visible;
                TxtDownloadStatus.Text = $"Descargando {target.FileName}...";

                try
                {
                    string path = await _downloadService.DownloadServerJarAsync(target, profile.DirectoryPath, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => DownloadProgressBar.Value = p);
                    });

                    TxtDownloadStatus.Text = $"¡Server JAR ({target.FileName}) instalado exitosamente en {profile.Name}!";
                    profile.JarFileName = target.FileName;
                    _serverManager.UpdateProfile(profile);

                    TxtActiveSoftware.Text = $"Motor: {target.FileName}";
                    RefreshFileList();
                }
                catch (Exception ex)
                {
                    TxtDownloadStatus.Text = $"Error al descargar JAR: {ex.Message}";
                }
                finally
                {
                    GlobalDownloadProgressPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        #region Visual Configuration & Engine Configs

        private void LoadConfigurationToUI()
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            _currentProperties = _configService.LoadProperties(profile.DirectoryPath);

            CfgMotd.Text = _currentProperties.GetProperty("motd", "A Minecraft Server");
            CfgMaxPlayers.Text = _currentProperties.GetProperty("max-players", "20");
            CfgServerPort.Text = _currentProperties.GetProperty("server-port", "25565");

            SelectComboByContent(CfgGamemode, _currentProperties.GetProperty("gamemode", "survival"));
            SelectComboByContent(CfgDifficulty, _currentProperties.GetProperty("difficulty", "easy"));

            CfgOnlineMode.IsOn = _currentProperties.GetProperty("online-mode", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            ToggleEula.IsOn = _configService.CheckEulaAccepted(profile.DirectoryPath);
        }

        private void LoadEngineConfigs()
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            var paperCfg = _configService.LoadSpecificConfig(profile.DirectoryPath, "paper-global.yml", "# Paper Global Configuration\nverbose: false\nconfig-version: 28\n");
            TxtPaperConfigEditor.Text = paperCfg.Content;

            var spigotCfg = _configService.LoadSpecificConfig(profile.DirectoryPath, "spigot.yml", "# Spigot Configuration File\nconfig-version: 12\nsettings:\n  bungeecord: false\n");
            TxtSpigotConfigEditor.Text = spigotCfg.Content;
        }

        private void SelectComboByContent(ComboBox combo, string val)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item && item.Content.ToString()?.Equals(val, StringComparison.OrdinalIgnoreCase) == true)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            _currentProperties.SetProperty("motd", CfgMotd.Text);
            _currentProperties.SetProperty("max-players", CfgMaxPlayers.Text);
            _currentProperties.SetProperty("server-port", CfgServerPort.Text);

            if (CfgGamemode.SelectedItem is ComboBoxItem gmItem)
            {
                _currentProperties.SetProperty("gamemode", gmItem.Content.ToString() ?? "survival");
            }

            if (CfgDifficulty.SelectedItem is ComboBoxItem diffItem)
            {
                _currentProperties.SetProperty("difficulty", diffItem.Content.ToString() ?? "easy");
            }

            _currentProperties.SetProperty("online-mode", CfgOnlineMode.IsOn.ToString().ToLower());

            _configService.SaveProperties(profile.DirectoryPath, _currentProperties);
            _configService.SetEulaAccepted(profile.DirectoryPath, ToggleEula.IsOn);

            TxtActivePort.Text = $"Puerto: {CfgServerPort.Text}";
            AppendConsoleLog("[Zenith] Configuración guardada en server.properties y eula.txt");
        }

        private void BtnSavePaperConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            _configService.SaveSpecificConfig(profile.DirectoryPath, "paper-global.yml", TxtPaperConfigEditor.Text);
            AppendConsoleLog("[Zenith] Guardado paper-global.yml exitosamente.");
        }

        private void BtnSaveSpigotConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            _configService.SaveSpecificConfig(profile.DirectoryPath, "spigot.yml", TxtSpigotConfigEditor.Text);
            AppendConsoleLog("[Zenith] Guardado spigot.yml exitosamente.");
        }

        private void BtnReloadConfig_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigurationToUI();
            LoadEngineConfigs();
        }

        private void ToggleEula_Toggled(object sender, RoutedEventArgs e)
        {
            if (_serverManager != null && _serverManager.ActiveProfile != null && _configService != null)
            {
                _configService.SetEulaAccepted(_serverManager.ActiveProfile.DirectoryPath, ToggleEula.IsOn);
            }
        }

        #endregion

        #region File Manager & Code Editor

        private void RefreshFileList()
        {
            if (_serverManager == null || _fileManager == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            var files = _fileManager.GetDirectoryContents(profile.DirectoryPath);
            LstFileItems.ItemsSource = files;
        }

        private void BtnRefreshFiles_Click(object sender, RoutedEventArgs e)
        {
            RefreshFileList();
        }

        private void LstFileItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_fileManager == null) return;
            if (LstFileItems.SelectedItem is ServerFileItem item)
            {
                _selectedFileItem = item;
                TxtEditingFileName.Text = item.Name;

                if (!item.IsDirectory)
                {
                    string content = _fileManager.ReadFileText(item.FullPath);
                    TxtCodeEditor.Text = content;
                    TxtFileEditorStatus.Text = $"Editando: {item.RelativePath} ({item.FormattedSize})";
                }
                else
                {
                    TxtCodeEditor.Text = "// [Carpeta seleccionada]";
                    TxtFileEditorStatus.Text = $"Carpeta: {item.RelativePath}";
                }
            }
        }

        private void BtnSaveFileContent_Click(object sender, RoutedEventArgs e)
        {
            if (_fileManager != null && _selectedFileItem != null && !_selectedFileItem.IsDirectory)
            {
                _fileManager.SaveFileText(_selectedFileItem.FullPath, TxtCodeEditor.Text);
                TxtFileEditorStatus.Text = $"¡Guardado exitosamente a las {DateTime.Now:HH:mm:ss}!";
                RefreshFileList();
            }
        }

        private void BtnNewFile_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _fileManager == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            string newFileName = $"config_custom_{DateTime.Now:mm_ss}.txt";
            string newFilePath = Path.Combine(profile.DirectoryPath, newFileName);
            _fileManager.CreateFile(newFilePath, "# Archivo creado desde Zenith MC Server Launcher\n");
            RefreshFileList();
        }

        private void BtnDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (_fileManager != null && _selectedFileItem != null)
            {
                _fileManager.DeleteItem(_selectedFileItem.FullPath);
                TxtCodeEditor.Text = string.Empty;
                TxtEditingFileName.Text = "Ningún archivo seleccionado";
                _selectedFileItem = null;
                RefreshFileList();
            }
        }

        #endregion
    }
}
