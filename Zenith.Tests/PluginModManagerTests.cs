using System.IO;
using Xunit;
using Zenith.Core.Services;

namespace Zenith.Tests
{
    public class PluginModManagerTests
    {
        [Fact]
        public void PluginModManager_ScansAndTogglesJarFiles()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZenithPluginTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            string pluginsDir = Path.Combine(tempDir, "plugins");
            Directory.CreateDirectory(pluginsDir);

            string sampleJar = Path.Combine(pluginsDir, "TestPlugin.jar");
            File.WriteAllText(sampleJar, "dummy content");

            var service = new PluginModManagerService();
            var items = service.GetItems(tempDir, true);

            Assert.Single(items);
            Assert.True(items[0].IsEnabled);

            bool newStatus = service.ToggleItem(sampleJar);
            Assert.False(newStatus);
            Assert.True(File.Exists(sampleJar + ".disabled"));

            Directory.Delete(tempDir, true);
        }
    }
}
