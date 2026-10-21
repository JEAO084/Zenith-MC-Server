using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zenith.Core.Models;
using Zenith.Core.Services;

namespace Zenith.Tests
{
    public class MarketplaceServiceTests
    {
        [Fact]
        public void MarketplaceService_FiltersPluginsAndModsCorrectly()
        {
            var service = new MarketplaceService();
            var plugins = service.GetFeaturedItems(MarketplaceCategory.Plugin, "1.20.4", "Paper");

            Assert.NotEmpty(plugins);
            Assert.All(plugins, p => Assert.Equal(MarketplaceCategory.Plugin, p.Category));

            var mods = service.GetFeaturedItems(MarketplaceCategory.Mod, "1.20.4", "Fabric");
            Assert.NotEmpty(mods);
            Assert.All(mods, m => Assert.Equal(MarketplaceCategory.Mod, m.Category));
        }

        [Fact]
        public async Task MarketplaceService_InstallsJarFileToDirectory()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZenithMarketplaceTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            var service = new MarketplaceService();
            var item = new MarketplaceItem
            {
                Title = "TestPlugin",
                Category = MarketplaceCategory.Plugin,
                JarFileName = "TestPlugin.jar"
            };

            string installedPath = await service.DownloadAndInstallItemAsync(item, tempDir);
            Assert.True(File.Exists(installedPath));

            Directory.Delete(tempDir, true);
        }
    }
}
