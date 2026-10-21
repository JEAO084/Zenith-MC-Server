using Xunit;
using Zenith.Core.Models;

namespace Zenith.Tests
{
    public class ServerPropertiesTests
    {
        [Fact]
        public void DefaultProperties_ContainsStandardKeys()
        {
            var config = new ServerPropertiesConfig();
            Assert.Equal("25565", config.GetProperty("server-port"));
            Assert.Equal("A Minecraft Server", config.GetProperty("motd"));
            Assert.Equal("20", config.GetProperty("max-players"));
            Assert.Equal("survival", config.GetProperty("gamemode"));
        }

        [Fact]
        public void LoadFromString_ParsesPropertiesCorrectly()
        {
            var config = new ServerPropertiesConfig();
            string content = @"
# Minecraft server properties
server-port=25570
motd=Servidor de Prueba Zenith
max-players=50
pvp=false
custom-plugin-setting=enabled
";
            config.LoadFromString(content);

            Assert.Equal("25570", config.GetProperty("server-port"));
            Assert.Equal("Servidor de Prueba Zenith", config.GetProperty("motd"));
            Assert.Equal("50", config.GetProperty("max-players"));
            Assert.Equal("false", config.GetProperty("pvp"));
            Assert.Equal("enabled", config.GetProperty("custom-plugin-setting"));
        }

        [Fact]
        public void SaveToString_OutputsValidFormat()
        {
            var config = new ServerPropertiesConfig();
            config.SetProperty("motd", "Mi Servidor Zenith");
            config.SetProperty("pvp", "false");

            string result = config.SaveToString();

            Assert.Contains("motd=Mi Servidor Zenith", result);
            Assert.Contains("pvp=false", result);
            Assert.Contains("# Minecraft server properties", result);
        }
    }
}
