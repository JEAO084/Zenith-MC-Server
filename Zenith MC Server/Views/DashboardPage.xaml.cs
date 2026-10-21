using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.Generic;
using System.Linq;

namespace Zenith_MC_Server.Views
{
    public sealed partial class DashboardPage : Page
    {
        private MainWindow? _mainWindow;
        private readonly List<string> _knownMinecraftCommands = new()
        {
            "gamemode survival", "gamemode creative", "gamemode adventure", "gamemode spectator",
            "gamerule doDaylightCycle false", "gamerule doMobSpawning false", "gamerule keepInventory true",
            "op", "deop", "teleport", "tp", "give", "kick", "ban", "pardon", "stop", "reload", "save-all", "whitelist add", "whitelist remove", "list", "say"
        };

        public DashboardPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
        }

        private void BtnStartServer_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.StartActiveServer();
        }

        private void BtnStopServer_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.StopActiveServer();
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

            var matches = _knownMinecraftCommands.Where(c => c.StartsWith(query, System.StringComparison.OrdinalIgnoreCase)).ToList();
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
            if (!string.IsNullOrEmpty(cmd))
            {
                _mainWindow?.SendServerCommand(cmd);
                TxtConsoleInput.Text = string.Empty;
                CmdSuggestionContainer.Visibility = Visibility.Collapsed;
            }
        }

        public void AppendConsoleLog(string message)
        {
            TxtConsoleLog.Text += $"{message}\n";
            ConsoleScrollViewer.ChangeView(null, ConsoleScrollViewer.ScrollableHeight, null);
        }

        public void UpdateStatusIndicator(bool isRunning)
        {
            BtnStartServer.IsEnabled = !isRunning;
            BtnStopServer.IsEnabled = isRunning;
        }

        public void UpdateActiveServerInfo(string name, string software, string port)
        {
            TxtActiveServerName.Text = $"Servidor: {name}";
            TxtActiveSoftware.Text = $"Motor: {software}";
            TxtActivePort.Text = $"Puerto: {port}";
        }
    }
}
