using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class MinecraftVersionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "release"; // release, snapshot, old_alpha, old_beta
        public string Url { get; set; } = string.Empty;
        public DateTime ReleaseTime { get; set; }
        public int RecommendedJavaVersion { get; set; } = 21;
    }

    public class ServerDownloadService
    {
        private readonly HttpClient _httpClient = new();

        public int GetRecommendedJavaVersion(string mcVersion)
        {
            if (string.IsNullOrWhiteSpace(mcVersion)) return 21;

            if (mcVersion.StartsWith("1.20.5") || mcVersion.StartsWith("1.21"))
                return 21;

            if (mcVersion.StartsWith("1.17") || mcVersion.StartsWith("1.18") || mcVersion.StartsWith("1.19") || mcVersion.StartsWith("1.20"))
                return 17;

            if (mcVersion.StartsWith("1.13") || mcVersion.StartsWith("1.14") || mcVersion.StartsWith("1.15") || mcVersion.StartsWith("1.16"))
                return 16;

            return 8;
        }

        public async Task<List<MinecraftVersionInfo>> GetMojangVersionsAsync(bool includeSnapshots = false)
        {
            var versions = new List<MinecraftVersionInfo>();
            try
            {
                string manifestJson = await _httpClient.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
                using var doc = JsonDocument.Parse(manifestJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("versions", out var versionsArray))
                {
                    foreach (var item in versionsArray.EnumerateArray())
                    {
                        string id = item.GetProperty("id").GetString() ?? "";
                        string type = item.GetProperty("type").GetString() ?? "release";
                        string url = item.GetProperty("url").GetString() ?? "";

                        if (!includeSnapshots && type != "release")
                            continue;

                        versions.Add(new MinecraftVersionInfo
                        {
                            Id = id,
                            Type = type,
                            Url = url,
                            RecommendedJavaVersion = GetRecommendedJavaVersion(id)
                        });
                    }
                }
            }
            catch
            {
                // Fallback default list if offline
                versions.Add(new MinecraftVersionInfo { Id = "1.20.4", Type = "release", RecommendedJavaVersion = 21 });
                versions.Add(new MinecraftVersionInfo { Id = "1.20.2", Type = "release", RecommendedJavaVersion = 17 });
                versions.Add(new MinecraftVersionInfo { Id = "1.19.4", Type = "release", RecommendedJavaVersion = 17 });
                versions.Add(new MinecraftVersionInfo { Id = "1.16.5", Type = "release", RecommendedJavaVersion = 8 });
                versions.Add(new MinecraftVersionInfo { Id = "1.12.2", Type = "release", RecommendedJavaVersion = 8 });
            }

            return versions;
        }

        public List<ServerJarOption> GetPresetServerJars()
        {
            return new List<ServerJarOption>
            {
                new ServerJarOption
                {
                    SoftwareType = ServerSoftwareType.Paper,
                    Version = "1.20.4",
                    Build = "496",
                    DownloadUrl = "https://api.papermc.io/v2/projects/paper/versions/1.20.4/builds/496/downloads/paper-1.20.4-496.jar",
                    RecommendedJavaVersion = "Java 21 / 17",
                    FileName = "paper-1.20.4.jar",
                    Description = "Paper MC: Alto rendimiento, optimización masiva de rendimiento y soporte para plugins Spigot/Bukkit."
                },
                new ServerJarOption
                {
                    SoftwareType = ServerSoftwareType.Paper,
                    Version = "1.20.2",
                    Build = "318",
                    DownloadUrl = "https://api.papermc.io/v2/projects/paper/versions/1.20.2/builds/318/downloads/paper-1.20.2-318.jar",
                    RecommendedJavaVersion = "Java 17",
                    FileName = "paper-1.20.2.jar",
                    Description = "Paper MC para versión 1.20.2 con excelente compatibilidad y rendimiento."
                },
                new ServerJarOption
                {
                    SoftwareType = ServerSoftwareType.Purpur,
                    Version = "1.20.4",
                    Build = "latest",
                    DownloadUrl = "https://api.purpurmc.org/v2/purpur/1.20.4/latest/download",
                    RecommendedJavaVersion = "Java 21 / 17",
                    FileName = "purpur-1.20.4.jar",
                    Description = "Purpur MC: Fork de Paper con altísima personalización y configuración avanzada de juego."
                },
                new ServerJarOption
                {
                    SoftwareType = ServerSoftwareType.Vanilla,
                    Version = "1.20.4",
                    Build = "official",
                    DownloadUrl = "https://piston-data.mojang.com/v1/objects/833770bed101294204e93d11d30145899224b415/server.jar",
                    RecommendedJavaVersion = "Java 17 / 21",
                    FileName = "vanilla-server-1.20.4.jar",
                    Description = "Servidor Oficial Mojang Vanilla sin modificaciones."
                },
                new ServerJarOption
                {
                    SoftwareType = ServerSoftwareType.Spigot,
                    Version = "1.20.4",
                    Build = "stable",
                    DownloadUrl = "https://download.getbukkit.org/spigot/spigot-1.20.4.jar",
                    RecommendedJavaVersion = "Java 17 / 21",
                    FileName = "spigot-1.20.4.jar",
                    Description = "Spigot MC: Servidor modificado con soporte de API Bukkit/Spigot."
                }
            };
        }

        public async Task<string> DownloadServerJarAsync(ServerJarOption jarOption, string destinationFolder, Action<double>? progressCallback = null)
        {
            return await Task.Run(async () =>
            {
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                string targetFilePath = Path.Combine(destinationFolder, jarOption.FileName);

                using (var response = await _httpClient.GetAsync(jarOption.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        byte[] buffer = new byte[16384];
                        long totalRead = 0;
                        int read;
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;
                            if (totalBytes > 0)
                            {
                                progressCallback?.Invoke((double)totalRead / totalBytes * 100);
                            }
                        }
                    }
                }

                return targetFilePath;
            });
        }
    }
}
