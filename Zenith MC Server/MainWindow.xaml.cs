using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            this.SetTitleBar(AppTitleBar);

            ContentFrame.Navigated += ContentFrame_Navigated;

            _processManager.OutputReceived += OnServerOutputReceived;
            _processManager.ProcessExited += OnServerProcessExited;

            RefreshServerProfilesUI();
            UpdateStatusIndicator(false);

            NavigateToTag("Dashboard");
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
        }

        public void StartActiveServer()
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
                    AppendConsoleLog("[Zenith] Error: No se encontró ningún archivo server.jar en la carpeta del servidor activo.");
                    return;
                }

                string jarPath = Path.GetFileName(jarFiles[0]);
                int ramGb = profile.AllocatedRamGb;

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
                if (StatusIndicatorDot != null) StatusIndicatorDot.Fill = new SolidColorBrush(isRunning ? Color.FromArgb(255, 34, 197, 94) : Color.FromArgb(255, 239, 68, 68));
                if (StatusIndicatorText != null) StatusIndicatorText.Text = isRunning ? "Servidor En Ejecución" : "Servidor Detenido";

                if (ContentFrame.Content is DashboardPage dashPage)
                {
                    dashPage.UpdateStatusIndicator(isRunning);
                }
            });
        }

        public void ApplyThemeMode(string choice)
        {
            if (choice.Contains("Claro"))
            {
                if (Content is FrameworkElement rootElement) rootElement.RequestedTheme = ElementTheme.Light;
                this.SystemBackdrop = null;
                if (RootGrid != null) RootGrid.Background = new SolidColorBrush(Color.FromArgb(255, 243, 244, 246));
                if (AppTitleBar != null) AppTitleBar.Background = new SolidColorBrush(Color.FromArgb(255, 229, 231, 235));
                if (TxtAppTitle != null) TxtAppTitle.Foreground = new SolidColorBrush(Color.FromArgb(255, 17, 24, 39));
                if (TxtActiveServerLabel != null) TxtActiveServerLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 55, 65, 81));
            }
            else if (choice.Contains("Transparente"))
            {
                if (Content is FrameworkElement rootElement) rootElement.RequestedTheme = ElementTheme.Dark;
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
                if (RootGrid != null) RootGrid.Background = new SolidColorBrush(Color.FromArgb(64, 17, 11, 24));
                if (AppTitleBar != null) AppTitleBar.Background = new SolidColorBrush(Color.FromArgb(64, 26, 16, 36));
                if (TxtAppTitle != null) TxtAppTitle.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                if (TxtActiveServerLabel != null) TxtActiveServerLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204));
            }
            else
            {
                if (Content is FrameworkElement rootElement) rootElement.RequestedTheme = ElementTheme.Dark;
                this.SystemBackdrop = null;
                if (RootGrid != null) RootGrid.Background = new SolidColorBrush(Color.FromArgb(255, 17, 11, 24));
                if (AppTitleBar != null) AppTitleBar.Background = new SolidColorBrush(Color.FromArgb(255, 26, 16, 36));
                if (TxtAppTitle != null) TxtAppTitle.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                if (TxtActiveServerLabel != null) TxtActiveServerLabel.Foreground = new SolidColorBrush(Color.FromArgb(255, 204, 204, 204));
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
            else if (e.Content is MarketplacePage marketPage)
            {
                marketPage.Initialize(this);
            }
            else if (e.Content is FileManagerPage filePage)
            {
                filePage.Initialize(this);
            }
            else if (e.Content is JavaPage javaPage)
            {
                javaPage.Initialize(this);
            }
            else if (e.Content is CustomizationPage customPage)
            {
                customPage.Initialize(this);
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

        private void BtnNavigateBack_Click(object sender, RoutedEventArgs e)
        {
            NavigateBack();
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
            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString() ?? "";
                NavigateToTag(tag);
            }
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
                    break;
                case "Config":
                    ContentFrame.Navigate(typeof(ServerConfigPage));
                    break;
                case "Marketplace":
                    ContentFrame.Navigate(typeof(MarketplacePage));
                    break;
                case "FileManager":
                    ContentFrame.Navigate(typeof(FileManagerPage));
                    break;
                case "JavaRuntimes":
                    ContentFrame.Navigate(typeof(JavaPage));
                    break;
                case "Customization":
                    ContentFrame.Navigate(typeof(CustomizationPage));
                    break;
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
    }
}
