using System;
using System.IO;
using Xunit;
using Zenith.Core.Services;

namespace Zenith.Tests
{
    public class ServerConfigTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly ServerConfigService _configService;

        public ServerConfigTests()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "ZenithConfigTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
            _configService = new ServerConfigService();
        }

        [Fact]
        public void EulaManagement_TogglesEulaCorrectly()
        {
            Assert.False(_configService.CheckEulaAccepted(_tempFolder));

            _configService.SetEulaAccepted(_tempFolder, true);
            Assert.True(_configService.CheckEulaAccepted(_tempFolder));

            _configService.SetEulaAccepted(_tempFolder, false);
            Assert.False(_configService.CheckEulaAccepted(_tempFolder));
        }

        [Fact]
        public void LaunchArguments_GeneratedCorrectly()
        {
            string args = _configService.GetLaunchArguments("paper-1.20.4.jar", 8, "-XX:+UseG1GC");
            Assert.Contains("-Xms8G -Xmx8G", args);
            Assert.Contains("-XX:+UseG1GC", args);
            Assert.Contains("-jar \"paper-1.20.4.jar\" nogui", args);
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
