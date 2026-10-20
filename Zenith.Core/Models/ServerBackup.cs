using System;

namespace Zenith.Core.Models
{
    public class ServerBackup
    {
        public string BackupId { get; set; } = Guid.NewGuid().ToString("N");
        public string ServerId { get; set; } = string.Empty;
        public string ServerName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Description { get; set; } = string.Empty;
        public bool IncludeWorlds { get; set; } = true;
        public bool IncludeConfigs { get; set; } = true;
        public bool IncludePlugins { get; set; } = true;
    }
}
