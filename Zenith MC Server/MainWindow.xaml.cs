using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.UI;
using Zenith.Core.Models;
using Zenith.Core.Services;
using Zenith_MC_Server.Views;

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

        private readonly Stack<string> _navigationHistory = new();
        private bool _isServerRunning = false;
        private ServerProfile? _pendingDeleteProfile;

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

            this.InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(TitleBar);
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            if (AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }


            ContentFrame.Navigated += ContentFrame_Navigated;

            _processManager.OutputReceived += OnServerOutputReceived;
            _processManager.ProcessExited += OnServerProcessExited;

            RefreshServerProfilesUI();
            UpdateStatusIndicator(false);
            UpdateNavViewVisibility();

            // Default Desktop Acrylic Theme
            this.SystemBackdrop = new DesktopAcrylicBackdrop();
            ApplyThemeMode("Oscuro");

            // First-run detection: If no server profiles exist, navigate to Wizard Page automatically
            if (_serverManager.GetProfiles().Count == 0)
            {
                NavigateToTag("Wizard");
            }
            else
            {
                NavigateToTag("Dashboard");
            }
        }

        #region Service Accessors & Helper Methods

        public ServerManagerService ServerManager => _serverManager;
        public JavaInstallerService JavaService => _javaService;
        public ServerDownloadService DownloadService => _downloadService;
        public ServerConfigService ConfigService => _configService;
        public FileManagerService FileManager => _fileManager;
        public ServerProcessManager ProcessManager => _processManager;
        public PluginModManagerService PluginModManager => _pluginModManager;
        public MarketplaceService MarketplaceService => _marketplaceService;
        public ServerProfile? ActiveProfile => _serverManager.ActiveProfile;

        public void RefreshServerProfilesUI()
        {
            if (_serverManager == null || CmbServerSelector == null) return;
            var profiles = _serverManager.GetProfiles();
            CmbServerSelector.ItemsSource = profiles;
            if (_serverManager.ActiveProfile != null)
            {
                CmbServerSelector.SelectedItem = _serverManager.ActiveProfile;
            }
            if (LstServerProfilesDialog != null)
            {
                LstServerProfilesDialog.ItemsSource = profiles;
            }
            UpdateNavViewVisibility();
        }

        public void UpdateNavViewVisibility()
        {
            if (NavItemPlugins == null || NavItemMods == null) return;

            var profile = ActiveProfile;
            if (profile == null)
            {
                NavItemPlugins.Visibility = Visibility.Visible;
                NavItemMods.Visibility = Visibility.Visible;
                return;
            }

            string soft = profile.SoftwareType.ToLowerInvariant();
            bool supportsPlugins = soft.Contains("paper") || soft.Contains("spigot") || soft.Contains("purpur") || soft.Contains("bukkit") || soft.Contains("mohist") || soft.Contains("ar権") || soft.Contains("magma");
            bool supportsMods = soft.Contains("fabric") || soft.Contains("forge") || soft.Contains("neoforge") || soft.Contains("quilt") || soft.Contains("mohist") || soft.Contains("ar権") || soft.Contains("magma");

            if (!supportsPlugins && !supportsMods)
            {
                supportsPlugins = true;
                supportsMods = true;
            }

            NavItemPlugins.Visibility = supportsPlugins ? Visibility.Visible : Visibility.Collapsed;
            NavItemMods.Visibility = supportsMods ? Visibility.Visible : Visibility.Collapsed;
        }

        public async void StartActiveServer()
        {
            var profile = ActiveProfile;
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
                    AppendConsoleLog("[Zenith] Descargando motor de servidor...");
                    string downloadedJar = await _downloadService.DownloadServerJarForVersionAsync(profile.SoftwareType, profile.Version, serverDir);
                    profile.JarFileName = Path.GetFileName(downloadedJar);
                    _serverManager.UpdateProfile(profile);
                    jarFiles = Directory.GetFiles(serverDir, "*.jar");
                }

                if (jarFiles.Length == 0)
                {
                    AppendConsoleLog("[Zenith Error] No se encontró ni pudo descargarse un archivo server.jar.");
                    return;
                }

                string jarPath = Path.GetFileName(jarFiles[0]);
                int ramGb = profile.AllocatedRamGb;

                string javaPath = profile.JavaExecutablePath;
                if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
                {
                    int reqJava = _downloadService.GetRecommendedJavaVersion(profile.Version);
                    var javaOptions = _javaService.GetAvailableJavaVersions();
                    var targetOption = javaOptions.FirstOrDefault(j => j.MajorVersion == reqJava) ?? javaOptions.FirstOrDefault();
                    if (targetOption != null)
                    {
                        if (!File.Exists(targetOption.InstalledPath))
                        {
                            AppendConsoleLog($"[Zenith] Descargando entorno Java {targetOption.MajorVersion} interno...");
                            javaPath = await _javaService.DownloadAndExtractJavaAsync(targetOption);
                        }
                        else
                        {
                            javaPath = targetOption.InstalledPath;
                        }
                        profile.JavaExecutablePath = javaPath;
                        _serverManager.UpdateProfile(profile);
                    }
                }

                if (string.IsNullOrWhiteSpace(javaPath) || !File.Exists(javaPath))
                {
                    javaPath = "java";
                }

                AppendConsoleLog($"[Zenith] Iniciando servidor '{profile.Name}' ({jarPath}) con {ramGb}GB RAM usando Java: {javaPath}...");
                bool started = _processManager.StartServer(javaPath, serverDir, jarPath, ramGb, profile.CustomJvmArgs);
                if (started)
                {
                    _isServerRunning = true;
                    UpdateStatusIndicator(true);
                }
            }
            catch (Exception ex)
            {
                AppendConsoleLog($"[Zenith Error] {ex.Message}");
            }
        }

        public async void StopActiveServer()
        {
            AppendConsoleLog("[Zenith] Enviando comando 'stop' al servidor...");
            await _processManager.StopServerAsync();
            _isServerRunning = false;
            UpdateStatusIndicator(false);
        }

        public void SendServerCommand(string command)
        {
            if (!string.IsNullOrWhiteSpace(command))
            {
                _processManager.SendCommand(command);
            }
        }

        public void AppendConsoleLog(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ContentFrame.Content is DashboardPage dashPage)
                {
                    dashPage.AppendConsoleLog(message);
                }
            });
        }

        public void UpdateStatusIndicator(bool isRunning)
        {
            _isServerRunning = isRunning;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (StatusIndicatorDot != null) StatusIndicatorDot.Fill = new SolidColorBrush(isRunning ? Color.FromArgb(255, 240, 240, 240) : Color.FromArgb(255, 120, 120, 120));
                if (StatusIndicatorText != null) StatusIndicatorText.Text = isRunning ? "Servidor En Ejecución" : "Servidor Detenido";

                if (ContentFrame.Content is DashboardPage dashPage)
                {
                    dashPage.UpdateStatusIndicator(isRunning);
                }
            });
        }

        public void ApplyThemeMode(string choice)
        {
            this.SystemBackdrop = new DesktopAcrylicBackdrop();

            if (choice.Contains("Claro"))
            {
                if (Content is FrameworkElement rootElement) rootElement.RequestedTheme = ElementTheme.Light;

                if (RootGrid != null) RootGrid.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                if (AppWindow.TitleBar != null) AppWindow.TitleBar.ButtonForegroundColor = ColorHelper.FromArgb(255, 0, 0, 0);

            }
            else
            {
                if (Content is FrameworkElement rootElement) rootElement.RequestedTheme = ElementTheme.Dark;

                if (RootGrid != null) RootGrid.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                if (AppWindow.TitleBar != null) AppWindow.TitleBar.ButtonForegroundColor = ColorHelper.FromArgb(255, 255, 255, 255);
            }
        }

        #endregion

        #region Navigation and Events

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is DashboardPage dashPage)
            {
                dashPage.Initialize(this);
                dashPage.UpdateStatusIndicator(_isServerRunning);
                if (ActiveProfile != null)
                {
                    dashPage.UpdateActiveServerInfo(ActiveProfile.Name, $"{ActiveProfile.SoftwareType} {ActiveProfile.Version}", "25565");
                }
            }
            else if (e.Content is WizardPage wizardPage)
            {
                wizardPage.Initialize(this);
            }
            else if (e.Content is ServerConfigPage configPage)
            {
                configPage.Initialize(this);
            }
            else if (e.Content is PluginsPage pluginsPage)
            {
                pluginsPage.Initialize(this);
            }
            else if (e.Content is ModsPage modsPage)
            {
                modsPage.Initialize(this);
            }
            else if (e.Content is ModPluginDetailsPage detailsPage)
            {
                detailsPage.Initialize(this);
            }
            else if (e.Content is FileManagerPage filePage)
            {
                filePage.Initialize(this);
            }
            else if (e.Content is SettingsPage settingsPage)
            {
                settingsPage.Initialize(this);
            }
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(sender as UIElement);
            if (point.Properties.IsXButton1Pressed)
            {
                NavigateBack();
                e.Handled = true;
            }
        }

        private async void BtnManageServers_Click(object sender, RoutedEventArgs e)
        {
            if (ServerManagerDialog != null)
            {
                ServerManagerDialog.XamlRoot = RootGrid.XamlRoot;
                if (InlineDeleteConfirmBorder != null) InlineDeleteConfirmBorder.Visibility = Visibility.Collapsed;
                RefreshServerProfilesUI();
                await ServerManagerDialog.ShowAsync();
            }
        }

        private void LstServerProfilesDialog_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ServerProfile profile)
            {
                _serverManager.SelectProfile(profile.Id);
                RefreshServerProfilesUI();
                if (ServerManagerDialog != null)
                {
                    ServerManagerDialog.Hide();
                }
                if (ContentFrame.Content is DashboardPage dashPage)
                {
                    dashPage.UpdateActiveServerInfo(profile.Name, $"{profile.SoftwareType} {profile.Version}", "25565");
                }
            }
        }

        private void BtnDeleteServerProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ServerProfile profile)
            {
                _pendingDeleteProfile = profile;
                if (TxtInlineDeleteConfirmText != null)
                {
                    TxtInlineDeleteConfirmText.Text = $"¿Estás seguro de eliminar permanentemente el servidor '{profile.Name}'?";
                }
                if (InlineDeleteConfirmBorder != null)
                {
                    InlineDeleteConfirmBorder.Visibility = Visibility.Visible;
                }
            }
        }

        private void BtnCancelInlineDelete_Click(object sender, RoutedEventArgs e)
        {
            _pendingDeleteProfile = null;
            if (InlineDeleteConfirmBorder != null)
            {
                InlineDeleteConfirmBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnConfirmInlineDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingDeleteProfile != null)
            {
                _serverManager.DeleteProfile(_pendingDeleteProfile.Id);
                _pendingDeleteProfile = null;
                if (InlineDeleteConfirmBorder != null)
                {
                    InlineDeleteConfirmBorder.Visibility = Visibility.Collapsed;
                }
                RefreshServerProfilesUI();
            }
        }

        private void NavigateBack()
        {
            if (_navigationHistory.Count > 1)
            {
                _navigationHistory.Pop();
                string previousTag = _navigationHistory.Peek();
                NavigateToTag(previousTag, recordHistory: false);
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavigateToTag("Settings");
            }
            else if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString() ?? "";
                NavigateToTag(tag);
            }
        }

        public void OpenModPluginDetails(MarketplaceItem item)
        {
            ContentFrame.Navigate(typeof(ModPluginDetailsPage), item);
        }

        public void NavigateToTag(string tag, bool recordHistory = true)
        {
            if (recordHistory && (_navigationHistory.Count == 0 || _navigationHistory.Peek() != tag))
            {
                _navigationHistory.Push(tag);
            }

            switch (tag)
            {
                case "Dashboard":
                    ContentFrame.Navigate(typeof(DashboardPage));
                    break;
                case "Wizard":
                    ContentFrame.Navigate(typeof(WizardPage));
                    NavView.SelectedItem = null;
                    break;
                case "Config":
                    ContentFrame.Navigate(typeof(ServerConfigPage));
                    break;
                case "Plugins":
                    ContentFrame.Navigate(typeof(PluginsPage));
                    break;
                case "Mods":
                    ContentFrame.Navigate(typeof(ModsPage));
                    break;
                case "FileManager":
                    ContentFrame.Navigate(typeof(FileManagerPage));
                    break;
                case "JavaRuntimes":
                case "Settings":
                case "LauncherSettings":
                    ContentFrame.Navigate(typeof(SettingsPage));
                    break;
                default:
                    ContentFrame.Navigate(typeof(DashboardPage));
                    break;
            }
        }

        private void CmbServerSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_serverManager != null && CmbServerSelector?.SelectedItem is ServerProfile profile)
            {
                _serverManager.SelectProfile(profile.Id);
                if (ContentFrame.Content is DashboardPage dashPage)
                {
                    dashPage.UpdateActiveServerInfo(profile.Name, $"{profile.SoftwareType} {profile.Version}", "25565");
                }
            }
        }

        private void BtnOpenWizard_Click(object sender, RoutedEventArgs e)
        {
            NavigateToTag("Wizard");
        }

        private void OnServerOutputReceived(string line)
        {
            AppendConsoleLog(line);
        }

        private void OnServerProcessExited(int exitCode)
        {
            UpdateStatusIndicator(false);
            AppendConsoleLog($"[Zenith] El servidor se ha detenido con código de salida: {exitCode}");
        }

        #endregion

        private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
        {
            NavView.IsPaneOpen = !NavView.IsPaneOpen;
        }

        private void TitleBar_BackRequested(TitleBar sender, object args)
        {
            NavigateBack();
        }
    }
}
