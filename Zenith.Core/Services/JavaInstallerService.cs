using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class JavaInstallerService
    {
        private readonly HttpClient _httpClient = new();
        private string _runtimesDir;

        public string RuntimesDirectory => _runtimesDir;

        public JavaInstallerService(string? baseDir = null)
        {
            string root = baseDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZenithMCServer");
            _runtimesDir = Path.Combine(root, "runtimes", "java");
            if (!Directory.Exists(_runtimesDir))
            {
                Directory.CreateDirectory(_runtimesDir);
            }
        }

        public void SetCustomRuntimesDirectory(string customPath)
        {
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                _runtimesDir = customPath;
                if (!Directory.Exists(_runtimesDir))
                {
                    Directory.CreateDirectory(_runtimesDir);
                }
            }
        }

        public List<JavaVersionOption> GetAvailableJavaVersions()
        {
            var versions = new List<JavaVersionOption>
            {
                new JavaVersionOption
                {
                    MajorVersion = 25,
                    Name = "OpenJDK 25 (Latest / Experimental)",
                    Architecture = "x64",
                    DownloadUrl = "https://download.java.net/java/GA/jdk25/12/GPL/openjdk-25_windows-x64_bin.zip",
                    Description = "Recomendado para versiones experimentales de Minecraft que requieren class file 69.0.",
                    InstalledPath = FindJavaExecutable(Path.Combine(_runtimesDir, "jdk-25"))
                },
                new JavaVersionOption
                {
                    MajorVersion = 22,
                    Name = "Eclipse Temurin JDK 22",
                    Architecture = "x64",
                    DownloadUrl = "https://github.com/adoptium/temurin22-binaries/releases/download/jdk-22.0.1%2B8/OpenJDK22U-jdk_x64_windows_hotspot_22.0.1_8.zip",
                    Description = "Recomendado para versiones recientes y snapshots de Minecraft 1.21+.",
                    InstalledPath = FindJavaExecutable(Path.Combine(_runtimesDir, "jdk-22"))
                },
                new JavaVersionOption
                {
                    MajorVersion = 21,
                    Name = "Eclipse Temurin JDK 21 (LTS)",
                    Architecture = "x64",
                    DownloadUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.2%2B13/OpenJDK21U-jdk_x64_windows_hotspot_21.0.2_13.zip",
                    Description = "Recomendado para Minecraft 1.20.5+ y 1.21.",
                    InstalledPath = FindJavaExecutable(Path.Combine(_runtimesDir, "jdk-21"))
                },
                new JavaVersionOption
                {
                    MajorVersion = 17,
                    Name = "Eclipse Temurin JDK 17 (LTS)",
                    Architecture = "x64",
                    DownloadUrl = "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.10%2B7/OpenJDK17U-jdk_x64_windows_hotspot_17.0.10_7.zip",
                    Description = "Recomendado para Minecraft 1.17 hasta 1.20.4.",
                    InstalledPath = FindJavaExecutable(Path.Combine(_runtimesDir, "jdk-17"))
                },
                new JavaVersionOption
                {
                    MajorVersion = 8,
                    Name = "Eclipse Temurin JDK 8 (LTS)",
                    Architecture = "x64",
                    DownloadUrl = "https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u402-b06/OpenJDK8U-jdk_x64_windows_hotspot_8u402b06.zip",
                    Description = "Recomendado para Minecraft 1.12.2 y versiones legadas.",
                    InstalledPath = FindJavaExecutable(Path.Combine(_runtimesDir, "jdk-8"))
                }
            };

            foreach (var v in versions)
            {
                v.IsInstalled = File.Exists(v.InstalledPath) || CheckSystemJavaInstalled(v.MajorVersion);
            }

            return versions;
        }

        private static string FindJavaExecutable(string targetFolder)
        {
            string direct = Path.Combine(targetFolder, "bin", "java.exe");
            if (File.Exists(direct)) return direct;

            if (Directory.Exists(targetFolder))
            {
                var subdirs = Directory.GetDirectories(targetFolder);
                foreach (var sub in subdirs)
                {
                    string nested = Path.Combine(sub, "bin", "java.exe");
                    if (File.Exists(nested)) return nested;
                }
            }

            return direct;
        }

        public bool CheckSystemJavaInstalled(int majorVersion)
        {
            string? pathVar = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(pathVar) && File.Exists(Path.Combine(pathVar, "bin", "java.exe")))
            {
                return true;
            }
            return false;
        }

        private static void ClearReadOnlyAttributes(string targetDir)
        {
            if (!Directory.Exists(targetDir)) return;
            try
            {
                var dirInfo = new DirectoryInfo(targetDir);
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }
                foreach (var subDir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
                {
                    subDir.Attributes &= ~FileAttributes.ReadOnly;
                }
            }
            catch { }
        }

        public async Task<string> DownloadAndExtractJavaAsync(JavaVersionOption option, Action<double>? progressCallback = null)
        {
            return await Task.Run(async () =>
            {
                string targetFolder = Path.Combine(_runtimesDir, $"jdk-{option.MajorVersion}");
                string zipFile = Path.Combine(_runtimesDir, $"jdk-{option.MajorVersion}.zip");

                string existingExe = FindJavaExecutable(targetFolder);
                if (File.Exists(existingExe))
                {
                    option.IsInstalled = true;
                    option.InstalledPath = existingExe;
                    return existingExe;
                }

                using (var response = await _httpClient.GetAsync(option.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(zipFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
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

                // Extract archive asynchronously
                if (Directory.Exists(targetFolder))
                {
                    ClearReadOnlyAttributes(targetFolder);
                    Directory.Delete(targetFolder, true);
                }

                ZipFile.ExtractToDirectory(zipFile, targetFolder);
                if (File.Exists(zipFile))
                {
                    File.Delete(zipFile);
                }

                string executablePath = FindJavaExecutable(targetFolder);

                option.IsInstalled = true;
                option.InstalledPath = executablePath;
                return executablePath;
            });
        }
    }
}
