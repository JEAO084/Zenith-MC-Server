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
        private bool _isServerRunning = false;

        public DashboardPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
        }

        private void BtnToggleServer_Click(object sender, RoutedEventArgs e)
        {
            if (_isServerRunning)
            {
                _mainWindow?.StopActiveServer();
            }
            else
            {
                _mainWindow?.StartActiveServer();
            }
        }

        private void BtnSendCommand_Click(object sender, RoutedEventArgs e)
        {
            SendConsoleInput();
        }

        private void CmdAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && _mainWindow != null)
            {
                string query = sender.Text;
                var suggestions = _mainWindow.ProcessManager.GetDynamicSuggestions(query);
                sender.ItemsSource = suggestions;
            }
        }

        private void CmdAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is string chosen)
            {
                sender.Text = chosen;
            }
        }

        private void CmdAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string cmd = !string.IsNullOrWhiteSpace(args.QueryText) ? args.QueryText : sender.Text;
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                _mainWindow?.SendServerCommand(cmd.Trim());
                sender.Text = string.Empty;
            }
        }

        private void SendConsoleInput()
        {
            string cmd = CmdAutoSuggestBox.Text.Trim();
            if (!string.IsNullOrEmpty(cmd))
            {
                _mainWindow?.SendServerCommand(cmd);
                CmdAutoSuggestBox.Text = string.Empty;
            }
        }

        public void AppendConsoleLog(string message)
        {
            TxtConsoleLog.Text += $"{message}\n";
            ConsoleScrollViewer.ChangeView(null, ConsoleScrollViewer.ScrollableHeight, null);
        }

        public void UpdateStatusIndicator(bool isRunning)
        {
            _isServerRunning = isRunning;
            if (BtnToggleServer != null)
            {
                BtnToggleServer.Content = isRunning ? "⏹ Detener Servidor" : "▶ Iniciar Servidor";
            }
        }

        public void UpdateActiveServerInfo(string name, string software, string port)
        {
            TxtActiveServerName.Text = $"Servidor: {name}";
            TxtActiveSoftware.Text = $"Motor: {software}";
            TxtActivePort.Text = $"Puerto: {port}";
        }
    }
}
