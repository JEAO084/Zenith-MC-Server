using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Zenith.Core.Parsers
{
    public class MotdSegment
    {
        public string Text { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#FFFFFF";
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsUnderlined { get; set; }
        public bool IsStrikethrough { get; set; }
    }

    public class MinecraftMotdParser
    {
        private static readonly Dictionary<char, string> ColorMap = new()
        {
            ['0'] = "#000000", // Black
            ['1'] = "#0000AA", // Dark Blue
            ['2'] = "#00AA00", // Dark Green
            ['3'] = "#00AAAA", // Dark Aqua
            ['4'] = "#AA0000", // Dark Red
            ['5'] = "#AA00AA", // Dark Purple
            ['6'] = "#FFAA00", // Gold
            ['7'] = "#AAAAAA", // Gray
            ['8'] = "#555555", // Dark Gray
            ['9'] = "#5555FF", // Blue
            ['a'] = "#55FF55", // Green
            ['b'] = "#55FFFF", // Aqua
            ['c'] = "#FF5555", // Red
            ['d'] = "#FF55FF", // Light Purple
            ['e'] = "#FFFF55", // Yellow
            ['f'] = "#FFFFFF"  // White
        };

        public static List<MotdSegment> ParseMotd(string rawMotd)
        {
            var segments = new List<MotdSegment>();
            if (string.IsNullOrEmpty(rawMotd)) return segments;

            // Normalize & to §
            string normalized = rawMotd.Replace('&', '§');

            string currentColor = "#FFFFFF";
            bool isBold = false;
            bool isItalic = false;
            bool isUnderline = false;
            bool isStrike = false;

            int i = 0;
            string currentText = "";

            while (i < normalized.Length)
            {
                if (normalized[i] == '§' && i + 1 < normalized.Length)
                {
                    char code = char.ToLowerInvariant(normalized[i + 1]);

                    if (!string.IsNullOrEmpty(currentText))
                    {
                        segments.Add(new MotdSegment
                        {
                            Text = currentText,
                            ColorHex = currentColor,
                            IsBold = isBold,
                            IsItalic = isItalic,
                            IsUnderlined = isUnderline,
                            IsStrikethrough = isStrike
                        });
                        currentText = "";
                    }

                    if (ColorMap.ContainsKey(code))
                    {
                        currentColor = ColorMap[code];
                        isBold = false;
                        isItalic = false;
                        isUnderline = false;
                        isStrike = false;
                    }
                    else if (code == 'l') isBold = true;
                    else if (code == 'm') isStrike = true;
                    else if (code == 'n') isUnderline = true;
                    else if (code == 'o') isItalic = true;
                    else if (code == 'r')
                    {
                        currentColor = "#FFFFFF";
                        isBold = false;
                        isItalic = false;
                        isUnderline = false;
                        isStrike = false;
                    }

                    i += 2;
                }
                else
                {
                    currentText += normalized[i];
                    i++;
                }
            }

            if (!string.IsNullOrEmpty(currentText))
            {
                segments.Add(new MotdSegment
                {
                    Text = currentText,
                    ColorHex = currentColor,
                    IsBold = isBold,
                    IsItalic = isItalic,
                    IsUnderlined = isUnderline,
                    IsStrikethrough = isStrike
                });
            }

            return segments;
        }
    }
}
