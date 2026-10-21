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

            bool deleteServerFiles = ChkDeleteServerFilesOnReset.IsChecked == true;

            string details = deleteServerFiles
                ? "¿Estás seguro de resetear el launcher y eliminar permanentemente TODOS los archivos de los servidores?"
                : "¿Estás seguro de resetear el launcher? (Los archivos físicos de los servidores NO se eliminarán)";

            var confirmDialog = new ContentDialog
            {
                Title = "⚠️ Restablecimiento de Fábrica",
                Content = details,
                PrimaryButtonText = "Sí, Restablecer",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    if (deleteServerFiles)
                    {
                        var profiles = _mainWindow.ServerManager.GetProfiles();
                        foreach (var profile in profiles)
                        {
                            if (!string.IsNullOrWhiteSpace(profile.DirectoryPath) && Directory.Exists(profile.DirectoryPath))
                            {
                                try
                                {
                                    Directory.Delete(profile.DirectoryPath, true);
                                }
                                catch { }
                            }
                        }
                    }

                    string rootDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZenithMCServer");
                    if (Directory.Exists(rootDataDir))
                    {
                        Directory.Delete(rootDataDir, true);
                    }
                }
                catch { }

                _mainWindow.ServerManager.GetProfiles().Clear();
                _mainWindow.RefreshServerProfilesUI();
                _mainWindow.NavigateToTag("Wizard");
                _mainWindow.AppendConsoleLog("[Zenith Factory Reset] Reseteo de fábrica completado exitosamente.");
            }
        }
    }
}
