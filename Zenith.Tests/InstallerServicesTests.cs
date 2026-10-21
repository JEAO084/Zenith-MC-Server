using Xunit;
using Zenith.Core.Models;
using Zenith.Core.Services;

namespace Zenith.Tests
{
    public class InstallerServicesTests
    {
        [Fact]
        public void JavaInstallerService_ReturnsPresetVersions()
        {
            var service = new JavaInstallerService();
            var versions = service.GetAvailableJavaVersions();

            Assert.NotEmpty(versions);
            Assert.Contains(versions, v => v.MajorVersion == 25);
            Assert.Contains(versions, v => v.MajorVersion == 22);
            Assert.Contains(versions, v => v.MajorVersion == 21);
            Assert.Contains(versions, v => v.MajorVersion == 17);
            Assert.Contains(versions, v => v.MajorVersion == 8);
        }

        [Fact]
        public void ServerDownloadService_AccuratelyMapsJavaVersions()
        {
            var service = new ServerDownloadService();

            // Experimental versions (26.x) -> Java 25
            Assert.Equal(25, service.GetRecommendedJavaVersion("26.3"));

            // Snapshots -> Java 22
            Assert.Equal(22, service.GetRecommendedJavaVersion("24w14a"));

            // 1.20.5 - 1.21 -> Java 21
            Assert.Equal(21, service.GetRecommendedJavaVersion("1.20.5"));
            Assert.Equal(21, service.GetRecommendedJavaVersion("1.21"));

            // 1.17 - 1.20.4 -> Java 17
            Assert.Equal(17, service.GetRecommendedJavaVersion("1.20.4"));
            Assert.Equal(17, service.GetRecommendedJavaVersion("1.18.2"));

            // <= 1.12.2 -> Java 8
            Assert.Equal(8, service.GetRecommendedJavaVersion("1.12.2"));
            Assert.Equal(8, service.GetRecommendedJavaVersion("1.8.9"));
        }

        [Fact]
        public void ServerDownloadService_ReturnsPresetJars()
        {
            var service = new ServerDownloadService();
            var jars = service.GetPresetServerJars();

            Assert.NotEmpty(jars);
            Assert.Contains(jars, j => j.SoftwareType == ServerSoftwareType.Paper);
            Assert.Contains(jars, j => j.SoftwareType == ServerSoftwareType.Purpur);
            Assert.Contains(jars, j => j.SoftwareType == ServerSoftwareType.Vanilla);
            Assert.Contains(jars, j => j.SoftwareType == ServerSoftwareType.Spigot);
        }
    }
}
