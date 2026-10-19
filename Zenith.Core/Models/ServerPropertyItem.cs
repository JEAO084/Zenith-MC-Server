using System;
using System.Collections.Generic;

namespace Zenith.Core.Models
{
    public enum PropertyCategory
    {
        General,
        Gameplay,
        World,
        Network,
        Security,
        Performance
    }

    public enum PropertyType
    {
        String,
        Integer,
        Boolean,
        Enum
    }

    public class ServerPropertyItem
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public PropertyType Type { get; set; } = PropertyType.String;
        public PropertyCategory Category { get; set; } = PropertyCategory.General;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> AllowedValues { get; set; } = new();

        public ServerPropertyItem() { }

        public ServerPropertyItem(string key, string value, string defaultValue, PropertyType type, PropertyCategory category, string displayName, string description, List<string>? allowedValues = null)
        {
            Key = key;
            Value = value;
            DefaultValue = defaultValue;
            Type = type;
            Category = category;
            DisplayName = displayName;
            Description = description;
            AllowedValues = allowedValues ?? new List<string>();
        }
    }
}
