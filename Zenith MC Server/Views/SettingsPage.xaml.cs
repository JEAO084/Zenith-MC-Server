using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
    }
}
