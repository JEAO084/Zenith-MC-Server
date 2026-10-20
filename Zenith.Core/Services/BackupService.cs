using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Zenith.Core.Models;

namespace Zenith.Core.Services
{
    public class BackupService
    {
        private readonly string _backupsBaseDir;

        public BackupService(string? customBackupsDir = null)
        {
            if (!string.IsNullOrWhiteSpace(customBackupsDir))
            {
                _backupsBaseDir = customBackupsDir;
            }
            else
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData))
                {
                    localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                }
                _backupsBaseDir = Path.Combine(localAppData, "ZenithMCServer", "backups");
            }

            Directory.CreateDirectory(_backupsBaseDir);
        }

        public async Task<ServerBackup> CreateBackupAsync(ServerInstance server, string description = "Automatic Pre-Update Backup")
        {
            string timeStampStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"{server.Name.Replace(" ", "_")}_{timeStampStr}.zip";
            string destinationZipPath = Path.Combine(_backupsBaseDir, backupFileName);

            if (File.Exists(destinationZipPath))
            {
                File.Delete(destinationZipPath);
            }

            await Task.Run(() =>
            {
                if (Directory.Exists(server.ServerPath))
                {
                    ZipFile.CreateFromDirectory(server.ServerPath, destinationZipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                }
                else
                {
                    // Create minimal zip archive if directory doesn't exist
                    using var zip = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);
                    var entry = zip.CreateEntry("backup_info.txt");
                    using var writer = new StreamWriter(entry.Open());
                    writer.WriteLine($"Backup created for {server.Name} at {DateTime.UtcNow}");
                }
            });

            var fileInfo = new FileInfo(destinationZipPath);

            return new ServerBackup
            {
                ServerId = server.Id,
                ServerName = server.Name,
                FilePath = destinationZipPath,
                SizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
                CreatedAt = DateTime.UtcNow,
                Description = description
            };
        }

        public List<ServerBackup> GetBackupsForServer(string serverId)
        {
            var list = new List<ServerBackup>();
            if (!Directory.Exists(_backupsBaseDir)) return list;

            var files = Directory.GetFiles(_backupsBaseDir, "*.zip");
            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                list.Add(new ServerBackup
                {
                    ServerId = serverId,
                    ServerName = fi.Name.Split('_')[0],
                    FilePath = fi.FullName,
                    SizeBytes = fi.Length,
                    CreatedAt = fi.CreationTimeUtc,
                    Description = "Server Backup Archive"
                });
            }

            return list.OrderByDescending(b => b.CreatedAt).ToList();
        }

        public async Task<bool> RestoreBackupAsync(string zipFilePath, string targetServerPath)
        {
            if (!File.Exists(zipFilePath)) return false;

            await Task.Run(() =>
            {
                Directory.CreateDirectory(targetServerPath);
                ZipFile.ExtractToDirectory(zipFilePath, targetServerPath, overwriteFiles: true);
            });

            return true;
        }
    }
}
