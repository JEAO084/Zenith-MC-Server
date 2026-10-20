using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using Windows.UI;
using Zenith.Core.Models;

namespace Zenith_MC_Server.Views
{
    public class AccentColorOption
    {
        public string Name { get; set; } = string.Empty;
        public string Hex { get; set; } = string.Empty;
        public SolidColorBrush Brush { get; set; } = new SolidColorBrush();
    }

    public sealed partial class AppearancePage : Page
    {
        public AppearancePage()
        {
            InitializeComponent();
            Loaded += AppearancePage_Loaded;
        }

        private void AppearancePage_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeRadioButtons.SelectedIndex = 0;
            BackdropComboBox.SelectedIndex = 0;

            var colors = new List<AccentColorOption>
            {
                new AccentColorOption { Name = "Azul Zenith", Hex = "#0078D4", Brush = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212)) },
                new AccentColorOption { Name = "Púrpura Neón", Hex = "#886CE4", Brush = new SolidColorBrush(Color.FromArgb(255, 136, 108, 228)) },
                new AccentColorOption { Name = "Verde Esmeralda", Hex = "#107C41", Brush = new SolidColorBrush(Color.FromArgb(255, 16, 124, 65)) },
                new AccentColorOption { Name = "Rojo Rubí", Hex = "#D13438", Brush = new SolidColorBrush(Color.FromArgb(255, 209, 52, 56)) },
                new AccentColorOption { Name = "Naranja Fuego", Hex = "#FF8C00", Brush = new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)) },
                new AccentColorOption { Name = "Cyan Neón", Hex = "#00B7C3", Brush = new SolidColorBrush(Color.FromArgb(255, 0, 183, 195)) }
            };

            AccentColorGridView.ItemsSource = colors;
            AccentColorGridView.SelectedIndex = 0;
        }

        private void ThemeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            UIThemeMode theme = ThemeRadioButtons.SelectedIndex switch
            {
                1 => UIThemeMode.Light,
                2 => UIThemeMode.Transparent,
                _ => UIThemeMode.Dark
            };

            app.AppearanceService.ApplyAppearance(theme, app.AppearanceService.CurrentBackdrop, app.AppearanceService.AccentColorHex);
        }

        private void BackdropComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            UIBackdropType backdrop = BackdropComboBox.SelectedIndex switch
            {
                1 => UIBackdropType.Mica,
                2 => UIBackdropType.Gradient,
                3 => UIBackdropType.Solid,
                _ => UIBackdropType.DesktopAcrylic
            };

            app.AppearanceService.ApplyAppearance(app.AppearanceService.CurrentTheme, backdrop, app.AppearanceService.AccentColorHex);
        }

        private void AccentColorGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var app = MainWindow.Current;
            if (app == null) return;

            if (AccentColorGridView.SelectedItem is AccentColorOption option)
            {
                app.AppearanceService.ApplyAppearance(app.AppearanceService.CurrentTheme, app.AppearanceService.CurrentBackdrop, option.Hex);
            }
        }
    }
}
