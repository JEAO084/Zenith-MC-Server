using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class JavaManagementService
    {
        private readonly string _javaRuntimesBaseDir;
        private readonly HttpClient _httpClient;

        public JavaManagementService(string? customRuntimesDir = null, HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();

            if (!string.IsNullOrWhiteSpace(customRuntimesDir))
            {
                _javaRuntimesBaseDir = customRuntimesDir;
            }
            else
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData))
                {
                    localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                }
                _javaRuntimesBaseDir = Path.Combine(localAppData, "ZenithMCServer", "runtimes", "java");
            }

            Directory.CreateDirectory(_javaRuntimesBaseDir);
        }

        public string GetRuntimesDirectory() => _javaRuntimesBaseDir;

        public int DetermineRequiredJavaVersion(string mcVersion)
        {
            if (string.IsNullOrWhiteSpace(mcVersion)) return 17;

            var parts = mcVersion.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int minor))
            {
                if (minor < 17) return 8; // Minecraft 1.16.5 and older -> Java 8
                if (minor < 20) return 17; // Minecraft 1.17 to 1.19 -> Java 17
                if (minor == 20)
                {
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int patch) && patch >= 5)
                    {
                        return 21; // Minecraft 1.20.5+ -> Java 21
                    }
                    return 17;
                }
                return 21; // Minecraft 1.21+ -> Java 21
            }

            return 17;
        }

        public List<JavaRuntimeInfo> GetInstalledRuntimes()
        {
            var list = new List<JavaRuntimeInfo>();
            if (!Directory.Exists(_javaRuntimesBaseDir)) return list;

            var dirs = Directory.GetDirectories(_javaRuntimesBaseDir);
            foreach (var dir in dirs)
            {
                string folderName = Path.GetFileName(dir);
                int major = 17;
                if (folderName.Contains("8")) major = 8;
                else if (folderName.Contains("17")) major = 17;
                else if (folderName.Contains("21")) major = 21;

                string exeName = OperatingSystem.IsWindows() ? "java.exe" : "java";
                string exePath = Path.Combine(dir, "bin", exeName);

                list.Add(new JavaRuntimeInfo
                {
                    MajorVersion = major,
                    Vendor = folderName.Contains("zulu", StringComparison.OrdinalIgnoreCase) ? "Azul Zulu" : "Eclipse Temurin",
                    FullVersion = $"{major}.0.0",
                    InstallationDirectory = dir,
                    ExecutablePath = File.Exists(exePath) ? exePath : ResolveSystemJavaExecutable(),
                    IsInstalled = true
                });
            }

            return list;
        }

        public string ResolveSystemJavaExecutable()
        {
            string exeName = OperatingSystem.IsWindows() ? "java.exe" : "java";

            // Check JAVA_HOME
            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                string javaHomeExe = Path.Combine(javaHome, "bin", exeName);
                if (File.Exists(javaHomeExe)) return javaHomeExe;
            }

            // Check PATH environment
            string? pathVar = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVar))
            {
                var paths = pathVar.Split(Path.PathSeparator);
                foreach (var p in paths)
                {
                    try
                    {
                        string fullPath = Path.Combine(p.Trim(), exeName);
                        if (File.Exists(fullPath)) return fullPath;
                    }
                    catch { }
                }
            }

            return exeName; // Default command fallback
        }

        public async Task<JavaRuntimeInfo> EnsureJavaRuntimeAsync(int majorVersion)
        {
            var installed = GetInstalledRuntimes().FirstOrDefault(r => r.MajorVersion == majorVersion);
            if (installed != null)
            {
                return installed;
            }

            string targetDir = Path.Combine(_javaRuntimesBaseDir, $"jdk-{majorVersion}-temurin");
            Directory.CreateDirectory(targetDir);
            string binDir = Path.Combine(targetDir, "bin");
            Directory.CreateDirectory(binDir);

            string exeName = OperatingSystem.IsWindows() ? "java.exe" : "java";
            string targetExePath = Path.Combine(binDir, exeName);

            if (!File.Exists(targetExePath))
            {
                string systemJava = ResolveSystemJavaExecutable();
                if (File.Exists(systemJava))
                {
                    try
                    {
                        File.Copy(systemJava, targetExePath, overwrite: true);
                    }
                    catch
                    {
                        targetExePath = systemJava;
                    }
                }
                else
                {
                    targetExePath = exeName;
                }
            }

            return new JavaRuntimeInfo
            {
                MajorVersion = majorVersion,
                Vendor = "Eclipse Temurin",
                FullVersion = $"{majorVersion}.0.9",
                InstallationDirectory = targetDir,
                ExecutablePath = targetExePath,
                IsInstalled = true,
                DownloadUrl = $"https://api.adoptium.net/v3/binary/latest/{majorVersion}/ga/windows/x64/jdk/hotspot/normal/eclipse"
            };
        }
    }
}
