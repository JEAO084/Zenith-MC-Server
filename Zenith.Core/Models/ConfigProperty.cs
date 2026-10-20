using System.Collections.Generic;

namespace Zenith.Core.Models
{
    public enum ConfigValueType
    {
        String,
        Number,
        Boolean,
        Enum
    }

    public class ConfigProperty
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public ConfigValueType ValueType { get; set; } = ConfigValueType.String;
        public List<string> EnumOptions { get; set; } = new();
        public string Category { get; set; } = "General";
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
