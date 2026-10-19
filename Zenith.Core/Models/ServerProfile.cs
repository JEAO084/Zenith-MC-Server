using System;

namespace Zenith.Core.Models
{
    public class ServerProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Mi Servidor";
        public string DirectoryPath { get; set; } = string.Empty;
        public string SoftwareType { get; set; } = "Paper";
        public string Version { get; set; } = "1.20.4";
        public string JarFileName { get; set; } = "paper-1.20.4.jar";
        public int AllocatedRamGb { get; set; } = 4;
        public string CustomJvmArgs { get; set; } = "";
        public string JavaExecutablePath { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
