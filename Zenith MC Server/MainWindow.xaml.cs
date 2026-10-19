using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
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
        private readonly string _serverDirectory;
        private readonly JavaInstallerService _javaService;
        private readonly ServerDownloadService _downloadService;
        private readonly ServerConfigService _configService;
        private readonly FileManagerService _fileManager;
        private readonly ServerProcessManager _processManager;

        private ServerPropertiesConfig _currentProperties;
        private ServerFileItem? _selectedFileItem;

        public MainWindow()
        {
            this.InitializeComponent();

            _serverDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "minecraft_server");
            if (!Directory.Exists(_serverDirectory))
            {
                Directory.CreateDirectory(_serverDirectory);
            }

            _javaService = new JavaInstallerService();
            _downloadService = new ServerDownloadService();
            _configService = new ServerConfigService();
            _fileManager = new FileManagerService();
            _processManager = new ServerProcessManager();

            _currentProperties = _configService.LoadProperties(_serverDirectory);

            _processManager.OutputReceived += OnServerOutputReceived;
            _processManager.ProcessExited += OnServerProcessExited;

            LoadConfigurationToUI();
            RefreshFileList();
            UpdateStatusIndicator(false);
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString() ?? "";

                ViewDashboard.Visibility = tag == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
                ViewDownloads.Visibility = tag == "Downloads" ? Visibility.Visible : Visibility.Collapsed;
                ViewConfig.Visibility = tag == "Config" ? Visibility.Visible : Visibility.Collapsed;
                ViewFileManager.Visibility = tag == "FileManager" ? Visibility.Visible : Visibility.Collapsed;

                if (tag == "Config")
                {
                    LoadConfigurationToUI();
                }
                else if (tag == "FileManager")
                {
                    RefreshFileList();
                }
            }
        }

        #region Dashboard & Process Management

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
            try
            {
                if (!Directory.Exists(_serverDirectory))
                {
                    Directory.CreateDirectory(_serverDirectory);
                }

                var jarFiles = Directory.GetFiles(_serverDirectory, "*.jar");
                if (jarFiles.Length == 0)
                {
                    AppendConsoleLog("[Zenith] Error: No se encontró ningún archivo server.jar en la carpeta del servidor. Ve a 'Descargas' para instalar Paper, Spigot o Vanilla.");
                    return;
                }

                string jarPath = Path.GetFileName(jarFiles[0]);
                TxtActiveSoftware.Text = $"JAR: {jarPath}";

                if (!_configService.CheckEulaAccepted(_serverDirectory))
                {
                    AppendConsoleLog("[Zenith] ADVERTENCIA: El archivo eula.txt no ha sido aceptado. Ve a 'Configuración Visual' para activar eula=true.");
                }

                int ramGb = 4;
                if (CmbRamAllocation.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString()?.Replace("GB", "").Trim(), out int parsedRam))
                {
                    ramGb = parsedRam;
                }

                // Resolve Java runtime path (downloaded Java or system Java)
                string javaPath = "java";
                var installedJava = _javaService.GetAvailableJavaVersions().FirstOrDefault(v => File.Exists(v.InstalledPath));
                if (installedJava != null)
                {
                    javaPath = installedJava.InstalledPath;
                }

                AppendConsoleLog($"[Zenith] Iniciando servidor Minecraft ({jarPath}) con {ramGb}GB RAM usando Java: {javaPath}...");
                bool started = _processManager.StartServer(javaPath, _serverDirectory, jarPath, ramGb);
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
            AppendConsoleLog("[Zenith] Enviando comando de apagado seguro 'stop'...");
            await _processManager.StopServerAsync();
            UpdateStatusIndicator(false);
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
            if (!string.IsNullOrEmpty(cmd))
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

        #region Downloads & Installation

        private async void BtnDownloadJava21_Click(object sender, RoutedEventArgs e)
        {
            await HandleJavaDownload(21);
        }

        private async void BtnDownloadJava17_Click(object sender, RoutedEventArgs e)
        {
            await HandleJavaDownload(17);
        }

        private async void BtnDownloadJava8_Click(object sender, RoutedEventArgs e)
        {
            await HandleJavaDownload(8);
        }

        private async Task HandleJavaDownload(int majorVersion)
        {
            var versions = _javaService.GetAvailableJavaVersions();
            var target = versions.FirstOrDefault(v => v.MajorVersion == majorVersion);
            if (target != null)
            {
                DownloadProgressBar.Visibility = Visibility.Visible;
                TxtDownloadStatus.Text = $"Descargando e instalando JDK {majorVersion}...";

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
                    DownloadProgressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void BtnInstallPaper_Click(object sender, RoutedEventArgs e)
        {
            await HandleJarDownload(ServerSoftwareType.Paper);
        }

        private async void BtnInstallPurpur_Click(object sender, RoutedEventArgs e)
        {
            await HandleJarDownload(ServerSoftwareType.Purpur);
        }

        private async void BtnInstallVanilla_Click(object sender, RoutedEventArgs e)
        {
            await HandleJarDownload(ServerSoftwareType.Vanilla);
        }

        private async void BtnInstallSpigot_Click(object sender, RoutedEventArgs e)
        {
            await HandleJarDownload(ServerSoftwareType.Spigot);
        }

        private async Task HandleJarDownload(ServerSoftwareType type)
        {
            var presets = _downloadService.GetPresetServerJars();
            var target = presets.FirstOrDefault(j => j.SoftwareType == type);
            if (target != null)
            {
                DownloadProgressBar.Visibility = Visibility.Visible;
                TxtDownloadStatus.Text = $"Descargando {target.FileName}...";

                try
                {
                    string path = await _downloadService.DownloadServerJarAsync(target, _serverDirectory, p =>
                    {
                        DispatcherQueue.TryEnqueue(() => DownloadProgressBar.Value = p);
                    });

                    TxtDownloadStatus.Text = $"¡Server JAR ({target.FileName}) instalado exitosamente!";
                    TxtActiveSoftware.Text = $"JAR: {target.FileName}";
                    RefreshFileList();
                }
                catch (Exception ex)
                {
                    TxtDownloadStatus.Text = $"Error al descargar JAR: {ex.Message}";
                }
                finally
                {
                    DownloadProgressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        #region Visual Configuration

        private void LoadConfigurationToUI()
        {
            _currentProperties = _configService.LoadProperties(_serverDirectory);

            CfgMotd.Text = _currentProperties.GetProperty("motd", "A Minecraft Server");
            CfgMaxPlayers.Text = _currentProperties.GetProperty("max-players", "20");
            CfgServerPort.Text = _currentProperties.GetProperty("server-port", "25565");
            CfgViewDistance.Text = _currentProperties.GetProperty("view-distance", "10");
            CfgSimulationDistance.Text = _currentProperties.GetProperty("simulation-distance", "10");

            SelectComboByContent(CfgGamemode, _currentProperties.GetProperty("gamemode", "survival"));
            SelectComboByContent(CfgDifficulty, _currentProperties.GetProperty("difficulty", "easy"));

            CfgOnlineMode.IsOn = _currentProperties.GetProperty("online-mode", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            CfgPvp.IsOn = _currentProperties.GetProperty("pvp", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            CfgWhitelist.IsOn = _currentProperties.GetProperty("white-list", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            CfgCommandBlocks.IsOn = _currentProperties.GetProperty("enable-command-block", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            CfgAllowNether.IsOn = _currentProperties.GetProperty("allow-nether", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

            ToggleEula.IsOn = _configService.CheckEulaAccepted(_serverDirectory);
            TxtActivePort.Text = $"Puerto: {CfgServerPort.Text}";
        }

        private void SelectComboByContent(ComboBox combo, string val)
        {
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
            _currentProperties.SetProperty("motd", CfgMotd.Text);
            _currentProperties.SetProperty("max-players", CfgMaxPlayers.Text);
            _currentProperties.SetProperty("server-port", CfgServerPort.Text);
            _currentProperties.SetProperty("view-distance", CfgViewDistance.Text);
            _currentProperties.SetProperty("simulation-distance", CfgSimulationDistance.Text);

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
            _currentProperties.SetProperty("white-list", CfgWhitelist.IsOn.ToString().ToLower());
            _currentProperties.SetProperty("enable-command-block", CfgCommandBlocks.IsOn.ToString().ToLower());
            _currentProperties.SetProperty("allow-nether", CfgAllowNether.IsOn.ToString().ToLower());

            _configService.SaveProperties(_serverDirectory, _currentProperties);
            _configService.SetEulaAccepted(_serverDirectory, ToggleEula.IsOn);

            TxtActivePort.Text = $"Puerto: {CfgServerPort.Text}";
            AppendConsoleLog("[Zenith] Configuración guardada en server.properties y eula.txt");
        }

        private void BtnReloadConfig_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigurationToUI();
        }

        private void ToggleEula_Toggled(object sender, RoutedEventArgs e)
        {
            _configService.SetEulaAccepted(_serverDirectory, ToggleEula.IsOn);
        }

        #endregion

        #region File Manager & Integrated Code Editor

        private void RefreshFileList()
        {
            var files = _fileManager.GetDirectoryContents(_serverDirectory);
            LstFileItems.ItemsSource = files;
        }

        private void BtnRefreshFiles_Click(object sender, RoutedEventArgs e)
        {
            RefreshFileList();
        }

        private void LstFileItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
            if (_selectedFileItem != null && !_selectedFileItem.IsDirectory)
            {
                _fileManager.SaveFileText(_selectedFileItem.FullPath, TxtCodeEditor.Text);
                TxtFileEditorStatus.Text = $"¡Guardado exitosamente a las {DateTime.Now:HH:mm:ss}!";
                RefreshFileList();
            }
        }

        private void BtnNewFile_Click(object sender, RoutedEventArgs e)
        {
            string newFileName = $"config_custom_{DateTime.Now:mm ss}.txt";
            string newFilePath = Path.Combine(_serverDirectory, newFileName);
            _fileManager.CreateFile(newFilePath, "# Archivo creado desde Zenith MC Server Launcher\n");
            RefreshFileList();
        }

        private void BtnDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFileItem != null)
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
