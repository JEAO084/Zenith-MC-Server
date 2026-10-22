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
        public string Type { get; set; } = "release";
        public string Url { get; set; } = string.Empty;
        public DateTime ReleaseTime { get; set; }
        public int RecommendedJavaVersion { get; set; } = 21;
    }

    public class ServerDownloadService
    {
        private readonly HttpClient _httpClient = new();
        private List<MinecraftVersionInfo> _cachedVersions = new();

        public int GetRecommendedJavaVersion(string mcVersion)
        {
            if (string.IsNullOrWhiteSpace(mcVersion)) return 21;

            string v = mcVersion.ToLowerInvariant().Trim();

            // Check for class 69.0 / Java 25 requirements (e.g. Paper 26.x or experimental builds)
            if (v.StartsWith("26.") || v.Contains("26.3") || v.Contains("exp") || v.Contains("25"))
            {
                return 25;
            }

            if (v.Contains("w") || v.Contains("pre") || v.Contains("rc") || v.Contains("snapshot"))
            {
                return 22;
            }

            if (v.StartsWith("1.20.5") || v.StartsWith("1.20.6") || v.StartsWith("1.21") || v.StartsWith("1.22"))
            {
                return 21;
            }

            if (v.StartsWith("1.17") || v.StartsWith("1.18") || v.StartsWith("1.19") || v.StartsWith("1.20"))
            {
                return 17;
            }

            if (v.StartsWith("1.13") || v.StartsWith("1.14") || v.StartsWith("1.15") || v.StartsWith("1.16"))
            {
                return 16;
            }

            return 8;
        }

        public List<string> GetCompatibleLoadersForVersion(string mcVersion)
        {
            int javaVer = GetRecommendedJavaVersion(mcVersion);
            var loaders = new List<string> { "Paper", "Purpur", "Spigot", "Vanilla" };

            if (javaVer >= 16)
            {
                loaders.Add("Fabric");
            }
            loaders.Add("Forge");

            return loaders;
        }

        public async Task<List<string>> GetLoaderVersionsAsync(string loader, string mcVersion)
        {
            var versions = new List<string>();
            if (string.IsNullOrWhiteSpace(loader) || string.IsNullOrWhiteSpace(mcVersion))
            {
                return new List<string> { "Latest / Recommended" };
            }

            string cleanLoader = loader.Trim().ToLowerInvariant();

            if (cleanLoader.Contains("paper"))
            {
                try
                {
                    string buildsJson = await _httpClient.GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{mcVersion}");
                    using var doc = JsonDocument.Parse(buildsJson);
                    if (doc.RootElement.TryGetProperty("builds", out var buildsArr) && buildsArr.GetArrayLength() > 0)
                    {
                        var builds = buildsArr.EnumerateArray().Select(b => b.GetInt32()).Reverse().Take(10).ToList();
                        foreach (var b in builds)
                        {
                            versions.Add($"Build {b}");
                        }
                        if (versions.Count > 0)
                        {
                            versions[0] += " (Latest)";
                        }
                    }
                }
                catch { }
            }
            else if (cleanLoader.Contains("purpur"))
            {
                try
                {
                    string purpurJson = await _httpClient.GetStringAsync($"https://api.purpurmc.org/v2/purpur/{mcVersion}");
                    using var doc = JsonDocument.Parse(purpurJson);
                    if (doc.RootElement.TryGetProperty("builds", out var buildsObj) && buildsObj.TryGetProperty("all", out var buildsArr) && buildsArr.GetArrayLength() > 0)
                    {
                        var builds = buildsArr.EnumerateArray().Select(b => b.GetString() ?? "").Where(b => !string.IsNullOrEmpty(b)).Reverse().Take(10).ToList();
                        foreach (var b in builds)
                        {
                            versions.Add($"Build {b}");
                        }
                        if (versions.Count > 0)
                        {
                            versions[0] += " (Latest)";
                        }
                    }
                }
                catch { }
            }
            else if (cleanLoader.Contains("fabric"))
            {
                try
                {
                    string fabricJson = await _httpClient.GetStringAsync("https://meta.fabricmc.net/v2/versions/loader");
                    using var doc = JsonDocument.Parse(fabricJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray().Take(8))
                        {
                            if (item.TryGetProperty("version", out var vProp))
                            {
                                string ver = vProp.GetString() ?? "";
                                if (!string.IsNullOrEmpty(ver))
                                {
                                    versions.Add($"Fabric Loader {ver}");
                                }
                            }
                        }
                        if (versions.Count > 0)
                        {
                            versions[0] += " (Recommended)";
                        }
                    }
                }
                catch { }
            }
            else if (cleanLoader.Contains("forge"))
            {
                versions.Add("Forge Recommended Build");
                versions.Add("Forge Latest Build");
                versions.Add($"{mcVersion} - Standard");
            }

            if (versions.Count == 0)
            {
                versions.Add("Latest / Recommended");
            }

            return versions;
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

                        var info = new MinecraftVersionInfo
                        {
                            Id = id,
                            Type = type,
                            Url = url,
                            RecommendedJavaVersion = GetRecommendedJavaVersion(id)
                        };

                        _cachedVersions.Add(info);

                        if (!includeSnapshots && type != "release")
                            continue;

                        versions.Add(info);
                    }
                }
            }
            catch
            {
                versions.Add(new MinecraftVersionInfo { Id = "1.20.4", Type = "release", RecommendedJavaVersion = 21 });
                versions.Add(new MinecraftVersionInfo { Id = "1.20.2", Type = "release", RecommendedJavaVersion = 17 });
                versions.Add(new MinecraftVersionInfo { Id = "1.19.4", Type = "release", RecommendedJavaVersion = 17 });
                versions.Add(new MinecraftVersionInfo { Id = "1.16.5", Type = "release", RecommendedJavaVersion = 16 });
                versions.Add(new MinecraftVersionInfo { Id = "1.12.2", Type = "release", RecommendedJavaVersion = 8 });
            }

            return versions;
        }

        public async Task<string?> GetMojangServerJarUrlAsync(string version)
        {
            try
            {
                if (_cachedVersions.Count == 0)
                {
                    await GetMojangVersionsAsync(true);
                }

                var verInfo = _cachedVersions.FirstOrDefault(v => v.Id.Equals(version, StringComparison.OrdinalIgnoreCase));
                if (verInfo != null && !string.IsNullOrEmpty(verInfo.Url))
                {
                    string verJson = await _httpClient.GetStringAsync(verInfo.Url);
                    using var doc = JsonDocument.Parse(verJson);
                    if (doc.RootElement.TryGetProperty("downloads", out var downloads) &&
                        downloads.TryGetProperty("server", out var serverObj) &&
                        serverObj.TryGetProperty("url", out var urlProp))
                    {
                        return urlProp.GetString();
                    }
                }
            }
            catch { }

            return null;
        }

        public async Task<string> DownloadServerJarForVersionAsync(string softwareType, string version, string destinationFolder, Action<double>? progressCallback = null, string? loaderVersion = null)
        {
            string cleanType = softwareType.Trim();

            // Try primary loader endpoint
            try
            {
                if (cleanType.Contains("Paper", StringComparison.OrdinalIgnoreCase))
                {
                    string? buildNum = null;
                    if (!string.IsNullOrEmpty(loaderVersion) && loaderVersion.Contains("Build"))
                    {
                        var parts = loaderVersion.Split(' ');
                        foreach (var p in parts)
                        {
                            if (int.TryParse(p, out _)) { buildNum = p; break; }
                        }
                    }

                    string? paperUrl = await GetPaperJarUrlAsync(version, buildNum);
                    if (!string.IsNullOrEmpty(paperUrl))
                    {
                        return await DownloadFileFromUrlAsync(paperUrl, destinationFolder, "server.jar", progressCallback);
                    }
                }
                else if (cleanType.Contains("Purpur", StringComparison.OrdinalIgnoreCase))
                {
                    string build = "latest";
                    if (!string.IsNullOrEmpty(loaderVersion) && loaderVersion.Contains("Build"))
                    {
                        var parts = loaderVersion.Split(' ');
                        foreach (var p in parts)
                        {
                            if (int.TryParse(p, out _)) { build = p; break; }
                        }
                    }
                    string purpurUrl = $"https://api.purpurmc.org/v2/purpur/{version}/{build}/download";
                    return await DownloadFileFromUrlAsync(purpurUrl, destinationFolder, "server.jar", progressCallback);
                }
                else if (cleanType.Contains("Fabric", StringComparison.OrdinalIgnoreCase))
                {
                    string fabricLoader = "0.15.7";
                    if (!string.IsNullOrEmpty(loaderVersion))
                    {
                        var parts = loaderVersion.Split(' ');
                        foreach (var p in parts)
                        {
                            if (p.Contains('.')) { fabricLoader = p; break; }
                        }
                    }
                    string fabricUrl = $"https://meta.fabricmc.net/v2/versions/loader/{version}/{fabricLoader}/1.0.1/server/jar";
                    return await DownloadFileFromUrlAsync(fabricUrl, destinationFolder, "server.jar", progressCallback);
                }
                else if (cleanType.Contains("Forge", StringComparison.OrdinalIgnoreCase))
                {
                    string forgeUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{version}-latest/forge-{version}-latest-installer.jar";
                    return await DownloadFileFromUrlAsync(forgeUrl, destinationFolder, "server.jar", progressCallback);
                }
                else if (cleanType.Contains("Spigot", StringComparison.OrdinalIgnoreCase))
                {
                    string spigotUrl = $"https://download.getbukkit.org/spigot/spigot-{version}.jar";
                    return await DownloadFileFromUrlAsync(spigotUrl, destinationFolder, "server.jar", progressCallback);
                }
            }
            catch { }

            // Fallback 1: Mojang official server.jar for requested version
            try
            {
                string? mojangUrl = await GetMojangServerJarUrlAsync(version);
                if (!string.IsNullOrEmpty(mojangUrl))
                {
                    return await DownloadFileFromUrlAsync(mojangUrl, destinationFolder, "server.jar", progressCallback);
                }
            }
            catch { }

            // Fallback 2: Default stable Mojang Vanilla 1.20.4 server.jar
            string fallbackUrl = "https://piston-data.mojang.com/v1/objects/833770bed101294204e93d11d30145899224b415/server.jar";
            return await DownloadFileFromUrlAsync(fallbackUrl, destinationFolder, "server.jar", progressCallback);
        }

        private async Task<string?> GetPaperJarUrlAsync(string version, string? buildNumber = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(buildNumber) && int.TryParse(buildNumber, out int specifiedBuild))
                {
                    return $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{specifiedBuild}/downloads/paper-{version}-{specifiedBuild}.jar";
                }

                string buildsJson = await _httpClient.GetStringAsync($"https://api.papermc.io/v2/projects/paper/versions/{version}");
                using var doc = JsonDocument.Parse(buildsJson);
                if (doc.RootElement.TryGetProperty("builds", out var buildsArr) && buildsArr.GetArrayLength() > 0)
                {
                    int latestBuild = buildsArr.EnumerateArray().Last().GetInt32();
                    return $"https://api.papermc.io/v2/projects/paper/versions/{version}/builds/{latestBuild}/downloads/paper-{version}-{latestBuild}.jar";
                }
            }
            catch { }

            return null;
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
                    Description = "Paper MC: Alto rendimiento, optimización masiva y soporte para plugins Spigot/Bukkit."
                },
                new ServerJarOption
                {
                    SoftwareType = ServerSoftwareType.Purpur,
                    Version = "1.20.4",
                    Build = "latest",
                    DownloadUrl = "https://api.purpurmc.org/v2/purpur/1.20.4/latest/download",
                    RecommendedJavaVersion = "Java 21 / 17",
                    FileName = "purpur-1.20.4.jar",
                    Description = "Purpur MC: Fork de Paper con altísima personalización de mecánicas de juego."
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
            return await DownloadFileFromUrlAsync(jarOption.DownloadUrl, destinationFolder, jarOption.FileName, progressCallback);
        }

        public async Task<string> DownloadFileFromUrlAsync(string downloadUrl, string destinationFolder, string fileName = "server.jar", Action<double>? progressCallback = null)
        {
            return await Task.Run(async () =>
            {
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                string targetFilePath = Path.Combine(destinationFolder, fileName);

                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                    }

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
