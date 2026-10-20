using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class MarketplaceService
    {
        private readonly HttpClient _httpClient = new();

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
            var allItems = GetSampleCatalog();

            var filtered = allItems.Where(i => i.Category == category).ToList();

            // 1. Filter by Source
            if (!string.IsNullOrWhiteSpace(sourceName) && !sourceName.StartsWith("Todas", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(i => i.SourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // 2. Filter by SubCategory (e.g. Rendimiento, Administración, Utilidades)
            if (!string.IsNullOrWhiteSpace(subCategoryFilter) && !subCategoryFilter.StartsWith("Todas", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(i => i.SubCategory.Equals(subCategoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // 3. Filter by Version (if overrideVersionFilter is specified and not "Todas", use it; otherwise soft match active server version)
            string targetVer = !string.IsNullOrWhiteSpace(overrideVersionFilter) && !overrideVersionFilter.StartsWith("Todas", StringComparison.OrdinalIgnoreCase)
                ? overrideVersionFilter
                : CleanVersionNumber(mcVersion);

            if (!string.IsNullOrWhiteSpace(targetVer))
            {
                var verMatches = filtered.Where(i => i.CompatibleVersions.Count == 0 || i.CompatibleVersions.Any(v => v.Contains(targetVer) || targetVer.Contains(v))).ToList();
                // Soft fallback: if strict version filtering yields 0 results, keep the catalog visible so search isn't empty!
                if (verMatches.Count > 0)
                {
                    filtered = verMatches;
                }
            }

            // 4. Filter by Loader
            string cleanLoader = CleanLoaderName(loader);
            if (!string.IsNullOrWhiteSpace(cleanLoader))
            {
                var loaderMatches = filtered.Where(i => i.CompatibleLoaders.Count == 0 || i.CompatibleLoaders.Any(l => l.Equals(cleanLoader, StringComparison.OrdinalIgnoreCase) || cleanLoader.Contains(l, StringComparison.OrdinalIgnoreCase))).ToList();
                if (loaderMatches.Count > 0)
                {
                    filtered = loaderMatches;
                }
            }

            // 5. Search Query
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                string q = searchQuery.ToLowerInvariant();
                filtered = filtered.Where(i => i.Title.ToLowerInvariant().Contains(q) ||
                                               i.Summary.ToLowerInvariant().Contains(q) ||
                                               i.Author.ToLowerInvariant().Contains(q) ||
                                               i.SubCategory.ToLowerInvariant().Contains(q)).ToList();
            }

            // 6. Sorting
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

        private static string CleanVersionNumber(string rawVersion)
        {
            if (string.IsNullOrWhiteSpace(rawVersion)) return string.Empty;

            // Extract core version numbers e.g. "1.20.4" from "1.20.4 [RELEASE]" or "1.20.4-496"
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

        public async Task<string> DownloadAndInstallItemAsync(MarketplaceItem item, string serverDir)
        {
            string folderName = item.Category == MarketplaceCategory.Plugin ? "plugins" : "mods";
            string targetDir = Path.Combine(serverDir, folderName);

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string targetFilePath = Path.Combine(targetDir, item.JarFileName);

            if (!string.IsNullOrEmpty(item.DownloadUrl) && item.DownloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                using var response = await _httpClient.GetAsync(item.DownloadUrl);
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }
            else
            {
                // Create local jar placeholder
                await File.WriteAllTextAsync(targetFilePath, $"// Jar Binary Placeholder for {item.Title}");
            }

            return targetFilePath;
        }

        private List<MarketplaceItem> GetSampleCatalog()
        {
            return new List<MarketplaceItem>
            {
                // Plugins (Modrinth, Spigot, Bukkit, CurseForge)
                new MarketplaceItem
                {
                    Title = "EssentialsX",
                    Author = "TeamCity",
                    Summary = "El plugin esencial con más de 100 comandos para administración, economía y teletransportación.",
                    Description = "EssentialsX proporciona comandos fundamentales para cualquier servidor de Minecraft Survival, Factions o Skyblock. Incluye sistemas de kits, warps, homes, mensajería privada, economía interna y permisos preconfigurados.",
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
                    Description = "WorldEdit es la herramienta definitiva para constructores y administradores. Permite seleccionar regiones extensas, copiar, pegar, rotar edificaciones, reemplazar bloques en masa y manipular terrenos en tiempo real.",
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
                    Description = "LuckPerms es el estándar de la industria para administración de permisos en servidores de Minecraft. Soporta SQL, YAML, edición visual vía editor web en la nube y sincronización multiseridor Bungee/Velocity.",
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
                    Description = "Vault es un plugin puente esencial para que plugins de economía y chats conversen entre sí sin incompatibilidades de versiones.",
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

                // Mods (Modrinth, CurseForge)
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
                    Description = "Lithium optimiza los sistemas internos del servidor sin modificar ninguna mecánica de juego ni comportamiento vanilla.",
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
                    Description = "FerriteCore aplica técnicas avanzadas de compresión en estructuras de datos de modelos y bloques reduciendo el uso de RAM.",
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
