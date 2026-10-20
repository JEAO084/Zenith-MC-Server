using System.IO;
using System.Threading.Tasks;
using Zenith.Core.Models;
using Zenith.Core.Services;
using Xunit;

namespace Zenith.Tests
{
    public class ServerManagerAndProcessTests
    {
        [Fact]
        public async Task ServerManager_CreatesAndDeletesServerInstance()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZenithServer_" + System.Guid.NewGuid().ToString("N"));
            var manager = new ServerManager(tempDir);

            try
            {
                var server = await manager.CreateServerAsync(
                    name: "Test Survival Server",
                    engine: ServerEngineType.Paper,
                    version: "1.20.4",
                    memoryMinMb: 1024,
                    memoryMaxMb: 2048,
                    port: 25565
                );

                Assert.NotNull(server);
                Assert.Equal("Test Survival Server", server.Name);
                Assert.Equal(25565, server.Port);
                Assert.True(File.Exists(Path.Combine(server.ServerPath, "server.properties")));
                Assert.True(File.Exists(Path.Combine(server.ServerPath, "server_meta.json")));

                bool deleted = manager.DeleteServer(server.Id, deleteFiles: true);
                Assert.True(deleted);
                Assert.False(Directory.Exists(server.ServerPath));
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
        public void LiveConsoleManager_BuffersLogsAndProvidesAutocomplete()
        {
            var console = new LiveConsoleManager();
            console.AppendLine("Server started successfully!");

            Assert.Single(console.ConsoleLogs);
            Assert.Contains("Server started successfully!", console.ConsoleLogs[0]);

            var suggestions = console.GetAutocompleteSuggestions("white");
            Assert.NotEmpty(suggestions);
            Assert.Contains("whitelist add", suggestions);
        }
    }
}
