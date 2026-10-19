using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
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
        private readonly PluginModManagerService _pluginModManager;

        private ServerPropertiesConfig _currentProperties;
        private ServerFileItem? _selectedFileItem;
        private PluginModItem? _selectedPluginModItem;
        private List<MinecraftVersionInfo> _wizardMojangVersions = new();

        private readonly List<string> _knownMinecraftCommands = new()
        {
            "gamemode survival", "gamemode creative", "gamemode adventure", "gamemode spectator",
            "gamerule doDaylightCycle false", "gamerule doMobSpawning false", "gamerule keepInventory true",
            "op", "deop", "teleport", "tp", "give", "kick", "ban", "pardon", "stop", "reload", "save-all", "whitelist add", "whitelist remove", "list", "say"
        };

        public MainWindow()
        {
            _serverManager = new ServerManagerService();
            _javaService = new JavaInstallerService();
            _downloadService = new ServerDownloadService();
            _configService = new ServerConfigService();
            _fileManager = new FileManagerService();
            _processManager = new ServerProcessManager();
            _pluginModManager = new PluginModManagerService();
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
            LoadJavaRuntimesUI();
            LoadLauncherSettingsUI();

            // Fetch Mojang versions for wizard
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
            if (_downloadService == null) return;
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

                var loaders = _downloadService.GetCompatibleLoadersForVersion(ver.Id);
                WzdCmbLoader.ItemsSource = loaders;
                if (loaders.Count > 0) WzdCmbLoader.SelectedIndex = 0;
            }
        }

        private async void BtnFinishWizard_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _downloadService == null) return;

            // Prevent double-click creation
            BtnFinishWizard.IsEnabled = false;
            BtnFinishWizard.Content = "⏳ Creando Servidor e Instalando Archivos...";
            WzdProgressBar.Visibility = Visibility.Visible;

            try
            {
                string name = WzdServerName.Text.Trim();
                if (string.IsNullOrWhiteSpace(name)) name = "Nuevo Servidor";

                string icon = "⛏️";
                if (WzdServerIcon.SelectedItem is ComboBoxItem iconItem)
                {
                    icon = iconItem.Content.ToString()?.Split(' ')[0] ?? "⛏️";
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

                // 1. Create Server Instance Profile & Folder
                string displayName = $"{icon} {name}";
                var newProfile = _serverManager.CreateProfile(displayName, loader, version, ramGb);

                // 2. Download server.jar for selected loader & version
                WzdTxtProgressStatus.Text = $"Obteniendo {loader} {version} (server.jar)...";
                try
                {
                    string downloadedJarPath = await _downloadService.DownloadServerJarForVersionAsync(loader, version, newProfile.DirectoryPath, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => WzdProgressBar.Value = p);
                    });

                    newProfile.JarFileName = Path.GetFileName(downloadedJarPath);
                }
                catch (Exception jarEx)
                {
                    AppendConsoleLog($"[Zenith Warning] No se pudo obtener server.jar automático: {jarEx.Message}");
                }

                // 3. Set Aikar's JVM Flags
                if (WzdToggleAikarsFlags.IsOn && _configService != null)
                {
                    newProfile.CustomJvmArgs = _configService.GetAikarsJvmFlags(ramGb);
                }

                // 4. Download/Verify Java Runtime
                int reqJava = _downloadService.GetRecommendedJavaVersion(version);
                var javaVerOption = _javaService?.GetAvailableJavaVersions().FirstOrDefault(j => j.MajorVersion == reqJava);
                if (javaVerOption != null)
                {
                    if (File.Exists(javaVerOption.InstalledPath))
                    {
                        newProfile.JavaExecutablePath = javaVerOption.InstalledPath;
                    }
                    else
                    {
                        WzdTxtProgressStatus.Text = $"Instalando JDK Java {reqJava}...";
                        try
                        {
                            string downloadedJavaPath = await _javaService!.DownloadAndExtractJavaAsync(javaVerOption);
                            newProfile.JavaExecutablePath = downloadedJavaPath;
                        }
                        catch { }
                    }
                }

                // 5. Save EULA & server.properties
                if (_configService != null)
                {
                    _configService.SetEulaAccepted(newProfile.DirectoryPath, WzdToggleEula.IsOn);
                    var props = _configService.LoadProperties(newProfile.DirectoryPath);
                    props.SetProperty("server-port", port.ToString());
                    props.SetProperty("motd", $"{name} - Servidor Minecraft");
                    _configService.SaveProperties(newProfile.DirectoryPath, props);
                }

                // Select newly created server profile as active
                _serverManager.UpdateProfile(newProfile);
                _serverManager.SelectProfile(newProfile.Id);

                RefreshServerProfilesUI();
                LoadActiveServerUI();

                AppendConsoleLog($"[Zenith Wizard] ¡Servidor '{newProfile.Name}' creado exitosamente con Java {reqJava}!");
                SwitchToView("Dashboard");
            }
            catch (Exception ex)
            {
                AppendConsoleLog($"[Zenith Wizard Notice] {ex.Message}");
            }
            finally
            {
                BtnFinishWizard.IsEnabled = true;
                BtnFinishWizard.Content = "🚀 Finalizar y Crear Servidor";
                WzdProgressBar.Visibility = Visibility.Collapsed;
                WzdTxtProgressStatus.Text = string.Empty;
            }
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
            RefreshPluginsModsList();
        }

        #endregion

        #region Navigation View Switching

        private void SwitchToView(string tag)
        {
            ViewDashboard.Visibility = tag == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
            ViewWizard.Visibility = tag == "Wizard" ? Visibility.Visible : Visibility.Collapsed;
            ViewConfig.Visibility = tag == "Config" ? Visibility.Visible : Visibility.Collapsed;
            ViewPluginsMods.Visibility = tag == "PluginsMods" ? Visibility.Visible : Visibility.Collapsed;
            ViewFileManager.Visibility = tag == "FileManager" ? Visibility.Visible : Visibility.Collapsed;
            ViewJavaRuntimes.Visibility = tag == "JavaRuntimes" ? Visibility.Visible : Visibility.Collapsed;
            ViewLauncherSettings.Visibility = tag == "LauncherSettings" ? Visibility.Visible : Visibility.Collapsed;
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
                else if (tag == "PluginsMods")
                {
                    RefreshPluginsModsList();
                }
                else if (tag == "FileManager")
                {
                    RefreshFileList();
                }
                else if (tag == "JavaRuntimes")
                {
                    LoadJavaRuntimesUI();
                }
                else if (tag == "LauncherSettings")
                {
                    LoadLauncherSettingsUI();
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
                    AppendConsoleLog("[Zenith] Error: No se encontró ningún archivo server.jar en la carpeta del servidor activo. Usa el 'Asistente de Creación' para descargar e instalar el servidor.");
                    return;
                }

                string jarPath = Path.GetFileName(jarFiles[0]);
                TxtActiveSoftware.Text = $"Motor: {jarPath}";

                if (!_configService.CheckEulaAccepted(serverDir))
                {
                    AppendConsoleLog("[Zenith] ADVERTENCIA: El archivo eula.txt no ha sido aceptado. Ve a 'Configuración del Servidor' para activar eula=true.");
                }

                int ramGb = profile.AllocatedRamGb;

                // Resolve Java executable path
                string javaPath = profile.JavaExecutablePath;
                if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
                {
                    int reqJava = _downloadService?.GetRecommendedJavaVersion(profile.Version) ?? 21;
                    var targetJava = _javaService?.GetAvailableJavaVersions().FirstOrDefault(v => v.MajorVersion == reqJava && File.Exists(v.InstalledPath))
                                   ?? _javaService?.GetAvailableJavaVersions().FirstOrDefault(v => File.Exists(v.InstalledPath));
                    javaPath = targetJava != null ? targetJava.InstalledPath : "java";
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

        private void TxtConsoleInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtConsoleInput.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(query))
            {
                CmdSuggestionContainer.Visibility = Visibility.Collapsed;
                return;
            }

            var matches = _knownMinecraftCommands.Where(c => c.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 0)
            {
                LstCmdSuggestions.ItemsSource = matches;
                CmdSuggestionContainer.Visibility = Visibility.Visible;
            }
            else
            {
                CmdSuggestionContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void LstCmdSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstCmdSuggestions.SelectedItem is string selectedCmd)
            {
                TxtConsoleInput.Text = selectedCmd;
                CmdSuggestionContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void TxtConsoleInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendConsoleInput();
            }
            else if (e.Key == Windows.System.VirtualKey.Tab && LstCmdSuggestions.Items.Count > 0)
            {
                TxtConsoleInput.Text = LstCmdSuggestions.Items[0].ToString() ?? "";
                CmdSuggestionContainer.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void SendConsoleInput()
        {
            string cmd = TxtConsoleInput.Text.Trim();
            if (!string.IsNullOrEmpty(cmd) && _processManager != null)
            {
                _processManager.SendCommand(cmd);
                TxtConsoleInput.Text = string.Empty;
                CmdSuggestionContainer.Visibility = Visibility.Collapsed;
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

                if (ToggleAutoRestart.IsOn && exitCode != 0)
                {
                    AppendConsoleLog("[Zenith Auto-Restart] Reiniciando servidor automáticamente en 3 segundos...");
                    Task.Delay(3000).ContinueWith(_ => DispatcherQueue.TryEnqueue(() => BtnStartServer_Click(this, new RoutedEventArgs())));
                }
            });
        }

        private void AppendConsoleLog(string message)
        {
            TxtConsoleLog.Text += $"{message}\n";
            ConsoleScrollViewer.ChangeView(null, ConsoleScrollViewer.ScrollableHeight, null);
        }

        #endregion

        #region Visual Configuration & Extended Settings

        private void LoadConfigurationToUI()
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            _currentProperties = _configService.LoadProperties(profile.DirectoryPath);

            CfgMotd.Text = _currentProperties.GetProperty("motd", "A Minecraft Server");
            CfgMaxPlayers.Text = _currentProperties.GetProperty("max-players", "20");
            CfgServerPort.Text = _currentProperties.GetProperty("server-port", "25565");

            CfgViewDistance.Text = _currentProperties.GetProperty("view-distance", "10");
            CfgSimulationDistance.Text = _currentProperties.GetProperty("simulation-distance", "10");
            CfgLevelSeed.Text = _currentProperties.GetProperty("level-seed", "");
            CfgSpawnProtection.Text = _currentProperties.GetProperty("spawn-protection", "16");

            SelectComboByContent(CfgGamemode, _currentProperties.GetProperty("gamemode", "survival"));
            SelectComboByContent(CfgDifficulty, _currentProperties.GetProperty("difficulty", "easy"));

            CfgOnlineMode.IsOn = _currentProperties.GetProperty("online-mode", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            CfgPvp.IsOn = _currentProperties.GetProperty("pvp", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            CfgAllowFlight.IsOn = _currentProperties.GetProperty("allow-flight", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            CfgCommandBlocks.IsOn = _currentProperties.GetProperty("enable-command-block", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            CfgHardcore.IsOn = _currentProperties.GetProperty("hardcore", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

            ToggleEula.IsOn = _configService.CheckEulaAccepted(profile.DirectoryPath);

            TxtServerCustomJavaExe.Text = profile.JavaExecutablePath;
            TxtCustomJvmArgs.Text = profile.CustomJvmArgs;
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

        private void BtnApplyAikarsFlags_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager?.ActiveProfile != null && _configService != null)
            {
                int ram = _serverManager.ActiveProfile.AllocatedRamGb;
                TxtCustomJvmArgs.Text = _configService.GetAikarsJvmFlags(ram);
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

            _currentProperties.SetProperty("view-distance", CfgViewDistance.Text);
            _currentProperties.SetProperty("simulation-distance", CfgSimulationDistance.Text);
            _currentProperties.SetProperty("level-seed", CfgLevelSeed.Text);
            _currentProperties.SetProperty("spawn-protection", CfgSpawnProtection.Text);

            if (CfgGamemode.SelectedItem is ComboBoxItem gmItem)
            {
                _currentProperties.SetProperty("gamemode", gmItem.Content.ToString() ?? "survival");
            }

            if (CfgDifficulty.SelectedItem is ComboBoxItem diffItem)
            {
                _currentProperties.SetProperty("difficulty", diffItem.Content.ToString() ?? "easy");
            }

            _currentProperties.SetProperty("online-mode", CfgOnlineMode.IsOn.ToString().ToLower());
            _currentProperties.SetProperty("pvp", CfgPvp.IsOn.ToString().ToLower());
            _currentProperties.SetProperty("allow-flight", CfgAllowFlight.IsOn.ToString().ToLower());
            _currentProperties.SetProperty("enable-command-block", CfgCommandBlocks.IsOn.ToString().ToLower());
            _currentProperties.SetProperty("hardcore", CfgHardcore.IsOn.ToString().ToLower());

            profile.JavaExecutablePath = TxtServerCustomJavaExe.Text.Trim();
            profile.CustomJvmArgs = TxtCustomJvmArgs.Text.Trim();
            _serverManager.UpdateProfile(profile);

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

        #region Plugins & Mods Manager

        private bool IsPluginMode()
        {
            if (_serverManager?.ActiveProfile == null) return true;
            string loader = _serverManager.ActiveProfile.SoftwareType.ToLower();
            return !loader.Contains("fabric") && !loader.Contains("forge");
        }

        private void RefreshPluginsModsList()
        {
            if (_serverManager?.ActiveProfile == null || _pluginModManager == null) return;
            bool isPlugin = IsPluginMode();
            TxtPluginsModsTitle.Text = isPlugin ? "Gestión de Plugins (Spigot / Paper / Purpur)" : "Gestión de Mods (Fabric / Forge)";

            var items = _pluginModManager.GetItems(_serverManager.ActiveProfile.DirectoryPath, isPlugin);
            LstPluginModItems.ItemsSource = items;
        }

        private void BtnRefreshPlugins_Click(object sender, RoutedEventArgs e)
        {
            RefreshPluginsModsList();
        }

        private void ChkTogglePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.Tag is PluginModItem item && _pluginModManager != null)
            {
                _pluginModManager.ToggleItem(item.FullPath);
                RefreshPluginsModsList();
            }
        }

        private void BtnDeletePluginItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PluginModItem item && _pluginModManager != null)
            {
                _pluginModManager.DeleteItem(item.FullPath);
                RefreshPluginsModsList();
            }
        }

        private void LstPluginModItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstPluginModItems.SelectedItem is PluginModItem item)
            {
                _selectedPluginModItem = item;
                TxtPluginStatus.Text = $"Archivo: {item.FileName} | Tamaño: {item.FormattedSize}";
                TxtPluginConfigViewer.Text = $"// Configuración o detalles de {item.FileName}\n// Estado: {(item.IsEnabled ? "ACTIVO" : "DESACTIVADO")}";
            }
        }

        private void DropZoneBorder_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void DropZoneBorder_Drop(object sender, DragEventArgs e)
        {
            if (_serverManager?.ActiveProfile == null || _pluginModManager == null) return;

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var paths = items.Select(i => i.Path).ToList();

                _pluginModManager.CopyImportedFiles(_serverManager.ActiveProfile.DirectoryPath, paths, IsPluginMode());
                RefreshPluginsModsList();
                AppendConsoleLog($"[Zenith] Se importaron {paths.Count} archivos/carpetas a {(IsPluginMode() ? "plugins" : "mods")}.");
            }
        }

        private void BtnOpenExternalEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPluginModItem == null) return;
            string editor = _serverManager?.ActiveProfile?.ExternalEditorPath ?? "";

            try
            {
                if (!string.IsNullOrWhiteSpace(editor) && File.Exists(editor))
                {
                    Process.Start(editor, $"\"{_selectedPluginModItem.FullPath}\"");
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _selectedPluginModItem.FullPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                TxtPluginStatus.Text = $"Error al abrir editor: {ex.Message}";
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

        #region Java Runtimes & Launcher Settings

        private void LoadJavaRuntimesUI()
        {
            if (_javaService == null) return;
            var versions = _javaService.GetAvailableJavaVersions();
            LstJavaVersions.ItemsSource = versions;
        }

        private void LstJavaVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private async void BtnInstallJavaItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is JavaVersionOption option && _javaService != null)
            {
                btn.IsEnabled = false;
                JavaDownloadProgress.Visibility = Visibility.Visible;
                TxtJavaProgressStatus.Text = $"Descargando {option.Name}...";

                try
                {
                    string path = await _javaService.DownloadAndExtractJavaAsync(option, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => JavaDownloadProgress.Value = p);
                    });

                    TxtJavaProgressStatus.Text = $"¡Java {option.MajorVersion} instalado correctamente!";
                    LoadJavaRuntimesUI();
                }
                catch (Exception ex)
                {
                    TxtJavaProgressStatus.Text = $"Error instalando JDK: {ex.Message}";
                }
                finally
                {
                    btn.IsEnabled = true;
                    JavaDownloadProgress.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void LoadLauncherSettingsUI()
        {
            if (_javaService != null)
            {
                TxtCustomJavaPath.Text = _javaService.RuntimesDirectory;
            }
            if (_serverManager?.ActiveProfile != null)
            {
                TxtExternalEditorExecutablePath.Text = _serverManager.ActiveProfile.ExternalEditorPath;
            }
        }

        private void BtnSaveExternalEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager?.ActiveProfile != null)
            {
                _serverManager.ActiveProfile.ExternalEditorPath = TxtExternalEditorExecutablePath.Text.Trim();
                _serverManager.UpdateProfile(_serverManager.ActiveProfile);
                AppendConsoleLog("[Zenith] Editor de texto externo actualizado.");
            }
        }

        private void BtnResetJavaPath_Click(object sender, RoutedEventArgs e)
        {
            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZenithMCServer", "runtimes", "java");
            _javaService?.SetCustomRuntimesDirectory(defaultDir);
            LoadLauncherSettingsUI();
            LoadJavaRuntimesUI();
        }

        #endregion
    }
}
