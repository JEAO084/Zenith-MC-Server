using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zenith.Core.Services
{
    public class PluginModItem
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public long SizeBytes { get; set; }
        public string FormattedSize => $"{SizeBytes / 1024.0 / 1024.0:F2} MB";
        public DateTime LastModified { get; set; }
    }

    public class PluginModManagerService
    {
        public List<PluginModItem> GetItems(string serverDir, bool isPluginMode)
        {
            var result = new List<PluginModItem>();
            string targetFolder = Path.Combine(serverDir, isPluginMode ? "plugins" : "mods");

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                return result;
            }

            var files = Directory.GetFiles(targetFolder, "*.*", SearchOption.TopDirectoryOnly)
                                 .Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                                             f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase));

            foreach (var f in files)
            {
                var fi = new FileInfo(f);
                result.Add(new PluginModItem
                {
                    FileName = fi.Name,
                    FullPath = fi.FullName,
                    IsEnabled = !fi.Name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase),
                    SizeBytes = fi.Length,
                    LastModified = fi.LastWriteTime
                });
            }

            return result;
        }

        public bool ToggleItem(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            string dir = Path.GetDirectoryName(filePath) ?? "";
            string fileName = Path.GetFileName(filePath);

            if (fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
            {
                string newPath = Path.Combine(dir, fileName.Substring(0, fileName.Length - ".disabled".Length));
                File.Move(filePath, newPath);
                return true;
            }
            else
            {
                string newPath = filePath + ".disabled";
                File.Move(filePath, newPath);
                return false;
            }
        }

        public void CopyImportedFiles(string serverDir, IEnumerable<string> sourcePaths, bool isPluginMode)
        {
            string targetFolder = Path.Combine(serverDir, isPluginMode ? "plugins" : "mods");
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            foreach (var path in sourcePaths)
            {
                if (File.Exists(path))
                {
                    if (path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        string dest = Path.Combine(targetFolder, Path.GetFileName(path));
                        File.Copy(path, dest, true);
                    }
                }
                else if (Directory.Exists(path))
                {
                    foreach (var subFile in Directory.GetFiles(path, "*.jar", SearchOption.AllDirectories))
                    {
                        string dest = Path.Combine(targetFolder, Path.GetFileName(subFile));
                        File.Copy(subFile, dest, true);
                    }
                }
            }
        }

        public void DeleteItem(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
