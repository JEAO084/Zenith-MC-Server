using System.IO;
using System.Threading.Tasks;
using Zenith.Core.Models;
using Zenith.Core.Services;
using Xunit;

namespace Zenith.Tests
{
    public class BackupAndSettingsTests
    {
        [Fact]
        public async Task BackupService_CreatesAndRestoresZipArchive()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZenithBackup_" + System.Guid.NewGuid().ToString("N"));
            string backupDir = Path.Combine(tempDir, "backups");
            string serverDir = Path.Combine(tempDir, "server");
            Directory.CreateDirectory(serverDir);

            // Add dummy file in server dir
            File.WriteAllText(Path.Combine(serverDir, "test.txt"), "Hello Backup World!");

            var backupService = new BackupService(backupDir);

            try
            {
                var server = new ServerInstance
                {
                    Name = "MyServer",
                    ServerPath = serverDir
                };

                var backup = await backupService.CreateBackupAsync(server, "Test Backup");

                Assert.NotNull(backup);
                Assert.True(File.Exists(backup.FilePath));

                string restoreTarget = Path.Combine(tempDir, "restored_server");
                bool restored = await backupService.RestoreBackupAsync(backup.FilePath, restoreTarget);

                Assert.True(restored);
                Assert.True(File.Exists(Path.Combine(restoreTarget, "test.txt")));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        [Fact]
        public void LauncherSettingsService_SavesAndLoadsSettings()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "ZenithSettings_" + System.Guid.NewGuid().ToString("N") + ".json");
            var service = new LauncherSettingsService(tempFile);

            try
            {
                service.Settings.MaxCpuThreads = 8;
                service.Settings.Theme = UIThemeMode.Transparent;
                service.SaveSettings();

                var reloaded = new LauncherSettingsService(tempFile);
                Assert.Equal(8, reloaded.Settings.MaxCpuThreads);
                Assert.Equal(UIThemeMode.Transparent, reloaded.Settings.Theme);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
