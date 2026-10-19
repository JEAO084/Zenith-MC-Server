using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class FileManagerService
    {
        public List<ServerFileItem> GetDirectoryContents(string serverDirectory, string subPath = "")
        {
            string targetPath = Path.GetFullPath(Path.Combine(serverDirectory, subPath));

            // Security check: keep inside serverDirectory
            string fullServerDir = Path.GetFullPath(serverDirectory);
            if (!targetPath.StartsWith(fullServerDir, StringComparison.OrdinalIgnoreCase))
            {
                targetPath = fullServerDir;
            }

            var items = new List<ServerFileItem>();

            if (!Directory.Exists(targetPath))
            {
                return items;
            }

            var dirInfo = new DirectoryInfo(targetPath);

            foreach (var dir in dirInfo.GetDirectories())
            {
                string rel = Path.GetRelativePath(fullServerDir, dir.FullName);
                items.Add(new ServerFileItem
                {
                    Name = dir.Name,
                    RelativePath = rel,
                    FullPath = dir.FullName,
                    IsDirectory = true,
                    FileSizeBytes = 0,
                    FormattedSize = "<Carpeta>",
                    LastModified = dir.LastWriteTime,
                    FileType = "Carpeta de archivos"
                });
            }

            foreach (var file in dirInfo.GetFiles())
            {
                string rel = Path.GetRelativePath(fullServerDir, file.FullName);
                items.Add(new ServerFileItem
                {
                    Name = file.Name,
                    RelativePath = rel,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    FileSizeBytes = file.Length,
                    FormattedSize = FormatFileSize(file.Length),
                    LastModified = file.LastWriteTime,
                    FileType = GetFriendlyTypeName(file.Extension)
                });
            }

            return items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name).ToList();
        }

        public string ReadFileText(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;
            return File.ReadAllText(filePath, Encoding.UTF8);
        }

        public void SaveFileText(string filePath, string content)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        public void CreateDirectory(string serverDirectory, string subPath)
        {
            string targetPath = Path.Combine(serverDirectory, subPath);
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }
        }

        public void CreateFile(string filePath, string initialContent = "")
        {
            SaveFileText(filePath, initialContent);
        }

        public void DeleteItem(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        private string GetFriendlyTypeName(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".properties" => "Propiedades del Servidor",
                ".yml" or ".yaml" => "Configuración YAML",
                ".json" => "Archivo JSON",
                ".txt" => "Documento de Texto",
                ".log" => "Registro Log",
                ".jar" => "Archivo Executable JAR",
                ".sk" => "Skript Plugin",
                _ => "Archivo de Configuración"
            };
        }
    }
}
