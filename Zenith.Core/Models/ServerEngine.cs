using System.Collections.Generic;

namespace Zenith.Core.Models
{
    public class ServerEngineInfo
    {
        public ServerEngineType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public bool SupportsPlugins { get; set; }
        public bool SupportsMods { get; set; }
        public List<string> SupportedVersions { get; set; } = new();
    }
}
