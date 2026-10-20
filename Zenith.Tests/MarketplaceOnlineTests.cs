using System.Threading.Tasks;
using Xunit;
using Zenith.Core.Models;
using Zenith.Core.Services;

namespace Zenith.Tests
{
    public class MarketplaceOnlineTests
    {
        [Fact]
        public async Task GetFeaturedItemsAsync_WhenOffline_ReturnsFallbackCatalog()
        {
            var service = new MarketplaceService();
            var items = await service.GetFeaturedItemsAsync(MarketplaceCategory.Plugin);

            Assert.NotNull(items);
            Assert.NotEmpty(items);
        }

        [Fact]
        public async Task GetFeaturedItemsAsync_WithSearchQuery_FiltersResults()
        {
            var service = new MarketplaceService();
            var items = await service.GetFeaturedItemsAsync(MarketplaceCategory.Plugin, searchQuery: "Essentials");

            Assert.NotNull(items);
            Assert.NotEmpty(items);
            Assert.Contains(items, i => i.Title.ToLower().Contains("essentials"));
        }
    }
}
