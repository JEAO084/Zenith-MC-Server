using System;

namespace Zenith.Core.Models
{
    public class ServerCreationWizardModel
    {
        public string Name { get; set; } = "Nuevo Servidor";
        public string IconSymbol { get; set; } = "⛏️";
        public string MinecraftVersion { get; set; } = "1.20.4";
        public bool IsSnapshot { get; set; }
        public string SoftwareLoader { get; set; } = "Paper";
        public int RequiredJavaVersion { get; set; } = 21;
        public int RamGb { get; set; } = 4;
        public bool EnableAikarsFlags { get; set; } = true;
        public int Port { get; set; } = 25565;
        public bool AutoAcceptEula { get; set; } = true;
    }
}
