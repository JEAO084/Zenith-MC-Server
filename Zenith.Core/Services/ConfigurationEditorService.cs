using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class ConfigurationEditorService
    {
        public List<ConfigProperty> ParseServerProperties(string propertiesContent)
        {
            var properties = new List<ConfigProperty>();
            if (string.IsNullOrWhiteSpace(propertiesContent)) return properties;

            var lines = propertiesContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string pendingComment = "";

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#") || trimmed.StartsWith("!"))
                {
                    pendingComment += (string.IsNullOrEmpty(pendingComment) ? "" : " ") + trimmed.TrimStart('#', '!', ' ');
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    pendingComment = "";
                    continue;
                }

                int idx = trimmed.IndexOf('=');
                if (idx > 0)
                {
                    string key = trimmed.Substring(0, idx).Trim();
                    string value = trimmed.Substring(idx + 1).Trim();

                    var configProp = DeterminePropertyTypeAndMetadata(key, value, pendingComment);
                    properties.Add(configProp);

                    pendingComment = "";
                }
            }

            return properties;
        }

        private ConfigProperty DeterminePropertyTypeAndMetadata(string key, string value, string comment)
        {
            var prop = new ConfigProperty
            {
                Key = key,
                Value = value,
                Comment = comment,
                DisplayName = FormatDisplayName(key)
            };

            string lowerKey = key.ToLowerInvariant();

            if (lowerKey == "pvp" || lowerKey == "allow-flight" || lowerKey == "hardcore" ||
                lowerKey == "online-mode" || lowerKey == "enable-command-block" || lowerKey == "white-list" ||
                lowerKey == "spawn-protection" || lowerKey == "generate-structures" || lowerKey == "allow-nether")
            {
                prop.ValueType = ConfigValueType.Boolean;
            }
            else if (lowerKey == "max-players" || lowerKey == "server-port" || lowerKey == "view-distance" ||
                     lowerKey == "simulation-distance" || lowerKey == "max-tick-time" || lowerKey == "spawn-protection")
            {
                prop.ValueType = ConfigValueType.Number;
            }
            else if (lowerKey == "difficulty")
            {
                prop.ValueType = ConfigValueType.Enum;
                prop.EnumOptions = new List<string> { "peaceful", "easy", "normal", "hard" };
            }
            else if (lowerKey == "gamemode")
            {
                prop.ValueType = ConfigValueType.Enum;
                prop.EnumOptions = new List<string> { "survival", "creative", "adventure", "spectator" };
            }
            else
            {
                prop.ValueType = ConfigValueType.String;
            }

            return prop;
        }

        private string FormatDisplayName(string key)
        {
            string[] parts = key.Split('-', '_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
                }
            }
            return string.Join(" ", parts);
        }

        public string SaveServerProperties(string originalContent, List<ConfigProperty> updatedProperties)
        {
            var propDict = updatedProperties.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            var lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#") || trimmed.StartsWith("!") || string.IsNullOrWhiteSpace(trimmed))
                {
                    sb.AppendLine(line);
                    continue;
                }

                int idx = trimmed.IndexOf('=');
                if (idx > 0)
                {
                    string key = trimmed.Substring(0, idx).Trim();
                    if (propDict.TryGetValue(key, out var newValue))
                    {
                        sb.AppendLine($"{key}={newValue}");
                        processedKeys.Add(key);
                    }
                    else
                    {
                        sb.AppendLine(line);
                    }
                }
                else
                {
                    sb.AppendLine(line);
                }
            }

            // Append any newly added properties that were not in original content
            foreach (var prop in updatedProperties)
            {
                if (!processedKeys.Contains(prop.Key))
                {
                    if (!string.IsNullOrWhiteSpace(prop.Comment))
                    {
                        sb.AppendLine($"# {prop.Comment}");
                    }
                    sb.AppendLine($"{prop.Key}={prop.Value}");
                }
            }

            return sb.ToString();
        }
    }
}
