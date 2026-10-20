using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Zenith.Core.Services
{
    public class LiveConsoleManager
    {
        private readonly List<string> _consoleLogs = new();
        private readonly int _maxLogLines = 2000;

        private static readonly List<string> CommonCommands = new()
        {
            "op",
            "deop",
            "whitelist add",
            "whitelist remove",
            "whitelist list",
            "whitelist on",
            "whitelist off",
            "gamemode survival",
            "gamemode creative",
            "gamemode adventure",
            "gamemode spectator",
            "teleport",
            "tp",
            "kick",
            "ban",
            "pardon",
            "stop",
            "save-all",
            "save-off",
            "save-on",
            "reload",
            "say",
            "give",
            "clear",
            "difficulty easy",
            "difficulty normal",
            "difficulty hard",
            "difficulty peaceful",
            "time set day",
            "time set night",
            "weather clear",
            "weather rain"
        };

        public event Action<string>? LineAdded;

        public IReadOnlyList<string> ConsoleLogs => _consoleLogs.AsReadOnly();

        public void AppendLine(string line)
        {
            if (line == null) return;

            string formattedLine = $"[{DateTime.Now:HH:mm:ss}] {line}";
            _consoleLogs.Add(formattedLine);

            if (_consoleLogs.Count > _maxLogLines)
            {
                _consoleLogs.RemoveAt(0);
            }

            LineAdded?.Invoke(formattedLine);
        }

        public List<string> GetAutocompleteSuggestions(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return CommonCommands.Take(8).ToList();
            }

            string trim = input.TrimStart();
            return CommonCommands
                .Where(cmd => cmd.StartsWith(trim, StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .ToList();
        }

        public void ClearLogs()
        {
            _consoleLogs.Clear();
        }
    }
}
