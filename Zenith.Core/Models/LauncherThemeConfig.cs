namespace Zenith.Core.Models
{
    public class LauncherThemeConfig
    {
        public string AppThemeMode { get; set; } = "Oscuro"; // Oscuro, Claro, Transparente
        public string AccentColorHex { get; set; } = "#7C3AED";
        public string WindowBackdropType { get; set; } = "AcrylicThin"; // AcrylicThin, Mica, SolidDark
        public string ConsoleFontFamily { get; set; } = "Consolas";
        public string ConsoleBackgroundColorHex { get; set; } = "#140C1D";
        public string ConsoleTextColorHex { get; set; } = "#38BDF8";
        public string EditorBackgroundColorHex { get; set; } = "#1E1428";
        public string EditorTextColorHex { get; set; } = "#E2E8F0";
        public string CardBackgroundColorHex { get; set; } = "#1E1428";
        public string CardBorderColorHex { get; set; } = "#3A2352";
        public int CardCornerRadius { get; set; } = 10;
        public bool EnableGlassMorphism { get; set; } = true;
    }
}
