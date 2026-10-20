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
        private readonly MarketplaceService _marketplaceService;

        private ServerPropertiesConfig _currentProperties;
        private FileTreeNode? _selectedFileTreeNode;
        private MarketplaceItem? _selectedMarketplaceItem;
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
            _marketplaceService = new MarketplaceService();
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
            if (_serverManager == null || CmbServerSelector == null) return;
            var profiles = _serverManager.GetProfiles();
            CmbServerSelector.ItemsSource = profiles;
            if (_serverManager.ActiveProfile != null)
            {
                CmbServerSelector.SelectedItem = _serverManager.ActiveProfile;
            }
        }

        private void CmbServerSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_serverManager != null && CmbServerSelector?.SelectedItem is ServerProfile profile)
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
            if (_downloadService == null || WzdChkSnapshots == null || WzdCmbVersion == null) return;
            bool showSnapshots = WzdChkSnapshots.IsChecked == true;
            _wizardMojangVersions = await _downloadService.GetMojangVersionsAsync(showSnapshots);

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
            if (_downloadService == null || WzdCmbVersion == null) return;
            if (WzdCmbVersion.SelectedIndex >= 0 && WzdCmbVersion.SelectedIndex < _wizardMojangVersions.Count)
            {
                var ver = _wizardMojangVersions[WzdCmbVersion.SelectedIndex];
                int reqJava = _downloadService.GetRecommendedJavaVersion(ver.Id);

                if (WzdTxtJavaRequirement != null)
                {
                    WzdTxtJavaRequirement.Text = $"Requisito Estricto: Java {reqJava} (Para Minecraft {ver.Id})";
                }

                var installedJava = _javaService?.GetAvailableJavaVersions().FirstOrDefault(j => j.MajorVersion == reqJava && File.Exists(j.InstalledPath));
                if (WzdTxtJavaStatus != null)
                {
                    if (installedJava != null)
                    {
                        WzdTxtJavaStatus.Text = $"Estado: Java {reqJava} detectado e instalado localmente.";
                    }
                    else
                    {
                        WzdTxtJavaStatus.Text = $"Estado: Java {reqJava} no detectado. Se descargará e instalará automáticamente.";
                    }
                }

                var loaders = _downloadService.GetCompatibleLoadersForVersion(ver.Id);
                if (WzdCmbLoader != null)
                {
                    WzdCmbLoader.ItemsSource = loaders;
                    if (loaders.Count > 0) WzdCmbLoader.SelectedIndex = 0;
                }
            }
        }

        private async void BtnFinishWizard_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _downloadService == null || BtnFinishWizard == null || WzdProgressBar == null) return;

            // Prevent double-click creation
            BtnFinishWizard.IsEnabled = false;
            BtnFinishWizard.Content = "⏳ Creando Servidor e Instalando Archivos...";
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

                // 1. Create Server Instance Profile & Folder
                string displayName = $"{icon} {name}";
                var newProfile = _serverManager.CreateProfile(displayName, loader, version, ramGb);

                // 2. Download server.jar for selected loader & version
                if (WzdTxtProgressStatus != null) WzdTxtProgressStatus.Text = $"Obteniendo {loader} {version} (server.jar)...";
                try
                {
                    string downloadedJarPath = await _downloadService.DownloadServerJarForVersionAsync(loader, version, newProfile.DirectoryPath, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => { if (WzdProgressBar != null) WzdProgressBar.Value = p; });
                    });

                    newProfile.JarFileName = Path.GetFileName(downloadedJarPath);
                }
                catch (Exception jarEx)
                {
                    AppendConsoleLog($"[Zenith Warning] No se pudo obtener server.jar automático: {jarEx.Message}");
                }

                // 3. Set Aikar's JVM Flags
                if (WzdToggleAikarsFlags != null && WzdToggleAikarsFlags.IsOn && _configService != null)
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
                        if (WzdTxtProgressStatus != null) WzdTxtProgressStatus.Text = $"Instalando JDK Java {reqJava}...";
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
                    bool eulaAccepted = WzdToggleEula?.IsOn ?? true;
                    _configService.SetEulaAccepted(newProfile.DirectoryPath, eulaAccepted);
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
                if (WzdTxtProgressStatus != null) WzdTxtProgressStatus.Text = string.Empty;
            }
        }

        private void BtnCreateServerProfile_Click(object sender, RoutedEventArgs e)
        {
            SwitchToView("Wizard");
        }

        private async void LoadActiveServerUI()
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            if (TxtActiveServerName != null) TxtActiveServerName.Text = $"Servidor: {profile.Name}";
            if (TxtActiveSoftware != null) TxtActiveSoftware.Text = $"Motor: {profile.SoftwareType} {profile.Version}";

            string serverDir = profile.DirectoryPath;
            if (!Directory.Exists(serverDir))
            {
                Directory.CreateDirectory(serverDir);
            }

            _currentProperties = _configService.LoadProperties(serverDir);
            if (TxtActivePort != null) TxtActivePort.Text = $"Puerto: {_currentProperties.GetProperty("server-port", "25565")}";

            bool isPluginLoader = IsPluginMode();
            if (NavItemDownloadPlugins != null) NavItemDownloadPlugins.IsEnabled = isPluginLoader;
            if (NavItemInstalledPlugins != null) NavItemInstalledPlugins.IsEnabled = isPluginLoader;

            if (NavItemDownloadMods != null) NavItemDownloadMods.IsEnabled = !isPluginLoader;
            if (NavItemInstalledMods != null) NavItemInstalledMods.IsEnabled = !isPluginLoader;

            LoadConfigurationToUI();
            RefreshFileList();
            LoadEngineConfigs();
            RefreshInstalledPluginsList();
            RefreshInstalledModsList();
            await RefreshMarketplacePluginCatalogAsync();
            await RefreshMarketplaceModCatalogAsync();
        }

        #endregion

        #region Navigation View Switching

        private void SwitchToView(string tag)
        {
            if (ViewDashboard != null) ViewDashboard.Visibility = tag == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewWizard != null) ViewWizard.Visibility = tag == "Wizard" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewConfig != null) ViewConfig.Visibility = tag == "Config" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewDownloadPlugins != null) ViewDownloadPlugins.Visibility = tag == "DownloadPlugins" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewDownloadMods != null) ViewDownloadMods.Visibility = tag == "DownloadMods" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewInstalledPlugins != null) ViewInstalledPlugins.Visibility = tag == "InstalledPlugins" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewInstalledMods != null) ViewInstalledMods.Visibility = tag == "InstalledMods" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewMarketplaceDetails != null) ViewMarketplaceDetails.Visibility = tag == "MarketplaceDetails" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewFileManager != null) ViewFileManager.Visibility = tag == "FileManager" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewJavaRuntimes != null) ViewJavaRuntimes.Visibility = tag == "JavaRuntimes" ? Visibility.Visible : Visibility.Collapsed;
            if (ViewLauncherSettings != null) ViewLauncherSettings.Visibility = tag == "LauncherSettings" ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
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
                else if (tag == "DownloadPlugins")
                {
                    await RefreshMarketplacePluginCatalogAsync();
                }
                else if (tag == "DownloadMods")
                {
                    await RefreshMarketplaceModCatalogAsync();
                }
                else if (tag == "InstalledPlugins")
                {
                    RefreshInstalledPluginsList();
                }
                else if (tag == "InstalledMods")
                {
                    RefreshInstalledModsList();
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
                if (StatusIndicatorDot != null) StatusIndicatorDot.Fill = new SolidColorBrush(isRunning ? Color.FromArgb(255, 34, 197, 94) : Color.FromArgb(255, 239, 68, 68));
                if (StatusIndicatorText != null) StatusIndicatorText.Text = isRunning ? "Servidor En Ejecución" : "Servidor Detenido";
                if (BtnStartServer != null) BtnStartServer.IsEnabled = !isRunning;
                if (BtnStopServer != null) BtnStopServer.IsEnabled = isRunning;
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
                if (TxtActiveSoftware != null) TxtActiveSoftware.Text = $"Motor: {jarPath}";

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
            if (_serverManager != null && _serverManager.ActiveProfile != null && CmbRamAllocation?.SelectedItem is ComboBoxItem item)
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
            if (TxtConsoleInput == null || CmdSuggestionContainer == null) return;
            string query = TxtConsoleInput.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(query))
            {
                CmdSuggestionContainer.Visibility = Visibility.Collapsed;
                return;
            }

            var matches = _knownMinecraftCommands.Where(c => c.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 0)
            {
                if (LstCmdSuggestions != null) LstCmdSuggestions.ItemsSource = matches;
                CmdSuggestionContainer.Visibility = Visibility.Visible;
            }
            else
            {
                CmdSuggestionContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void LstCmdSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstCmdSuggestions?.SelectedItem is string selectedCmd)
            {
                if (TxtConsoleInput != null) TxtConsoleInput.Text = selectedCmd;
                if (CmdSuggestionContainer != null) CmdSuggestionContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void TxtConsoleInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendConsoleInput();
            }
            else if (e.Key == Windows.System.VirtualKey.Tab && LstCmdSuggestions != null && LstCmdSuggestions.Items.Count > 0)
            {
                if (TxtConsoleInput != null) TxtConsoleInput.Text = LstCmdSuggestions.Items[0].ToString() ?? "";
                if (CmdSuggestionContainer != null) CmdSuggestionContainer.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void SendConsoleInput()
        {
            if (TxtConsoleInput == null) return;
            string cmd = TxtConsoleInput.Text.Trim();
            if (!string.IsNullOrEmpty(cmd) && _processManager != null)
            {
                _processManager.SendCommand(cmd);
                TxtConsoleInput.Text = string.Empty;
                if (CmdSuggestionContainer != null) CmdSuggestionContainer.Visibility = Visibility.Collapsed;
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

                if (ToggleAutoRestart != null && ToggleAutoRestart.IsOn && exitCode != 0)
                {
                    AppendConsoleLog("[Zenith Auto-Restart] Reiniciando servidor automáticamente en 3 segundos...");
                    Task.Delay(3000).ContinueWith(_ => DispatcherQueue.TryEnqueue(() => BtnStartServer_Click(this, new RoutedEventArgs())));
                }
            });
        }

        private void AppendConsoleLog(string message)
        {
            if (TxtConsoleLog != null && ConsoleScrollViewer != null)
            {
                TxtConsoleLog.Text += $"{message}\n";
                ConsoleScrollViewer.ChangeView(null, ConsoleScrollViewer.ScrollableHeight, null);
            }
        }

        #endregion

        #region Visual Configuration & Extended Settings

        private void LoadConfigurationToUI()
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            _currentProperties = _configService.LoadProperties(profile.DirectoryPath);

            if (CfgMotd != null) CfgMotd.Text = _currentProperties.GetProperty("motd", "A Minecraft Server");
            if (CfgMaxPlayers != null) CfgMaxPlayers.Text = _currentProperties.GetProperty("max-players", "20");
            if (CfgServerPort != null) CfgServerPort.Text = _currentProperties.GetProperty("server-port", "25565");

            if (CfgViewDistance != null) CfgViewDistance.Text = _currentProperties.GetProperty("view-distance", "10");
            if (CfgSimulationDistance != null) CfgSimulationDistance.Text = _currentProperties.GetProperty("simulation-distance", "10");
            if (CfgLevelSeed != null) CfgLevelSeed.Text = _currentProperties.GetProperty("level-seed", "");
            if (CfgSpawnProtection != null) CfgSpawnProtection.Text = _currentProperties.GetProperty("spawn-protection", "16");

            if (CfgGamemode != null) SelectComboByContent(CfgGamemode, _currentProperties.GetProperty("gamemode", "survival"));
            if (CfgDifficulty != null) SelectComboByContent(CfgDifficulty, _currentProperties.GetProperty("difficulty", "easy"));

            if (CfgOnlineMode != null) CfgOnlineMode.IsOn = _currentProperties.GetProperty("online-mode", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (CfgPvp != null) CfgPvp.IsOn = _currentProperties.GetProperty("pvp", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (CfgAllowFlight != null) CfgAllowFlight.IsOn = _currentProperties.GetProperty("allow-flight", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (CfgCommandBlocks != null) CfgCommandBlocks.IsOn = _currentProperties.GetProperty("enable-command-block", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (CfgHardcore != null) CfgHardcore.IsOn = _currentProperties.GetProperty("hardcore", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

            if (ToggleEula != null) ToggleEula.IsOn = _configService.CheckEulaAccepted(profile.DirectoryPath);

            if (TxtServerCustomJavaExe != null) TxtServerCustomJavaExe.Text = profile.JavaExecutablePath;
            if (TxtCustomJvmArgs != null) TxtCustomJvmArgs.Text = profile.CustomJvmArgs;
        }

        private void LoadEngineConfigs()
        {
            if (_serverManager == null || _configService == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            var paperCfg = _configService.LoadSpecificConfig(profile.DirectoryPath, "paper-global.yml", "# Paper Global Configuration\nverbose: false\nconfig-version: 28\n");
            if (TxtPaperConfigEditor != null) TxtPaperConfigEditor.Text = paperCfg.Content;

            var spigotCfg = _configService.LoadSpecificConfig(profile.DirectoryPath, "spigot.yml", "# Spigot Configuration File\nconfig-version: 12\nsettings:\n  bungeecord: false\n");
            if (TxtSpigotConfigEditor != null) TxtSpigotConfigEditor.Text = spigotCfg.Content;
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
            if (_serverManager?.ActiveProfile != null && _configService != null && TxtCustomJvmArgs != null)
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

            if (CfgMotd != null) _currentProperties.SetProperty("motd", CfgMotd.Text);
            if (CfgMaxPlayers != null) _currentProperties.SetProperty("max-players", CfgMaxPlayers.Text);
            if (CfgServerPort != null) _currentProperties.SetProperty("server-port", CfgServerPort.Text);

            if (CfgViewDistance != null) _currentProperties.SetProperty("view-distance", CfgViewDistance.Text);
            if (CfgSimulationDistance != null) _currentProperties.SetProperty("simulation-distance", CfgSimulationDistance.Text);
            if (CfgLevelSeed != null) _currentProperties.SetProperty("level-seed", CfgLevelSeed.Text);
            if (CfgSpawnProtection != null) _currentProperties.SetProperty("spawn-protection", CfgSpawnProtection.Text);

            if (CfgGamemode?.SelectedItem is ComboBoxItem gmItem)
            {
                _currentProperties.SetProperty("gamemode", gmItem.Content.ToString() ?? "survival");
            }

            if (CfgDifficulty?.SelectedItem is ComboBoxItem diffItem)
            {
                _currentProperties.SetProperty("difficulty", diffItem.Content.ToString() ?? "easy");
            }

            if (CfgOnlineMode != null) _currentProperties.SetProperty("online-mode", CfgOnlineMode.IsOn.ToString().ToLower());
            if (CfgPvp != null) _currentProperties.SetProperty("pvp", CfgPvp.IsOn.ToString().ToLower());
            if (CfgAllowFlight != null) _currentProperties.SetProperty("allow-flight", CfgAllowFlight.IsOn.ToString().ToLower());
            if (CfgCommandBlocks != null) _currentProperties.SetProperty("enable-command-block", CfgCommandBlocks.IsOn.ToString().ToLower());
            if (CfgHardcore != null) _currentProperties.SetProperty("hardcore", CfgHardcore.IsOn.ToString().ToLower());

            if (TxtServerCustomJavaExe != null) profile.JavaExecutablePath = TxtServerCustomJavaExe.Text.Trim();
            if (TxtCustomJvmArgs != null) profile.CustomJvmArgs = TxtCustomJvmArgs.Text.Trim();
            _serverManager.UpdateProfile(profile);

            _configService.SaveProperties(profile.DirectoryPath, _currentProperties);
            if (ToggleEula != null) _configService.SetEulaAccepted(profile.DirectoryPath, ToggleEula.IsOn);

            if (TxtActivePort != null && CfgServerPort != null) TxtActivePort.Text = $"Puerto: {CfgServerPort.Text}";
            AppendConsoleLog("[Zenith] Configuración guardada en server.properties y eula.txt");
        }

        private void BtnSavePaperConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _configService == null || TxtPaperConfigEditor == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            _configService.SaveSpecificConfig(profile.DirectoryPath, "paper-global.yml", TxtPaperConfigEditor.Text);
            AppendConsoleLog("[Zenith] Guardado paper-global.yml exitosamente.");
        }

        private void BtnSaveSpigotConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _configService == null || TxtSpigotConfigEditor == null) return;
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
            if (_serverManager != null && _serverManager.ActiveProfile != null && _configService != null && ToggleEula != null)
            {
                _configService.SetEulaAccepted(_serverManager.ActiveProfile.DirectoryPath, ToggleEula.IsOn);
            }
        }

        #endregion

        #region Marketplace & Installed Plugins / Mods

        private async Task RefreshMarketplacePluginCatalogAsync()
        {
            if (_marketplaceService == null || _serverManager?.ActiveProfile == null || GrdDownloadPlugins == null) return;
            string ver = (CmbPluginVersionFilter?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Versiones";
            if (ver == "Todas las Versiones") ver = _serverManager.ActiveProfile.Version;

            string category = (CmbPluginCategoryFilter?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Categorías";
            string loader = _serverManager.ActiveProfile.SoftwareType;
            string source = (CmbPluginSource?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Fuentes";
            string search = TxtPluginSearch?.Text ?? "";
            string sort = (CmbPluginSort != null && CmbPluginSort.SelectedIndex == 1) ? "Name" : "Downloads";

            var items = await _marketplaceService.GetFeaturedItemsAsync(MarketplaceCategory.Plugin, ver, loader, source, search, sort, category);
            GrdDownloadPlugins.ItemsSource = items;
        }

        private async Task RefreshMarketplaceModCatalogAsync()
        {
            if (_marketplaceService == null || _serverManager?.ActiveProfile == null || GrdDownloadMods == null) return;
            string ver = (CmbModVersionFilter?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Versiones";
            if (ver == "Todas las Versiones") ver = _serverManager.ActiveProfile.Version;

            string category = (CmbModCategoryFilter?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Categorías";
            string loader = _serverManager.ActiveProfile.SoftwareType;
            string source = (CmbModSource?.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Fuentes";
            string search = TxtModSearch?.Text ?? "";
            string sort = (CmbModSort != null && CmbModSort.SelectedIndex == 1) ? "Name" : "Downloads";

            var items = await _marketplaceService.GetFeaturedItemsAsync(MarketplaceCategory.Mod, ver, loader, source, search, sort, category);
            GrdDownloadMods.ItemsSource = items;
        }

        private async void CmbPluginSource_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshMarketplacePluginCatalogAsync();
        private async void CmbPluginVersionFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshMarketplacePluginCatalogAsync();
        private async void CmbPluginCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshMarketplacePluginCatalogAsync();
        private async void TxtPluginSearch_TextChanged(object sender, TextChangedEventArgs e) => await RefreshMarketplacePluginCatalogAsync();
        private async void CmbPluginSort_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshMarketplacePluginCatalogAsync();

        private async void CmbModSource_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshMarketplaceModCatalogAsync();
        private async void CmbModVersionFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshMarketplaceModCatalogAsync();
        private async void CmbModCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshMarketplaceModCatalogAsync();
        private async void TxtModSearch_TextChanged(object sender, TextChangedEventArgs e) => await RefreshMarketplaceModCatalogAsync();
        private async void CmbModSort_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshMarketplaceModCatalogAsync();

        private void GrdDownloadPlugins_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MarketplaceItem item)
            {
                OpenMarketplaceItemDetails(item);
            }
        }

        private void GrdDownloadMods_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MarketplaceItem item)
            {
                OpenMarketplaceItemDetails(item);
            }
        }

        private void OpenMarketplaceItemDetails(MarketplaceItem item)
        {
            _selectedMarketplaceItem = item;
            if (TxtDetailTitle != null) TxtDetailTitle.Text = item.Title;
            if (TxtDetailAuthor != null) TxtDetailAuthor.Text = $"Autor: {item.Author}";
            if (TxtDetailSource != null) TxtDetailSource.Text = $"Fuente: {item.SourceName}";
            if (TxtDetailDownloads != null) TxtDetailDownloads.Text = $"Descargas: {item.FormattedDownloads}";
            if (TxtDetailSummary != null) TxtDetailSummary.Text = item.Summary;
            if (TxtDetailDescription != null) TxtDetailDescription.Text = item.Description;

            SwitchToView("MarketplaceDetails");
        }

        private void BtnBackToMarketplace_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMarketplaceItem != null && _selectedMarketplaceItem.Category == MarketplaceCategory.Mod)
            {
                SwitchToView("DownloadMods");
            }
            else
            {
                SwitchToView("DownloadPlugins");
            }
        }

        private async void BtnDetailInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMarketplaceItem != null && _serverManager?.ActiveProfile != null && _marketplaceService != null && BtnDetailInstall != null)
            {
                BtnDetailInstall.IsEnabled = false;
                BtnDetailInstall.Content = "⏳ Instalando...";
                try
                {
                    string path = await _marketplaceService.DownloadAndInstallItemAsync(_selectedMarketplaceItem, _serverManager.ActiveProfile.DirectoryPath);
                    AppendConsoleLog($"[Zenith Marketplace] Instalado '{_selectedMarketplaceItem.Title}' en {path}");
                    BtnDetailInstall.Content = "¡Instalación Exitosa!";
                }
                catch (Exception ex)
                {
                    AppendConsoleLog($"[Zenith Marketplace Error] {ex.Message}");
                    BtnDetailInstall.Content = "Error al instalar";
                }
                finally
                {
                    await Task.Delay(1500);
                    BtnDetailInstall.IsEnabled = true;
                    BtnDetailInstall.Content = "🚀 Instalar en Servidor";
                }
            }
        }

        private async void BtnInstallMarketplaceItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MarketplaceItem item && _serverManager?.ActiveProfile != null && _marketplaceService != null)
            {
                btn.IsEnabled = false;
                btn.Content = "⏳";
                try
                {
                    await _marketplaceService.DownloadAndInstallItemAsync(item, _serverManager.ActiveProfile.DirectoryPath);
                    btn.Content = "✓";
                    AppendConsoleLog($"[Zenith Marketplace] Instalado '{item.Title}' ({item.JarFileName}).");
                }
                catch (Exception ex)
                {
                    btn.Content = "Error";
                    AppendConsoleLog($"[Zenith Marketplace Error] {ex.Message}");
                }
            }
        }

        private void RefreshInstalledPluginsList()
        {
            if (_serverManager?.ActiveProfile == null || _pluginModManager == null || LstInstalledPlugins == null) return;
            var items = _pluginModManager.GetItems(_serverManager.ActiveProfile.DirectoryPath, true);
            LstInstalledPlugins.ItemsSource = items;
        }

        private void RefreshInstalledModsList()
        {
            if (_serverManager?.ActiveProfile == null || _pluginModManager == null || LstInstalledMods == null) return;
            var items = _pluginModManager.GetItems(_serverManager.ActiveProfile.DirectoryPath, false);
            LstInstalledMods.ItemsSource = items;
        }

        private void BtnRefreshInstalledPlugins_Click(object sender, RoutedEventArgs e) => RefreshInstalledPluginsList();
        private void BtnRefreshInstalledMods_Click(object sender, RoutedEventArgs e) => RefreshInstalledModsList();

        private void ChkTogglePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.Tag is PluginModItem item && _pluginModManager != null)
            {
                _pluginModManager.ToggleItem(item.FullPath);
                RefreshInstalledPluginsList();
            }
        }

        private void BtnDeletePluginItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PluginModItem item && _pluginModManager != null)
            {
                _pluginModManager.DeleteItem(item.FullPath);
                RefreshInstalledPluginsList();
            }
        }

        private void ChkToggleMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.Tag is PluginModItem item && _pluginModManager != null)
            {
                _pluginModManager.ToggleItem(item.FullPath);
                RefreshInstalledModsList();
            }
        }

        private void BtnDeleteModItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PluginModItem item && _pluginModManager != null)
            {
                _pluginModManager.DeleteItem(item.FullPath);
                RefreshInstalledModsList();
            }
        }

        private void DropZoneBorder_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void DropZonePlugins_Drop(object sender, DragEventArgs e)
        {
            if (_serverManager?.ActiveProfile == null || _pluginModManager == null) return;

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var paths = items.Select(i => i.Path).ToList();

                _pluginModManager.CopyImportedFiles(_serverManager.ActiveProfile.DirectoryPath, paths, true);
                RefreshInstalledPluginsList();
                AppendConsoleLog($"[Zenith] Se importaron {paths.Count} archivos/carpetas a plugins.");
            }
        }

        private async void DropZoneMods_Drop(object sender, DragEventArgs e)
        {
            if (_serverManager?.ActiveProfile == null || _pluginModManager == null) return;

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var paths = items.Select(i => i.Path).ToList();

                _pluginModManager.CopyImportedFiles(_serverManager.ActiveProfile.DirectoryPath, paths, false);
                RefreshInstalledModsList();
                AppendConsoleLog($"[Zenith] Se importaron {paths.Count} archivos/carpetas a mods.");
            }
        }

        #endregion

        #region File Manager & Code Editor (TreeView Edition)

        private bool IsPluginMode()
        {
            if (_serverManager?.ActiveProfile == null) return true;
            string loader = _serverManager.ActiveProfile.SoftwareType.ToLower();
            return !loader.Contains("fabric") && !loader.Contains("forge");
        }

        private void RefreshFileList()
        {
            if (_serverManager == null || TreeFileExplorer == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            if (!Directory.Exists(profile.DirectoryPath))
            {
                Directory.CreateDirectory(profile.DirectoryPath);
            }

            var treeRoot = FileTreeNode.BuildTree(profile.DirectoryPath);
            TreeFileExplorer.ItemsSource = new List<FileTreeNode> { treeRoot };
        }

        private void BtnRefreshFiles_Click(object sender, RoutedEventArgs e)
        {
            RefreshFileList();
        }

        private void TreeFileExplorer_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (_fileManager == null || args.InvokedItem is not FileTreeNode node) return;

            _selectedFileTreeNode = node;
            if (TxtEditingFileName != null) TxtEditingFileName.Text = node.Name;

            if (!node.IsDirectory)
            {
                string content = _fileManager.ReadFileText(node.FullPath);
                if (TxtCodeEditor != null) TxtCodeEditor.Text = content;
                if (TxtFileEditorStatus != null) TxtFileEditorStatus.Text = $"Editando: {node.RelativePath} ({node.FormattedSize})";
            }
            else
            {
                if (TxtCodeEditor != null) TxtCodeEditor.Text = "// [Carpeta seleccionada]";
                if (TxtFileEditorStatus != null) TxtFileEditorStatus.Text = $"Carpeta: {node.RelativePath}";
            }
        }

        private void BtnSaveFileContent_Click(object sender, RoutedEventArgs e)
        {
            if (_fileManager != null && _selectedFileTreeNode != null && !_selectedFileTreeNode.IsDirectory && TxtCodeEditor != null)
            {
                _fileManager.SaveFileText(_selectedFileTreeNode.FullPath, TxtCodeEditor.Text);
                if (TxtFileEditorStatus != null) TxtFileEditorStatus.Text = $"¡Guardado exitosamente a las {DateTime.Now:HH:mm:ss}!";
                RefreshFileList();
            }
        }

        private void BtnNewFile_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager == null || _fileManager == null) return;
            var profile = _serverManager.ActiveProfile;
            if (profile == null) return;

            string targetDir = profile.DirectoryPath;
            if (_selectedFileTreeNode != null)
            {
                targetDir = _selectedFileTreeNode.IsDirectory ? _selectedFileTreeNode.FullPath : Path.GetDirectoryName(_selectedFileTreeNode.FullPath) ?? profile.DirectoryPath;
            }

            string newFileName = $"config_custom_{DateTime.Now:mm_ss}.txt";
            string newFilePath = Path.Combine(targetDir, newFileName);
            _fileManager.CreateFile(newFilePath, "# Archivo creado desde Zenith MC Server Launcher\n");
            RefreshFileList();
        }

        private void BtnDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (_fileManager != null && _selectedFileTreeNode != null)
            {
                _fileManager.DeleteItem(_selectedFileTreeNode.FullPath);
                if (TxtCodeEditor != null) TxtCodeEditor.Text = string.Empty;
                if (TxtEditingFileName != null) TxtEditingFileName.Text = "Ningún archivo seleccionado";
                _selectedFileTreeNode = null;
                RefreshFileList();
            }
        }

        #endregion

        #region Java Runtimes & Launcher Settings

        private void LoadJavaRuntimesUI()
        {
            if (_javaService == null || LstJavaVersions == null) return;
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
                if (JavaDownloadProgress != null) JavaDownloadProgress.Visibility = Visibility.Visible;
                if (TxtJavaProgressStatus != null) TxtJavaProgressStatus.Text = $"Descargando {option.Name}...";

                try
                {
                    string path = await _javaService.DownloadAndExtractJavaAsync(option, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => { if (JavaDownloadProgress != null) JavaDownloadProgress.Value = p; });
                    });

                    if (TxtJavaProgressStatus != null) TxtJavaProgressStatus.Text = $"¡Java {option.MajorVersion} instalado correctamente!";
                    LoadJavaRuntimesUI();
                }
                catch (Exception ex)
                {
                    if (TxtJavaProgressStatus != null) TxtJavaProgressStatus.Text = $"Error instalando JDK: {ex.Message}";
                }
                finally
                {
                    btn.IsEnabled = true;
                    if (JavaDownloadProgress != null) JavaDownloadProgress.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void LoadLauncherSettingsUI()
        {
            if (_javaService != null && TxtCustomJavaPath != null)
            {
                TxtCustomJavaPath.Text = _javaService.RuntimesDirectory;
            }
            if (_serverManager?.ActiveProfile != null && TxtExternalEditorExecutablePath != null)
            {
                TxtExternalEditorExecutablePath.Text = _serverManager.ActiveProfile.ExternalEditorPath;
            }
        }

        private void BtnSaveExternalEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager?.ActiveProfile != null && TxtExternalEditorExecutablePath != null)
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
