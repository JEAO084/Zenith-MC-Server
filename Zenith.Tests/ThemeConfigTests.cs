using Xunit;
using Zenith.Core.Models;

namespace Zenith.Tests
{
    public class ThemeConfigTests
    {
        [Fact]
        public void ThemeConfig_DefaultValuesAndSerialization_BehavesCorrectly()
        {
            var config = new LauncherThemeConfig();

            Assert.Equal("#7C3AED", config.AccentColorHex);
            Assert.Equal("AcrylicThin", config.WindowBackdropType);
            Assert.Equal("Consolas", config.ConsoleFontFamily);
            Assert.Equal(10, config.CardCornerRadius);
        }
    }
}
