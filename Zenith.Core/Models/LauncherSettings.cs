namespace Zenith.Core.Models
{
    public enum UIThemeMode
    {
        Light,
        Dark,
        Transparent
    }

    public enum UIBackdropType
    {
        Solid,
        Gradient,
        Mica,
        DesktopAcrylic
    }

    public class LauncherSettings
    {
        public string DefaultServersDirectory { get; set; } = string.Empty;
        public string DefaultJavaRuntimesDirectory { get; set; } = string.Empty;
        public int MaxCpuThreads { get; set; } = 4;
        public int RamAlertThresholdMb { get; set; } = 8192;
        public UIThemeMode Theme { get; set; } = UIThemeMode.Dark;
        public UIBackdropType Backdrop { get; set; } = UIBackdropType.DesktopAcrylic;
        public string AccentColorHex { get; set; } = "#0078D4";
        public bool AutoCheckUpdates { get; set; } = true;
        public bool AutoBackupBeforeUpdate { get; set; } = true;
    }
}
