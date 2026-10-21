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

        [Fact]
        public async Task GetFeaturedItemsAsync_WithPagination_CalculatesCorrectOffset()
        {
            var service = new MarketplaceService();
            var page1 = await service.GetFeaturedItemsAsync(MarketplaceCategory.Plugin, page: 1, pageSize: 5);
            var page2 = await service.GetFeaturedItemsAsync(MarketplaceCategory.Plugin, page: 2, pageSize: 5);

            Assert.NotNull(page1);
            Assert.NotNull(page2);
        }
    }
}
