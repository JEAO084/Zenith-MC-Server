using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class ServerManagerService
    {
        private readonly string _baseDataDir;
        private readonly string _serversRootDir;
        private readonly string _profilesJsonPath;
        private List<ServerProfile> _profiles = new();

        public ServerProfile? ActiveProfile { get; private set; }

        public ServerManagerService(string? baseDataDir = null)
        {
            _baseDataDir = baseDataDir ?? AppDomain.CurrentDomain.BaseDirectory;
            _serversRootDir = Path.Combine(_baseDataDir, "servers");
            _profilesJsonPath = Path.Combine(_baseDataDir, "server_profiles.json");

            if (!Directory.Exists(_serversRootDir))
            {
                Directory.CreateDirectory(_serversRootDir);
            }

            LoadProfiles();
        }

        public List<ServerProfile> GetProfiles()
        {
            return _profiles.ToList();
        }

        public ServerProfile CreateProfile(string name, string softwareType, string version, int ramGb = 4)
        {
            string safeFolderName = string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
            if (string.IsNullOrWhiteSpace(safeFolderName))
            {
                safeFolderName = "Server_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }

            string serverDir = Path.Combine(_serversRootDir, safeFolderName);
            if (!Directory.Exists(serverDir))
            {
                Directory.CreateDirectory(serverDir);
            }

            var profile = new ServerProfile
            {
                Name = name,
                SoftwareType = softwareType,
                Version = version,
                DirectoryPath = serverDir,
                AllocatedRamGb = ramGb,
                JarFileName = $"{softwareType.ToLower()}-{version}.jar"
            };

            _profiles.Add(profile);
            ActiveProfile = profile;
            SaveProfiles();
            return profile;
        }

        public void SelectProfile(string profileId)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile != null)
            {
                ActiveProfile = profile;
            }
        }

        public void UpdateProfile(ServerProfile updatedProfile)
        {
            var existing = _profiles.FirstOrDefault(p => p.Id == updatedProfile.Id);
            if (existing != null)
            {
                existing.Name = updatedProfile.Name;
                existing.AllocatedRamGb = updatedProfile.AllocatedRamGb;
                existing.CustomJvmArgs = updatedProfile.CustomJvmArgs;
                existing.JavaExecutablePath = updatedProfile.JavaExecutablePath;
                existing.JarFileName = updatedProfile.JarFileName;
                existing.SoftwareType = updatedProfile.SoftwareType;
                existing.Version = updatedProfile.Version;
                SaveProfiles();
            }
        }

        public void DeleteProfile(string profileId, bool deleteDirectory = false)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile != null)
            {
                _profiles.Remove(profile);
                if (ActiveProfile?.Id == profileId)
                {
                    ActiveProfile = _profiles.FirstOrDefault();
                }

                if (deleteDirectory && Directory.Exists(profile.DirectoryPath))
                {
                    try
                    {
                        Directory.Delete(profile.DirectoryPath, true);
                    }
                    catch { }
                }

                SaveProfiles();
            }
        }

        private void LoadProfiles()
        {
            if (File.Exists(_profilesJsonPath))
            {
                try
                {
                    string json = File.ReadAllText(_profilesJsonPath);
                    var loaded = JsonSerializer.Deserialize<List<ServerProfile>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        _profiles = loaded;
                        ActiveProfile = _profiles.FirstOrDefault();
                        return;
                    }
                }
                catch { }
            }

            // Create default initial profile if none exists
            string defaultServerDir = Path.Combine(_serversRootDir, "Servidor_Principal");
            if (!Directory.Exists(defaultServerDir))
            {
                Directory.CreateDirectory(defaultServerDir);
            }

            var defaultProfile = new ServerProfile
            {
                Name = "Servidor Principal",
                SoftwareType = "Paper",
                Version = "1.20.4",
                DirectoryPath = defaultServerDir,
                AllocatedRamGb = 4,
                JarFileName = "paper-1.20.4.jar"
            };

            _profiles.Add(defaultProfile);
            ActiveProfile = defaultProfile;
            SaveProfiles();
        }

        private void SaveProfiles()
        {
            try
            {
                string json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_profilesJsonPath, json);
            }
            catch { }
        }
    }
}
