using System;
using System.Collections.Generic;

namespace Zenith.Core.Services
{
    public class EditorLineInfo
    {
        public int LineNumber { get; set; }
        public string Text { get; set; } = string.Empty;
        public int IndentationLevel { get; set; }
        public bool IsComment { get; set; }
        public bool IsKeyValue { get; set; }
    }

    public class IntegratedEditorService
    {
        public List<EditorLineInfo> AnalyzeContent(string content, string fileExtension)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var result = new List<EditorLineInfo>();

            bool isYaml = fileExtension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
                         fileExtension.Equals(".yaml", StringComparison.OrdinalIgnoreCase);
            bool isProps = fileExtension.Equals(".properties", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                string trimmed = rawLine.TrimStart();
                int indentSpaces = rawLine.Length - trimmed.Length;

                bool isComment = trimmed.StartsWith("#") || trimmed.StartsWith("//");
                bool isKeyValue = false;

                if (!isComment && !string.IsNullOrWhiteSpace(trimmed))
                {
                    if (isProps && trimmed.Contains("=")) isKeyValue = true;
                    else if (isYaml && trimmed.Contains(":")) isKeyValue = true;
                }

                result.Add(new EditorLineInfo
                {
                    LineNumber = i + 1,
                    Text = rawLine,
                    IndentationLevel = indentSpaces,
                    IsComment = isComment,
                    IsKeyValue = isKeyValue
                });
            }

            return result;
        }

        public string FormatYamlIndentation(string yamlContent)
        {
            var lines = yamlContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                // Ensure spaces are used instead of tab characters for YAML indentation
                if (lines[i].Contains('\t'))
                {
                    lines[i] = lines[i].Replace("\t", "  ");
                }
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
