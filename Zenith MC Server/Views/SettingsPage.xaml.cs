using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

namespace Zenith_MC_Server.Views
{
    public sealed partial class SettingsPage : Page
    {
        private MainWindow? _mainWindow;

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
            if (_mainWindow?.ActiveProfile != null && TxtExternalEditorExecutablePath != null)
            {
                TxtExternalEditorExecutablePath.Text = _mainWindow.ActiveProfile.ExternalEditorPath;
            }
        }

        private void CmbAppTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbAppTheme?.SelectedItem is ComboBoxItem item && _mainWindow != null)
            {
                string choice = item.Content.ToString() ?? "";
                _mainWindow.ApplyThemeMode(choice);
            }
        }

        private void BtnSaveExternalEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow?.ActiveProfile != null && TxtExternalEditorExecutablePath != null)
            {
                _mainWindow.ActiveProfile.ExternalEditorPath = TxtExternalEditorExecutablePath.Text.Trim();
                _mainWindow.ServerManager.UpdateProfile(_mainWindow.ActiveProfile);
                _mainWindow.AppendConsoleLog("[Zenith] Editor de texto externo actualizado.");
            }
        }

        private async void BtnFullFactoryReset_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindow == null) return;

            var confirmDialog = new ContentDialog
            {
                Title = "⚠️ Reseteo Completo del Launcher",
                Content = "¿Estás completamente seguro de que deseas borrar TODOS los servidores, ajustes e información del launcher?\n\nEsta acción reiniciará el launcher a su estado original de fábrica.",
                PrimaryButtonText = "Sí, Borrar Todo",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    string rootDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZenithMCServer");
                    if (Directory.Exists(rootDataDir))
                    {
                        Directory.Delete(rootDataDir, true);
                    }
                }
                catch { }

                _mainWindow.RefreshServerProfilesUI();
                _mainWindow.NavigateToTag("Wizard");
                _mainWindow.AppendConsoleLog("[Zenith Factory Reset] Reseteo completo finalizado exitosamente.");
            }
        }
    }
}
