using System;

namespace Zenith.Core.Models
{
    public enum ServerEngineType
    {
        Paper,
        Purpur,
        Spigot,
        Vanilla,
        Fabric,
        Forge,
        NeoForge
    }

    public enum ServerStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
    }

    public class ServerInstance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Minecraft Server";
        public ServerEngineType Engine { get; set; } = ServerEngineType.Paper;
        public string Version { get; set; } = "1.20.4";
        public int MemoryMinMb { get; set; } = 1024;
        public int MemoryMaxMb { get; set; } = 4096;
        public int Port { get; set; } = 25565;
        public string ServerPath { get; set; } = string.Empty;
        public string ExecutableJarPath { get; set; } = string.Empty;
        public string CustomJavaPath { get; set; } = string.Empty;
        public string JvmArguments { get; set; } = "-XX:+UseG1GC -XX:+ParallelRefProcEnabled";
        public ServerStatus Status { get; set; } = ServerStatus.Stopped;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastStartedAt { get; set; }
    }
}
