using System.IO;
using System.Threading.Tasks;
using Zenith.Core.Services;
using Xunit;

namespace Zenith.Tests
{
    public class JavaManagementServiceTests
    {
        [Theory]
        [InlineData("1.12.2", 8)]
        [InlineData("1.16.5", 8)]
        [InlineData("1.17.1", 17)]
        [InlineData("1.20.4", 17)]
        [InlineData("1.20.5", 21)]
        [InlineData("1.21.1", 21)]
        public void DetermineRequiredJavaVersion_MatchesMinecraftVersionRequirements(string mcVersion, int expectedJava)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ZenithJava_" + System.Guid.NewGuid().ToString("N"));
            var service = new JavaManagementService(tempDir);

            int javaVersion = service.DetermineRequiredJavaVersion(mcVersion);
            Assert.Equal(expectedJava, javaVersion);
        }

        [Fact]
        public async Task EnsureJavaRuntimeAsync_IsolatesRuntimeInInternalFolder()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ZenithJava_" + System.Guid.NewGuid().ToString("N"));
            var service = new JavaManagementService(tempDir);

            try
            {
                var runtime = await service.EnsureJavaRuntimeAsync(17);

                Assert.NotNull(runtime);
                Assert.Equal(17, runtime.MajorVersion);
                Assert.True(runtime.IsInstalled);
                Assert.True(Directory.Exists(runtime.InstallationDirectory));
                Assert.StartsWith(tempDir, runtime.InstallationDirectory);
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
