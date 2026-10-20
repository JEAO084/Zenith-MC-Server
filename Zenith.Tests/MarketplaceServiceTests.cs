using System.IO;
using System.Threading.Tasks;
using Zenith.Core.Models;
using Zenith.Core.Services;
using Xunit;

namespace Zenith.Tests
{
    public class MarketplaceServiceTests
    {
        [Fact]
        public void GetLocalCatalog_ReturnsPluginsAndMods()
        {
            var service = new MarketplaceService();
            var plugins = service.GetLocalCatalog(MarketplaceCategory.Plugin);
            var mods = service.GetLocalCatalog(MarketplaceCategory.Mod);

            Assert.NotEmpty(plugins);
            Assert.NotEmpty(mods);
            Assert.Contains(plugins, p => p.Title == "EssentialsX");
            Assert.Contains(mods, m => m.Title == "Sodium");
        }

        [Fact]
        public async Task SearchItemsAsync_ReturnsFilteredPaginatedResults()
        {
            var service = new MarketplaceService();
            var filters = new MarketplaceFilterOptions
            {
                Query = "vault",
                Category = MarketplaceCategory.Plugin,
                Page = 1,
                PageSize = 5
            };

            var result = await service.SearchItemsAsync(filters);

            Assert.NotNull(result);
            Assert.NotNull(result.Items);
            Assert.NotEmpty(result.Items);
            Assert.Equal(1, result.CurrentPage);
        }

        [Fact]
        public async Task InstallItemAsync_CreatesPluginFileInTargetDirectory()
        {
            var service = new MarketplaceService();
            string tempDir = Path.Combine(Path.GetTempPath(), "ZenithTest_" + System.Guid.NewGuid().ToString("N"));

            try
            {
                var item = new MarketplaceItem
                {
                    Id = "test-plugin",
                    Title = "Test Plugin",
                    Category = MarketplaceCategory.Plugin,
                    FileName = "TestPlugin.jar"
                };

                bool installed = await service.InstallItemAsync(item, tempDir);

                Assert.True(installed);
                Assert.True(item.IsInstalled);
                Assert.True(File.Exists(Path.Combine(tempDir, "plugins", "TestPlugin.jar")));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
    }
}
