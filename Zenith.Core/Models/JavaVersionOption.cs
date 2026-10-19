namespace Zenith.Core.Models
{
    public class JavaVersionOption
    {
        public int MajorVersion { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Architecture { get; set; } = "x64";
        public string DownloadUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string InstalledPath { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
    }
}
