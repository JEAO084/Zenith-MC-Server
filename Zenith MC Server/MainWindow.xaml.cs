using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using Windows.UI;
using Zenith.Core.Models;
using Zenith.Core.Services;
using Zenith_MC_Server.Views;

namespace Zenith_MC_Server
{
    public sealed partial class MainWindow : Window
    {
        public static new MainWindow? Current { get; private set; }

        public ServerManager ServerManager { get; } = new();
        public EngineService EngineService { get; } = new();
        public MarketplaceService MarketplaceService { get; } = new();
        public JavaManagementService JavaService { get; } = new();
        public ServerProcessController ProcessController { get; } = new();
        public LiveConsoleManager ConsoleManager { get; } = new();
        public FileExplorerService FileExplorerService { get; } = new();
        public IntegratedEditorService EditorService { get; } = new();
        public ConfigurationEditorService ConfigEditorService { get; } = new();
        public ThemeAndAppearanceService AppearanceService { get; } = new();
        public LauncherSettingsService SettingsService { get; } = new();
        public BackupService BackupService { get; } = new();
        public UpdateService UpdateService { get; } = new();

        public ServerInstance? ActiveServer { get; private set; }

        public MainWindow()
        {
            Current = this;
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            RootGrid.PointerPressed += RootGrid_PointerPressed;

            // Subscribe to process output
            ProcessController.OutputDataReceived += line => ConsoleManager.AppendLine(line);
            ProcessController.ErrorDataReceived += line => ConsoleManager.AppendLine(line);

            // Subscribe to appearance changes
            AppearanceService.AppearanceChanged += AppearanceService_AppearanceChanged;

            // Populate Quick Active Server Selector
            RefreshActiveServerComboBox();

            // Navigate to default home page
            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(DashboardPage));
        }

        public void RefreshActiveServerComboBox()
        {
            var servers = ServerManager.Servers.ToList();
            ServerSelectComboBox.ItemsSource = servers;

            if (servers.Count > 0)
            {
                if (ActiveServer == null || !servers.Any(s => s.Id == ActiveServer.Id))
                {
                    ActiveServer = servers[0];
                }
                ServerSelectComboBox.SelectedItem = ActiveServer;
            }
            else
            {
                ActiveServer = null;
                ServerSelectComboBox.SelectedItem = null;
            }
        }

        private void ServerSelectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerSelectComboBox.SelectedItem is ServerInstance selectedServer)
            {
                ActiveServer = selectedServer;
            }
        }

        public ServerInstance? GetActiveServer()
        {
            if (ActiveServer != null && ServerManager.Servers.Any(s => s.Id == ActiveServer.Id))
            {
                return ActiveServer;
            }

            if (ServerManager.Servers.Count > 0)
            {
                ActiveServer = ServerManager.Servers[0];
                return ActiveServer;
            }

            return null;
        }

        public void NavigateTo(Type pageType, object? parameter = null)
        {
            if (ContentFrame.CurrentSourcePageType != pageType || parameter != null)
            {
                ContentFrame.Navigate(pageType, parameter);
            }
        }

        private void AppearanceService_AppearanceChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Update Backdrop
                SystemBackdrop = AppearanceService.CurrentBackdrop switch
                {
                    UIBackdropType.Mica => new MicaBackdrop(),
                    UIBackdropType.DesktopAcrylic => new DesktopAcrylicBackdrop(),
                    _ => new DesktopAcrylicBackdrop()
                };

                // Update Theme & Opacity
                if (Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = AppearanceService.CurrentTheme switch
                    {
                        UIThemeMode.Light => ElementTheme.Light,
                        UIThemeMode.Transparent => ElementTheme.Dark,
                        _ => ElementTheme.Dark
                    };

                    rootElement.Opacity = AppearanceService.ElementOpacity;
                }

                // Apply Accent Color
                if (!string.IsNullOrEmpty(AppearanceService.AccentColorHex) && AppearanceService.AccentColorHex.Length >= 7)
                {
                    try
                    {
                        byte r = Convert.ToByte(AppearanceService.AccentColorHex.Substring(1, 2), 16);
                        byte g = Convert.ToByte(AppearanceService.AccentColorHex.Substring(3, 2), 16);
                        byte b = Convert.ToByte(AppearanceService.AccentColorHex.Substring(5, 2), 16);
                        var color = Color.FromArgb(255, r, g, b);

                        Application.Current.Resources["SystemControlHighlightAccentBrush"] = new SolidColorBrush(color);
                        Application.Current.Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(color);
                    }
                    catch { }
                }
            });
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(RootGrid).Properties;
            if (properties.IsXButton1Pressed)
            {
                if (ContentFrame.CanGoBack)
                {
                    ContentFrame.GoBack();
                    e.Handled = true;
                }
            }
            else if (properties.IsXButton2Pressed)
            {
                if (ContentFrame.CanGoForward)
                {
                    ContentFrame.GoForward();
                    e.Handled = true;
                }
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                Type pageType = tag switch
                {
                    "Dashboard" => typeof(DashboardPage),
                    "Wizard" => typeof(ServerWizardPage),
                    "Console" => typeof(ServerConsolePage),
                    "Marketplace" => typeof(MarketplacePage),
                    "FileExplorer" => typeof(FileExplorerPage),
                    "ConfigEditor" => typeof(ConfigEditorPage),
                    "Appearance" => typeof(AppearancePage),
                    "Settings" => typeof(SettingsPage),
                    _ => typeof(DashboardPage)
                };

                NavigateTo(pageType);
            }
        }
    }
}
