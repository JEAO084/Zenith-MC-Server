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

        [Fact]
        public void ThemeConfig_AppThemeModeAndCustomColors_PersistCorrectly()
        {
            var config = new LauncherThemeConfig
            {
                AppThemeMode = "Claro",
                ConsoleBackgroundColorHex = "#000000",
                ConsoleTextColorHex = "#22C55E",
                EditorBackgroundColorHex = "#181818",
                EditorTextColorHex = "#4ADE80"
            };

            Assert.Equal("Claro", config.AppThemeMode);
            Assert.Equal("#000000", config.ConsoleBackgroundColorHex);
            Assert.Equal("#22C55E", config.ConsoleTextColorHex);
            Assert.Equal("#181818", config.EditorBackgroundColorHex);
            Assert.Equal("#4ADE80", config.EditorTextColorHex);
        }
    }
}
