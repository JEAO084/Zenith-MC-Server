using Xunit;
using Zenith.Core.Models;
using Zenith.Core.Services;

namespace Zenith.Tests
{
    public class DynamicSuggestionsAndMarketplaceTests
    {
        [Fact]
        public void ServerProcessManager_RegistersAndFiltersDynamicSuggestions()
        {
            var manager = new ServerProcessManager();

            manager.RegisterDynamicCommand("/gamemode survival");
            manager.RegisterDynamicCommand("op Steve");
            manager.RegisterDynamicCommand("teleport Alex 0 100 0");

            var suggestionsG = manager.GetDynamicSuggestions("game");
            Assert.Single(suggestionsG);
            Assert.Equal("gamemode survival", suggestionsG[0]);

            var suggestionsOp = manager.GetDynamicSuggestions("op");
            Assert.Single(suggestionsOp);
            Assert.Equal("op Steve", suggestionsOp[0]);

            var allSuggestions = manager.GetDynamicSuggestions("");
            Assert.Equal(3, allSuggestions.Count);
        }

        [Fact]
        public void MarketplaceService_FiltersPluginsAndModsCorrectly()
        {
            var service = new MarketplaceService();

            var plugins = service.GetFeaturedItems(MarketplaceCategory.Plugin);
            Assert.All(plugins, item => Assert.Equal(MarketplaceCategory.Plugin, item.Category));

            var mods = service.GetFeaturedItems(MarketplaceCategory.Mod);
            Assert.All(mods, item => Assert.Equal(MarketplaceCategory.Mod, item.Category));
        }
    }
}
