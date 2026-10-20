namespace Zenith.Core.Models
{
    public class JavaRuntimeInfo
    {
        public int MajorVersion { get; set; } // 8, 17, 21, etc.
        public string Vendor { get; set; } = "Eclipse Temurin";
        public string FullVersion { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string InstallationDirectory { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string Architecture { get; set; } = "x64";
    }
}
