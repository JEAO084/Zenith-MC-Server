using System.Threading.Tasks;
using Zenith.Core.Models;
using Zenith.Core.Services;
using Xunit;

namespace Zenith.Tests
{
    public class EngineServiceTests
    {
        [Fact]
        public void GetSupportedEngines_ReturnsAllEngineTypes()
        {
            var service = new EngineService();
            var engines = service.GetSupportedEngines();

            Assert.NotNull(engines);
            Assert.True(engines.Count >= 6);
            Assert.Contains(engines, e => e.Type == ServerEngineType.Paper);
            Assert.Contains(engines, e => e.Type == ServerEngineType.Purpur);
            Assert.Contains(engines, e => e.Type == ServerEngineType.Fabric);
            Assert.Contains(engines, e => e.Type == ServerEngineType.Forge);
            Assert.Contains(engines, e => e.Type == ServerEngineType.NeoForge);
        }

        [Fact]
        public async Task GetDynamicVersionsAsync_ReturnsVersionList()
        {
            var service = new EngineService();
            var versions = await service.GetDynamicVersionsAsync(ServerEngineType.Paper);

            Assert.NotNull(versions);
            Assert.NotEmpty(versions);
        }

        [Theory]
        [InlineData(ServerEngineType.Paper, "1.20.4", "https://api.papermc.io/v2/projects/paper/versions/1.20.4/builds/latest/downloads/paper-1.20.4-latest.jar")]
        [InlineData(ServerEngineType.Purpur, "1.20.4", "https://api.purpurmc.org/v2/purpur/1.20.4/latest/download")]
        public void BuildDownloadUrl_GeneratesValidUrl(ServerEngineType type, string version, string expectedSub)
        {
            var service = new EngineService();
            string url = service.BuildDownloadUrl(type, version);

            Assert.Equal(expectedSub, url);
        }
    }
}
