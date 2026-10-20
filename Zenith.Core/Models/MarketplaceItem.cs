namespace Zenith.Core.Models
{
    public enum MarketplaceCategory
    {
        Plugin,
        Mod
    }

    public enum MarketplaceSource
    {
        Modrinth,
        Spiget,
        LocalCatalog
    }

    public class MarketplaceItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public MarketplaceCategory Category { get; set; } = MarketplaceCategory.Plugin;
        public MarketplaceSource Source { get; set; } = MarketplaceSource.Modrinth;
        public string IconUrl { get; set; } = string.Empty;
        public long Downloads { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public string InstalledVersion { get; set; } = string.Empty;
    }
}
