using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zenith.Core.Services
{
    public class FileExplorerItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long SizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public string Extension { get; set; } = string.Empty;
        public List<FileExplorerItem> Children { get; set; } = new();
    }

    public class FileExplorerService
    {
        public List<FileExplorerItem> GetDirectoryStructure(string rootPath, int maxDepth = 3)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return new List<FileExplorerItem>();
            }

            return BuildDirectoryTree(rootPath, 0, maxDepth);
        }

        private List<FileExplorerItem> BuildDirectoryTree(string path, int currentDepth, int maxDepth)
        {
            var items = new List<FileExplorerItem>();
            if (currentDepth >= maxDepth) return items;

            try
            {
                var dirInfo = new DirectoryInfo(path);

                foreach (var dir in dirInfo.GetDirectories())
                {
                    // Ignore hidden directories like .git
                    if (dir.Name.StartsWith(".")) continue;

                    var item = new FileExplorerItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true,
                        LastModified = dir.LastWriteTime,
                        Extension = string.Empty,
                        Children = BuildDirectoryTree(dir.FullName, currentDepth + 1, maxDepth)
                    };
                    items.Add(item);
                }

                foreach (var file in dirInfo.GetFiles())
                {
                    if (file.Name.StartsWith(".")) continue;

                    items.Add(new FileExplorerItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        SizeBytes = file.Length,
                        LastModified = file.LastWriteTime,
                        Extension = file.Extension.ToLowerInvariant()
                    });
                }
            }
            catch
            {
                // Access permission handling
            }

            return items;
        }

        public string ReadFileText(string filePath)
        {
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            return string.Empty;
        }

        public void WriteFileText(string filePath, string content)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, content);
        }

        public bool DeleteItem(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return true;
            }
            return false;
        }
    }
}
