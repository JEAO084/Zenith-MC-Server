using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Text;
using Windows.System;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public sealed partial class ServerConsolePage : Page
    {
        public ServerConsolePage()
        {
            InitializeComponent();
            Loaded += ServerConsolePage_Loaded;
            Unloaded += ServerConsolePage_Unloaded;
        }

        private void ServerConsolePage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            app.ConsoleManager.LineAdded += ConsoleManager_LineAdded;
            app.ProcessController.StatusChanged += ProcessController_StatusChanged;

            UpdateConsoleDisplay();
            UpdateStatusDisplay(app.ProcessController.CurrentServer?.Status ?? ServerStatus.Stopped);
        }

        private void ServerConsolePage_Unloaded(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            app.ConsoleManager.LineAdded -= ConsoleManager_LineAdded;
            app.ProcessController.StatusChanged -= ProcessController_StatusChanged;
        }

        private void ConsoleManager_LineAdded(string line)
        {
            DispatcherQueue.TryEnqueue(UpdateConsoleDisplay);
        }

        private void ProcessController_StatusChanged(ServerStatus status)
        {
            DispatcherQueue.TryEnqueue(() => UpdateStatusDisplay(status));
        }

        private void UpdateConsoleDisplay()
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var sb = new StringBuilder();
            foreach (var log in app.ConsoleManager.ConsoleLogs)
            {
                sb.AppendLine(log);
            }
            ConsoleOutputTextBox.Text = sb.ToString();
            ConsoleScrollViewer.ChangeView(null, ConsoleScrollViewer.ScrollableHeight, null);
        }

        private void UpdateStatusDisplay(ServerStatus status)
        {
            StatusTextBlock.Text = $"Estado del Servidor: {status}";
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            var server = app.ProcessController.CurrentServer ?? (app.ServerManager.Servers.Count > 0 ? app.ServerManager.Servers[0] : null);
            if (server != null)
            {
                var java = await app.JavaService.EnsureJavaRuntimeAsync(app.JavaService.DetermineRequiredJavaVersion(server.Version));
                await app.ProcessController.StartServerAsync(server, java.ExecutablePath);
            }
        }

        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current != null)
            {
                await MainWindow.Current.ProcessController.StopServerAsync();
            }
        }

        private async void RestartBtn_Click(object sender, RoutedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app != null && app.ProcessController.CurrentServer != null)
            {
                var server = app.ProcessController.CurrentServer;
                var java = await app.JavaService.EnsureJavaRuntimeAsync(app.JavaService.DetermineRequiredJavaVersion(server.Version));
                await app.ProcessController.RestartServerAsync(java.ExecutablePath);
            }
        }

        private void KillBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current != null)
            {
                MainWindow.Current.ProcessController.ForceKillServer();
            }
        }

        private void CommandInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            string text = CommandInputBox.Text;
            var suggestions = app.ConsoleManager.GetAutocompleteSuggestions(text);

            if (suggestions.Count > 0 && !string.IsNullOrWhiteSpace(text))
            {
                AutocompleteListView.ItemsSource = suggestions;
                AutocompleteListView.Visibility = Visibility.Visible;
            }
            else
            {
                AutocompleteListView.Visibility = Visibility.Collapsed;
            }
        }

        private void CommandInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                SendCurrentCommand();
                e.Handled = true;
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendCurrentCommand();
        }

        private void SendCurrentCommand()
        {
            var app = MainWindow.Current;
            if (app == null) return;

            string cmd = CommandInputBox.Text;
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                app.ProcessController.SendCommand(cmd);
                app.ConsoleManager.AppendLine($"> {cmd}");
                CommandInputBox.Text = string.Empty;
                AutocompleteListView.Visibility = Visibility.Collapsed;
            }
        }

        private void AutocompleteListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutocompleteListView.SelectedItem is string selectedCmd)
            {
                CommandInputBox.Text = selectedCmd;
                CommandInputBox.SelectionStart = selectedCmd.Length;
                AutocompleteListView.Visibility = Visibility.Collapsed;
            }
        }
    }
}
