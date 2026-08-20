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
            var firstVariantId = Guid.NewGuid();
            var secondVariantId = Guid.NewGuid();
            var carts = new[]
            {
                new ProcessCart { ProductId = productId, VariantId = firstVariantId, SizeValue = "tampered", Quantity = 1, UnitPrice = 0.01m },
                new ProcessCart { ProductId = productId, VariantId = secondVariantId, SizeValue = "tampered", Quantity = 2, UnitPrice = 0.01m },
            };
            var products = new[]
            {
                new GetProduct
                {
                    Id = productId,
                    Name = "Runner",
                    Image = "/img.png",
                    Price = 40m,
                    Variants =
                    [
                        new GetProductVariant { Id = firstVariantId, ProductId = productId, SizeValue = "41", Price = 50m, Stock = 5, Sku = "RUN-41" },
                        new GetProductVariant { Id = secondVariantId, ProductId = productId, SizeValue = "42", Price = 55m, Stock = 5, Sku = "RUN-42" },
                    ],
                },
            };

            var result = CheckoutCartLineMapper.Build(carts, products);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, line => line.SizeValue == "41" && line.Sku == "RUN-41" && line.Quantity == 1 && line.UnitPrice == 50m && !line.IsUnavailable);
            Assert.Contains(result, line => line.SizeValue == "42" && line.Sku == "RUN-42" && line.Quantity == 2 && line.UnitPrice == 55m && !line.IsUnavailable);
        }

        [Fact]
        public void Build_ProductOnlyLine_UsesCurrentServerProductPrice()
        {
            var productId = Guid.NewGuid();
            var carts = new[]
            {
                new ProcessCart { ProductId = productId, Quantity = 2, UnitPrice = 0.01m },
            };
            var products = new[]
            {
                new GetProduct { Id = productId, Name = "Hat", Price = 25m, Quantity = 5 },
            };

            var line = Assert.Single(CheckoutCartLineMapper.Build(carts, products));

            Assert.False(line.IsUnavailable);
            Assert.Null(line.VariantId);
            Assert.Equal(25m, line.UnitPrice);
            Assert.Equal(50m, line.LineTotal);
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
            Assert.Equal(0m, line.UnitPrice);
        }
    }
}
