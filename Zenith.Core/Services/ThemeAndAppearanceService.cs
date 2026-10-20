using System;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class ThemeAndAppearanceService
    {
        private UIThemeMode _currentTheme = UIThemeMode.Dark;
        private UIBackdropType _currentBackdrop = UIBackdropType.DesktopAcrylic;
        private string _accentColorHex = "#0078D4";

        public event Action? AppearanceChanged;

        public UIThemeMode CurrentTheme => _currentTheme;
        public UIBackdropType CurrentBackdrop => _currentBackdrop;
        public string AccentColorHex => _accentColorHex;

        public double ElementOpacity => _currentTheme == UIThemeMode.Transparent ? 0.25 : 1.0;

        public void ApplyAppearance(UIThemeMode theme, UIBackdropType backdrop, string accentColorHex)
        {
            _currentTheme = theme;
            _currentBackdrop = backdrop;
            if (!string.IsNullOrWhiteSpace(accentColorHex))
            {
                _accentColorHex = accentColorHex;
            }

            AppearanceChanged?.Invoke();
        }
    }
}
