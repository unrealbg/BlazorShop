namespace BlazorShop.Tests.Presentation.Payments
{
    using BlazorShop.Web.Pages.Payments;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;

    using Xunit;

    public class CheckoutCartLineMapperTests
    {
        [Fact]
        public void Build_PreservesSeparateVariantLines_ForSameProduct()
        {
            var productId = Guid.NewGuid();
            var carts = new[]
            {
                new ProcessCart { ProductId = productId, VariantId = Guid.NewGuid(), SizeValue = "41", Quantity = 1, UnitPrice = 50m },
                new ProcessCart { ProductId = productId, VariantId = Guid.NewGuid(), SizeValue = "42", Quantity = 2, UnitPrice = 55m },
            };
            var products = new[]
            {
                new GetProduct { Id = productId, Name = "Runner", Image = "/img.png", Price = 40m },
            };

            var result = CheckoutCartLineMapper.Build(carts, products);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, line => line.SizeValue == "41" && line.Quantity == 1 && line.UnitPrice == 50m);
            Assert.Contains(result, line => line.SizeValue == "42" && line.Quantity == 2 && line.UnitPrice == 55m);
        }

        [Fact]
        public void Build_WhenProductIsMissing_ReturnsUnavailableLine()
        {
            var productId = Guid.NewGuid();
            var carts = new[]
            {
                new ProcessCart { ProductId = productId, VariantId = null, Quantity = 1, UnitPrice = 19.95m },
            };

            var result = CheckoutCartLineMapper.Build(carts, Array.Empty<GetProduct>());

            var line = Assert.Single(result);
            Assert.True(line.IsUnavailable);
            Assert.Equal("Unavailable item", line.DisplayName);
            Assert.Equal(19.95m, line.UnitPrice);
        }
    }
}