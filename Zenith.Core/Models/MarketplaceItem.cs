using System;
using System.Collections.Generic;

namespace Zenith.Core.Models
{
    public enum MarketplaceCategory
    {
        Plugin,
        Mod
    }

    public class MarketplaceItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public List<string> Screenshots { get; set; } = new();
        public long DownloadsCount { get; set; }
        public string SourceName { get; set; } = "Modrinth"; // Modrinth, Spigot, Bukkit, CurseForge
        public MarketplaceCategory Category { get; set; } = MarketplaceCategory.Plugin;
        public string DownloadUrl { get; set; } = string.Empty;
        public string JarFileName { get; set; } = string.Empty;
        public List<string> CompatibleVersions { get; set; } = new();
        public List<string> CompatibleLoaders { get; set; } = new();
        public string FormattedDownloads => DownloadsCount >= 1000000 ? $"{DownloadsCount / 1000000.0:F1}M" : DownloadsCount >= 1000 ? $"{DownloadsCount / 1000.0:F1}k" : DownloadsCount.ToString();
    }
}
