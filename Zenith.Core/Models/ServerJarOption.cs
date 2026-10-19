namespace Zenith.Core.Models
{
    public enum ServerSoftwareType
    {
        Paper,
        Spigot,
        Vanilla,
        Purpur
    }

    public class ServerJarOption
    {
        public ServerSoftwareType SoftwareType { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Build { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string RecommendedJavaVersion { get; set; } = "Java 21";
        public string FileName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
