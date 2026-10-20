using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class ServerManager
    {
        private readonly string _serversDirectory;
        private readonly List<ServerInstance> _servers = new();

        public IReadOnlyList<ServerInstance> Servers => _servers.AsReadOnly();

        public ServerManager(string? customServersDirectory = null)
        {
            if (!string.IsNullOrWhiteSpace(customServersDirectory))
            {
                _serversDirectory = customServersDirectory;
            }
            else
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData))
                {
                    localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                }
                _serversDirectory = Path.Combine(localAppData, "ZenithMCServer", "servers");
            }

            Directory.CreateDirectory(_serversDirectory);
            LoadServers();
        }

        public void LoadServers()
        {
            _servers.Clear();
            if (!Directory.Exists(_serversDirectory)) return;

            var dirs = Directory.GetDirectories(_serversDirectory);
            foreach (var dir in dirs)
            {
                string jsonPath = Path.Combine(dir, "server_meta.json");
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        string json = File.ReadAllText(jsonPath);
                        var server = JsonSerializer.Deserialize<ServerInstance>(json);
                        if (server != null)
                        {
                            if (string.IsNullOrWhiteSpace(server.ServerPath))
                            {
                                server.ServerPath = dir;
                            }
                            _servers.Add(server);
                        }
                    }
                    catch
                    {
                        // Skip unparseable metadata
                    }
                }
            }
        }

        public async Task<ServerInstance> CreateServerAsync(
            string name,
            ServerEngineType engine,
            string version,
            int memoryMinMb,
            int memoryMaxMb,
            int port,
            string javaPath = "",
            string jvmArgs = "-XX:+UseG1GC")
        {
            string folderName = name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
            string serverPath = Path.Combine(_serversDirectory, folderName);
            Directory.CreateDirectory(serverPath);

            var server = new ServerInstance
            {
                Name = name,
                Engine = engine,
                Version = version,
                MemoryMinMb = memoryMinMb,
                MemoryMaxMb = memoryMaxMb,
                Port = port,
                ServerPath = serverPath,
                ExecutableJarPath = Path.Combine(serverPath, $"{engine.ToString().ToLower()}-{version}.jar"),
                CustomJavaPath = javaPath,
                JvmArguments = jvmArgs,
                Status = ServerStatus.Stopped
            };

            // Save metadata
            SaveServerMetadata(server);

            // Create initial server.properties file
            string propsPath = Path.Combine(serverPath, "server.properties");
            if (!File.Exists(propsPath))
            {
                string initialProps = $"# Minecraft Server Properties created by Zenith MC Server\n" +
                                     $"server-port={port}\n" +
                                     $"max-players=20\n" +
                                     $"motd=A Zenith Minecraft Server\n" +
                                     $"pvp=true\n" +
                                     $"difficulty=easy\n" +
                                     $"gamemode=survival\n" +
                                     $"online-mode=true\n" +
                                     $"allow-flight=false\n";
                await File.WriteAllTextAsync(propsPath, initialProps);
            }

            _servers.Add(server);
            return server;
        }

        public void SaveServerMetadata(ServerInstance server)
        {
            try
            {
                Directory.CreateDirectory(server.ServerPath);
                string jsonPath = Path.Combine(server.ServerPath, "server_meta.json");
                string json = JsonSerializer.Serialize(server, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonPath, json);
            }
            catch
            {
                // File writing handling
            }
        }

        public bool DeleteServer(string serverId, bool deleteFiles = true)
        {
            var server = _servers.FirstOrDefault(s => s.Id == serverId);
            if (server == null) return false;

            _servers.Remove(server);

            if (deleteFiles && Directory.Exists(server.ServerPath))
            {
                try
                {
                    Directory.Delete(server.ServerPath, recursive: true);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }
    }
}
