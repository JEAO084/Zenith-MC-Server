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
        private readonly string _runtimesDir;

        public JavaInstallerService(string? baseDir = null)
        {
            string root = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
            _runtimesDir = Path.Combine(root, "runtimes", "java");
            if (!Directory.Exists(_runtimesDir))
            {
                Directory.CreateDirectory(_runtimesDir);
            }
        }

        public List<JavaVersionOption> GetAvailableJavaVersions()
        {
            var versions = new List<JavaVersionOption>
            {
                new JavaVersionOption
                {
                    MajorVersion = 21,
                    Name = "Eclipse Temurin JDK 21 (LTS)",
                    Architecture = "x64",
                    DownloadUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.2%2B13/OpenJDK21U-jdk_x64_windows_hotspot_21.0.2_13.zip",
                    Description = "Recomendado para Minecraft 1.20.5+ y las versiones más recientes.",
                    InstalledPath = Path.Combine(_runtimesDir, "jdk-21", "bin", "java.exe")
                },
                new JavaVersionOption
                {
                    MajorVersion = 17,
                    Name = "Eclipse Temurin JDK 17 (LTS)",
                    Architecture = "x64",
                    DownloadUrl = "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.10%2B7/OpenJDK17U-jdk_x64_windows_hotspot_17.0.10_7.zip",
                    Description = "Recomendado para Minecraft 1.17 hasta 1.20.4.",
                    InstalledPath = Path.Combine(_runtimesDir, "jdk-17", "bin", "java.exe")
                },
                new JavaVersionOption
                {
                    MajorVersion = 8,
                    Name = "Eclipse Temurin JDK 8 (LTS)",
                    Architecture = "x64",
                    DownloadUrl = "https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u402-b06/OpenJDK8U-jdk_x64_windows_hotspot_8u402b06.zip",
                    Description = "Recomendado para Minecraft 1.12.2 y versiones legadas.",
                    InstalledPath = Path.Combine(_runtimesDir, "jdk-8", "bin", "java.exe")
                }
            };

            foreach (var v in versions)
            {
                v.IsInstalled = File.Exists(v.InstalledPath) || CheckSystemJavaInstalled(v.MajorVersion);
            }

            return versions;
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

        public async Task<string> DownloadAndExtractJavaAsync(JavaVersionOption option, Action<double>? progressCallback = null)
        {
            return await Task.Run(async () =>
            {
                string targetFolder = Path.Combine(_runtimesDir, $"jdk-{option.MajorVersion}");
                string zipFile = Path.Combine(_runtimesDir, $"jdk-{option.MajorVersion}.zip");

                if (File.Exists(Path.Combine(targetFolder, "bin", "java.exe")))
                {
                    option.IsInstalled = true;
                    option.InstalledPath = Path.Combine(targetFolder, "bin", "java.exe");
                    return option.InstalledPath;
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
                    Directory.Delete(targetFolder, true);
                }

                ZipFile.ExtractToDirectory(zipFile, targetFolder);
                if (File.Exists(zipFile))
                {
                    File.Delete(zipFile);
                }

                string executablePath = Path.Combine(targetFolder, "bin", "java.exe");
                if (!File.Exists(executablePath))
                {
                    var subdirs = Directory.GetDirectories(targetFolder);
                    if (subdirs.Length > 0)
                    {
                        string nestedExec = Path.Combine(subdirs[0], "bin", "java.exe");
                        if (File.Exists(nestedExec))
                        {
                            executablePath = nestedExec;
                        }
                    }
                }

                option.IsInstalled = true;
                option.InstalledPath = executablePath;
                return executablePath;
            });
        }
    }
}
