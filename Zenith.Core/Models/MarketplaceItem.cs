using System.Collections.Generic;

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

    public enum MarketplaceSortOption
    {
        Downloads,
        Relevance,
        Recent
    }

    public class MarketplaceFilterOptions
    {
        public string Query { get; set; } = string.Empty;
        public MarketplaceCategory Category { get; set; } = MarketplaceCategory.Plugin;
        public string SelectedLoader { get; set; } = "Todos"; // Todos, Paper, Purpur, Spigot, Fabric, Forge, NeoForge
        public string SelectedGameVersion { get; set; } = "Todas"; // Todas, 1.20.4, 1.20.1, 1.19.4, etc.
        public MarketplaceSortOption SortBy { get; set; } = MarketplaceSortOption.Downloads;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 8;
    }

    public class MarketplaceSearchResult
    {
        public List<MarketplaceItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }

    public class MarketplaceItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string DetailedDescription { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public MarketplaceCategory Category { get; set; } = MarketplaceCategory.Plugin;
        public MarketplaceSource Source { get; set; } = MarketplaceSource.Modrinth;
        public string IconUrl { get; set; } = string.Empty;
        public long Downloads { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ProjectUrl { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public string InstalledVersion { get; set; } = string.Empty;
        public List<string> SupportedLoaders { get; set; } = new();
        public List<string> SupportedGameVersions { get; set; } = new();
        public List<string> GalleryImageUrls { get; set; } = new();
    }
}
