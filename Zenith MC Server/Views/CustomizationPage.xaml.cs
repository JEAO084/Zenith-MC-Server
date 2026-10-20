using Microsoft.UI.Xaml.Controls;

namespace Zenith_MC_Server.Views
{
    public sealed partial class CustomizationPage : Page
    {
        private MainWindow? _mainWindow;

        public CustomizationPage()
        {
            this.InitializeComponent();
        }

        public void Initialize(MainWindow window)
        {
            _mainWindow = window;
        }

        private void CmbAppTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbAppTheme?.SelectedItem is ComboBoxItem item && _mainWindow != null)
            {
                string choice = item.Content.ToString() ?? "";
                _mainWindow.ApplyThemeMode(choice);
            }
        }
    }
}
