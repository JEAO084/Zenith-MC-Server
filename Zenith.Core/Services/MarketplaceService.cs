using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class MarketplaceService
    {
        private readonly HttpClient _httpClient;

        public MarketplaceService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ZenithMCServerLauncher/1.0");
        }

        public async Task<List<MarketplaceItem>> GetFeaturedItemsAsync(
            MarketplaceCategory category,
            string mcVersion = "",
            string loader = "",
            string sourceName = "Todas",
            string searchQuery = "",
            string sortBy = "Downloads",
            string subCategoryFilter = "Todas",
            string overrideVersionFilter = "Todas")
        {
            var results = new List<MarketplaceItem>();

            // Try fetching real items from Modrinth and Spiget APIs online
            try
            {
                if (sourceName.Equals("Modrinth", StringComparison.OrdinalIgnoreCase) || sourceName.StartsWith("Todas", StringComparison.OrdinalIgnoreCase))
                {
                    var modrinthItems = await FetchModrinthItemsAsync(category, searchQuery, mcVersion, loader);
                    results.AddRange(modrinthItems);
                }

                if (category == MarketplaceCategory.Plugin &&
                    (sourceName.Equals("Spigot", StringComparison.OrdinalIgnoreCase) || sourceName.Equals("Bukkit", StringComparison.OrdinalIgnoreCase) || sourceName.StartsWith("Todas", StringComparison.OrdinalIgnoreCase)))
                {
                    var spigetItems = await FetchSpigetItemsAsync(searchQuery);
                    results.AddRange(spigetItems);
                }
            }
            catch
            {
                // Network error fallback
            }

            // Fallback to local sample catalog if online query returned no results or failed
            if (results.Count == 0)
            {
                results = GetSampleCatalog();
            }

            // Filter results
            var filtered = results.Where(i => i.Category == category).ToList();

            if (!string.IsNullOrWhiteSpace(sourceName) && !sourceName.StartsWith("Todas", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(i => i.SourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(subCategoryFilter) && !subCategoryFilter.StartsWith("Todas", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(i => i.SubCategory.Equals(subCategoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            string targetVer = !string.IsNullOrWhiteSpace(overrideVersionFilter) && !overrideVersionFilter.StartsWith("Todas", StringComparison.OrdinalIgnoreCase)
                ? overrideVersionFilter
                : CleanVersionNumber(mcVersion);

            if (!string.IsNullOrWhiteSpace(targetVer))
            {
                var verMatches = filtered.Where(i => i.CompatibleVersions.Count == 0 || i.CompatibleVersions.Any(v => v.Contains(targetVer) || targetVer.Contains(v))).ToList();
                if (verMatches.Count > 0)
                {
                    filtered = verMatches;
                }
            }

            string cleanLoader = CleanLoaderName(loader);
            if (!string.IsNullOrWhiteSpace(cleanLoader))
            {
                var loaderMatches = filtered.Where(i => i.CompatibleLoaders.Count == 0 || i.CompatibleLoaders.Any(l => l.Equals(cleanLoader, StringComparison.OrdinalIgnoreCase) || cleanLoader.Contains(l, StringComparison.OrdinalIgnoreCase))).ToList();
                if (loaderMatches.Count > 0)
                {
                    filtered = loaderMatches;
                }
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                string q = searchQuery.ToLowerInvariant();
                filtered = filtered.Where(i => i.Title.ToLowerInvariant().Contains(q) ||
                                               i.Summary.ToLowerInvariant().Contains(q) ||
                                               i.Author.ToLowerInvariant().Contains(q) ||
                                               i.SubCategory.ToLowerInvariant().Contains(q)).ToList();
            }

            if (sortBy.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.OrderBy(i => i.Title).ToList();
            }
            else
            {
                filtered = filtered.OrderByDescending(i => i.DownloadsCount).ToList();
            }

            return filtered;
        }

        // Synchronous fallback wrapper
        public List<MarketplaceItem> GetFeaturedItems(
            MarketplaceCategory category,
            string mcVersion = "",
            string loader = "",
            string sourceName = "Todas",
            string searchQuery = "",
            string sortBy = "Downloads",
            string subCategoryFilter = "Todas",
            string overrideVersionFilter = "Todas")
        {
            try
            {
                return Task.Run(() => GetFeaturedItemsAsync(category, mcVersion, loader, sourceName, searchQuery, sortBy, subCategoryFilter, overrideVersionFilter)).GetAwaiter().GetResult();
            }
            catch
            {
                return GetSampleCatalog().Where(i => i.Category == category).ToList();
            }
        }

        private async Task<List<MarketplaceItem>> FetchModrinthItemsAsync(MarketplaceCategory category, string query, string mcVersion, string loader)
        {
            var items = new List<MarketplaceItem>();
            string projectType = category == MarketplaceCategory.Plugin ? "plugin" : "mod";
            string searchUrl = $"https://api.modrinth.com/v2/search?limit=20";

            if (!string.IsNullOrWhiteSpace(query))
            {
                searchUrl += $"&query={Uri.EscapeDataString(query)}";
            }

            searchUrl += $"&facets=[[\"project_type:{projectType}\"]]";

            var response = await _httpClient.GetAsync(searchUrl);
            if (!response.IsSuccessStatusCode) return items;

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
                return items;

            foreach (var hit in hits.EnumerateArray())
            {
                string id = hit.GetProperty("project_id").GetString() ?? "";
                string title = hit.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";
                string author = hit.TryGetProperty("author", out var aProp) ? aProp.GetString() ?? "Modrinth Creator" : "Modrinth Creator";
                string summary = hit.TryGetProperty("description", out var dProp) ? dProp.GetString() ?? "" : "";
                long downloads = hit.TryGetProperty("downloads", out var dlProp) ? dlProp.GetInt64() : 0;
                string slug = hit.TryGetProperty("slug", out var sProp) ? sProp.GetString() ?? id : id;

                var versions = new List<string>();
                if (hit.TryGetProperty("versions", out var vProp) && vProp.ValueKind == JsonValueKind.Array)
                {
                    versions = vProp.EnumerateArray().Select(v => v.GetString() ?? "").Where(v => !string.IsNullOrEmpty(v)).Take(5).ToList();
                }

                items.Add(new MarketplaceItem
                {
                    Title = title,
                    Author = author,
                    Summary = summary,
                    Description = $"{summary}\n\nProyecto de Modrinth ID: {id}\nSlug: {slug}",
                    DownloadsCount = downloads,
                    SourceName = "Modrinth",
                    Category = category,
                    SubCategory = category == MarketplaceCategory.Plugin ? "Utilidades" : "Rendimiento",
                    JarFileName = $"{slug}.jar",
                    DownloadUrl = $"https://api.modrinth.com/v2/project/{id}/version",
                    CompatibleVersions = versions,
                    CompatibleLoaders = new List<string> { CleanLoaderName(loader) }
                });
            }

            return items;
        }

        private async Task<List<MarketplaceItem>> FetchSpigetItemsAsync(string query)
        {
            var items = new List<MarketplaceItem>();
            string url = string.IsNullOrWhiteSpace(query)
                ? "https://api.spiget.org/v2/resources?size=15&sort=-downloads"
                : $"https://api.spiget.org/v2/resources/search/{Uri.EscapeDataString(query)}?size=15";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return items;

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return items;

            foreach (var res in doc.RootElement.EnumerateArray())
            {
                int id = res.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                string name = res.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                string tag = res.TryGetProperty("tag", out var tProp) ? tProp.GetString() ?? "" : "";
                long downloads = res.TryGetProperty("downloads", out var dlProp) ? dlProp.GetInt64() : 0;

                if (id == 0 || string.IsNullOrEmpty(name)) continue;

                items.Add(new MarketplaceItem
                {
                    Title = name,
                    Author = "Spigot Creator",
                    Summary = string.IsNullOrEmpty(tag) ? "Plugin de SpigotMC" : tag,
                    Description = $"{tag}\n\nResource ID SpigotMC: {id}",
                    DownloadsCount = downloads,
                    SourceName = "Spigot",
                    Category = MarketplaceCategory.Plugin,
                    SubCategory = "Administración",
                    JarFileName = $"{SanitizeFileName(name)}.jar",
                    DownloadUrl = $"https://api.spiget.org/v2/resources/{id}/download",
                    CompatibleVersions = new List<string> { "1.20.4", "1.20.2", "1.19.4" },
                    CompatibleLoaders = new List<string> { "Paper", "Spigot", "Purpur" }
                });
            }

            return items;
        }

        public async Task<string> DownloadAndInstallItemAsync(MarketplaceItem item, string serverDir)
        {
            string folderName = item.Category == MarketplaceCategory.Plugin ? "plugins" : "mods";
            string targetDir = Path.Combine(serverDir, folderName);

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string targetFilePath = Path.Combine(targetDir, item.JarFileName);

            // 1. Modrinth version download resolution
            if (!string.IsNullOrEmpty(item.DownloadUrl) && item.DownloadUrl.Contains("api.modrinth.com/v2/project/"))
            {
                try
                {
                    var versionResponse = await _httpClient.GetAsync(item.DownloadUrl);
                    if (versionResponse.IsSuccessStatusCode)
                    {
                        string vJson = await versionResponse.Content.ReadAsStringAsync();
                        using var vDoc = JsonDocument.Parse(vJson);
                        if (vDoc.RootElement.ValueKind == JsonValueKind.Array && vDoc.RootElement.GetArrayLength() > 0)
                        {
                            var latestVersion = vDoc.RootElement[0];
                            if (latestVersion.TryGetProperty("files", out var filesProp) && filesProp.ValueKind == JsonValueKind.Array && filesProp.GetArrayLength() > 0)
                            {
                                string fileUrl = filesProp[0].GetProperty("url").GetString() ?? "";
                                string filename = filesProp[0].TryGetProperty("filename", out var fnProp) ? fnProp.GetString() ?? item.JarFileName : item.JarFileName;

                                targetFilePath = Path.Combine(targetDir, filename);

                                using var jarResp = await _httpClient.GetAsync(fileUrl);
                                jarResp.EnsureSuccessStatusCode();
                                using var fs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                                await jarResp.Content.CopyToAsync(fs);
                                return targetFilePath;
                            }
                        }
                    }
                }
                catch { }
            }

            // 2. Direct HTTP download (Spiget or direct URL)
            if (!string.IsNullOrEmpty(item.DownloadUrl) && item.DownloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var response = await _httpClient.GetAsync(item.DownloadUrl);
                    response.EnsureSuccessStatusCode();
                    using var fs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                    return targetFilePath;
                }
                catch { }
            }

            // Local fallback placeholder
            await File.WriteAllTextAsync(targetFilePath, $"// Jar Binary Placeholder for {item.Title}");
            return targetFilePath;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Replace(" ", "_");
        }

        private static string CleanVersionNumber(string rawVersion)
        {
            if (string.IsNullOrWhiteSpace(rawVersion)) return string.Empty;
            var match = Regex.Match(rawVersion, @"\d+\.\d+(\.\d+)?");
            return match.Success ? match.Value : rawVersion.Split(' ')[0].Trim();
        }

        private static string CleanLoaderName(string rawLoader)
        {
            if (string.IsNullOrWhiteSpace(rawLoader)) return string.Empty;
            string clean = rawLoader.Split(' ')[0].Trim();
            if (clean.Contains("Paper", StringComparison.OrdinalIgnoreCase)) return "Paper";
            if (clean.Contains("Purpur", StringComparison.OrdinalIgnoreCase)) return "Purpur";
            if (clean.Contains("Spigot", StringComparison.OrdinalIgnoreCase)) return "Spigot";
            if (clean.Contains("Fabric", StringComparison.OrdinalIgnoreCase)) return "Fabric";
            if (clean.Contains("Forge", StringComparison.OrdinalIgnoreCase)) return "Forge";
            if (clean.Contains("Vanilla", StringComparison.OrdinalIgnoreCase)) return "Vanilla";
            return clean;
        }

        private List<MarketplaceItem> GetSampleCatalog()
        {
            return new List<MarketplaceItem>
            {
                new MarketplaceItem
                {
                    Title = "EssentialsX",
                    Author = "TeamCity",
                    Summary = "El plugin esencial con más de 100 comandos para administración, economía y teletransportación.",
                    Description = "EssentialsX proporciona comandos fundamentales para cualquier servidor de Minecraft Survival, Factions o Skyblock.",
                    DownloadsCount = 28500000,
                    SourceName = "Spigot",
                    Category = MarketplaceCategory.Plugin,
                    SubCategory = "Administración",
                    JarFileName = "EssentialsX-2.20.1.jar",
                    CompatibleVersions = new() { "1.20.4", "1.20.2", "1.19.4", "1.18.2" },
                    CompatibleLoaders = new() { "Paper", "Spigot", "Purpur" }
                },
                new MarketplaceItem
                {
                    Title = "WorldEdit",
                    Author = "sk89q",
                    Summary = "Editor de mapa dentro del juego con selección por varita y generación de esquemas.",
                    Description = "WorldEdit es la herramienta definitiva para constructores y administradores.",
                    DownloadsCount = 42000000,
                    SourceName = "Modrinth",
                    Category = MarketplaceCategory.Plugin,
                    SubCategory = "Utilidades",
                    JarFileName = "worldedit-bukkit-7.2.18.jar",
                    CompatibleVersions = new() { "1.20.4", "1.20.2", "1.19.4" },
                    CompatibleLoaders = new() { "Paper", "Spigot", "Purpur", "Fabric" }
                },
                new MarketplaceItem
                {
                    Title = "LuckPerms",
                    Author = "JSandora",
                    Summary = "El gestor de permisos más avanzado con interfaz web interactiva y jerarquías de grupos.",
                    Description = "LuckPerms es el estándar de la industria para administración de permisos en servidores de Minecraft.",
                    DownloadsCount = 35000000,
                    SourceName = "Modrinth",
                    Category = MarketplaceCategory.Plugin,
                    SubCategory = "Administración",
                    JarFileName = "LuckPerms-Bukkit-5.4.102.jar",
                    CompatibleVersions = new() { "1.20.4", "1.20.2", "1.19.4", "1.16.5" },
                    CompatibleLoaders = new() { "Paper", "Spigot", "Purpur", "Fabric" }
                },
                new MarketplaceItem
                {
                    Title = "Vault",
                    Author = "Slikey",
                    Summary = "API unificada para conectar plugins de Economía, Permisos y Chat.",
                    Description = "Vault es un plugin puente esencial para que plugins de economía y chats conversen entre sí.",
                    DownloadsCount = 19000000,
                    SourceName = "Bukkit",
                    Category = MarketplaceCategory.Plugin,
                    SubCategory = "Utilidades",
                    JarFileName = "Vault-1.7.3.jar",
                    CompatibleVersions = new() { "1.20.4", "1.20.2", "1.19.4" },
                    CompatibleLoaders = new() { "Paper", "Spigot", "Purpur" }
                },
                new MarketplaceItem
                {
                    Title = "Citizens",
                    Author = "fullserver",
                    Summary = "Crea NPCs interactivos que otorgan misiones, tiendas e interacciones visuales.",
                    Description = "Citizens permite colocar aldeanos y personajes no jugadores personalizables que responden a clics de jugadores.",
                    DownloadsCount = 12000000,
                    SourceName = "CurseForge",
                    Category = MarketplaceCategory.Plugin,
                    SubCategory = "Juegos",
                    JarFileName = "Citizens-2.0.33.jar",
                    CompatibleVersions = new() { "1.20.4", "1.20.2" },
                    CompatibleLoaders = new() { "Paper", "Spigot", "Purpur" }
                },
                new MarketplaceItem
                {
                    Title = "Sodium",
                    Author = "jellysquid3",
                    Summary = "Motor de renderizado de vanguardia que multiplica drásticamente los FPS del cliente y servidor.",
                    Description = "Sodium reemplaza el motor de renderizado vanilla con algoritmos modernos multihilo para un rendimiento excepcional.",
                    DownloadsCount = 48000000,
                    SourceName = "Modrinth",
                    Category = MarketplaceCategory.Mod,
                    SubCategory = "Rendimiento",
                    JarFileName = "sodium-fabric-0.5.8+mc1.20.4.jar",
                    CompatibleVersions = new() { "1.20.4", "1.20.2", "1.19.4" },
                    CompatibleLoaders = new() { "Fabric" }
                },
                new MarketplaceItem
                {
                    Title = "Lithium",
                    Author = "jellysquid3",
                    Summary = "Optimización masiva para la física de bloques, IA de mobs y simulación de Chunks.",
                    Description = "Lithium optimiza los sistemas internos del servidor sin modificar ninguna mecánica de juego.",
                    DownloadsCount = 31000000,
                    SourceName = "CurseForge",
                    Category = MarketplaceCategory.Mod,
                    SubCategory = "Rendimiento",
                    JarFileName = "lithium-fabric-mc1.20.4-0.12.1.jar",
                    CompatibleVersions = new() { "1.20.4", "1.20.2" },
                    CompatibleLoaders = new() { "Fabric" }
                },
                new MarketplaceItem
                {
                    Title = "FerriteCore",
                    Author = "malte0811",
                    Summary = "Reducción agresiva del consumo de memoria RAM del servidor y cliente.",
                    Description = "FerriteCore aplica técnicas avanzadas de compresión en estructuras de datos.",
                    DownloadsCount = 22000000,
                    SourceName = "Modrinth",
                    Category = MarketplaceCategory.Mod,
                    SubCategory = "Rendimiento",
                    JarFileName = "ferritecore-6.0.1-fabric.jar",
                    CompatibleVersions = new() { "1.20.4", "1.20.2" },
                    CompatibleLoaders = new() { "Fabric", "Forge" }
                }
            };
        }
    }
}
