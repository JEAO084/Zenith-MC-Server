using Xunit;
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
            Assert.Contains(versions, v => v.MajorVersion == 21);
            Assert.Contains(versions, v => v.MajorVersion == 17);
            Assert.Contains(versions, v => v.MajorVersion == 8);
        }

        [Fact]
        public void ServerDownloadService_ReturnsPresetJars()
        {
            var service = new ServerDownloadService();
            var jars = service.GetPresetServerJars();

            Assert.NotEmpty(jars);
            Assert.Contains(jars, j => j.SoftwareType == Core.Models.ServerSoftwareType.Paper);
            Assert.Contains(jars, j => j.SoftwareType == Core.Models.ServerSoftwareType.Purpur);
            Assert.Contains(jars, j => j.SoftwareType == Core.Models.ServerSoftwareType.Vanilla);
            Assert.Contains(jars, j => j.SoftwareType == Core.Models.ServerSoftwareType.Spigot);
        }
    }
}
