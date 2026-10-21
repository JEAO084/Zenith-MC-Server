using System;
using System.IO;
using Xunit;
using Zenith.Core.Services;

namespace Zenith.Tests
{
    public class FileManagerTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly FileManagerService _fileManager;

        public FileManagerTests()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "ZenithTestDir_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
            _fileManager = new FileManagerService();
        }

        [Fact]
        public void CreateAndReadFile_Works()
        {
            string filePath = Path.Combine(_tempFolder, "spigot.yml");
            string content = "settings:\n  bungeecord: true\n";

            _fileManager.CreateFile(filePath, content);
            Assert.True(File.Exists(filePath));

            string readContent = _fileManager.ReadFileText(filePath);
            Assert.Equal(content, readContent);
        }

        [Fact]
        public void DirectoryContents_ListsFilesAndFolders()
        {
            _fileManager.CreateDirectory(_tempFolder, "plugins");
            _fileManager.CreateFile(Path.Combine(_tempFolder, "server.properties"), "motd=Test");

            var items = _fileManager.GetDirectoryContents(_tempFolder);

            Assert.Equal(2, items.Count);
            Assert.Contains(items, i => i.Name == "plugins" && i.IsDirectory);
            Assert.Contains(items, i => i.Name == "server.properties" && !i.IsDirectory);
        }

        [Fact]
        public void DeleteItem_DeletesFileAndFolder()
        {
            string subDir = Path.Combine(_tempFolder, "logs");
            Directory.CreateDirectory(subDir);
            Assert.True(Directory.Exists(subDir));

            _fileManager.DeleteItem(subDir);
            Assert.False(Directory.Exists(subDir));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempFolder))
            {
                try
                {
                    Directory.Delete(_tempFolder, true);
                }
                catch { }
            }
        }
    }
}
