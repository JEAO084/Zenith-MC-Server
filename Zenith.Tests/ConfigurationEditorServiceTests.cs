using System.Collections.Generic;
using Zenith.Core.Models;
using Zenith.Core.Parsers;
using Zenith.Core.Services;
using Xunit;

namespace Zenith.Tests
{
    public class ConfigurationEditorServiceTests
    {
        [Fact]
        public void ParseServerProperties_ParsesKeysTypesAndComments()
        {
            string content = @"# Minecraft server properties
pvp=true
max-players=20
difficulty=normal
motd=§aWelcome to Zenith Server!
";

            var service = new ConfigurationEditorService();
            var props = service.ParseServerProperties(content);

            Assert.Equal(4, props.Count);

            var pvpProp = props.Find(p => p.Key == "pvp");
            Assert.NotNull(pvpProp);
            Assert.Equal(ConfigValueType.Boolean, pvpProp.ValueType);
            Assert.Equal("true", pvpProp.Value);

            var maxPlayersProp = props.Find(p => p.Key == "max-players");
            Assert.NotNull(maxPlayersProp);
            Assert.Equal(ConfigValueType.Number, maxPlayersProp.ValueType);
            Assert.Equal("20", maxPlayersProp.Value);

            var diffProp = props.Find(p => p.Key == "difficulty");
            Assert.NotNull(diffProp);
            Assert.Equal(ConfigValueType.Enum, diffProp.ValueType);
            Assert.Contains("normal", diffProp.EnumOptions);
        }

        [Fact]
        public void MinecraftMotdParser_ParsesColorCodesCorrectly()
        {
            string rawMotd = "&aGreen &cRed &lBoldText";
            var segments = MinecraftMotdParser.ParseMotd(rawMotd);

            Assert.NotEmpty(segments);
            Assert.Equal("#55FF55", segments[0].ColorHex);
            Assert.Equal("Green ", segments[0].Text);

            Assert.Equal("#FF5555", segments[1].ColorHex);
            Assert.Equal("Red ", segments[1].Text);

            Assert.True(segments[2].IsBold);
            Assert.Equal("BoldText", segments[2].Text);
        }

        [Fact]
        public void SaveServerProperties_PreservesCommentsAndUpdatesValues()
        {
            string original = @"# General settings
pvp=true
max-players=20
";
            var service = new ConfigurationEditorService();
            var props = service.ParseServerProperties(original);

            var maxPlayersProp = props.Find(p => p.Key == "max-players");
            if (maxPlayersProp != null) maxPlayersProp.Value = "50";

            string saved = service.SaveServerProperties(original, props);

            Assert.Contains("# General settings", saved);
            Assert.Contains("pvp=true", saved);
            Assert.Contains("max-players=50", saved);
        }
    }
}
