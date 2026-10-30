namespace BlazorShop.Tests.Presentation.Payments
{
    using System.Text.Json;

    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Web.Shared.CookieStorage;
    using BlazorShop.Web.Shared.Models.Payment;

    using Xunit;

    public class CartCookieParserTests
    {
        public static IEnumerable<object[]> Cases()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "BlazorShop.sln")))
            {
                root = root.Parent;
            }

            Assert.NotNull(root);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "tests", "fixtures", "cart-cookie-cases.json")));
            foreach (var item in document.RootElement.EnumerateArray())
            {
                yield return
                [
                    item.GetProperty("name").GetString()!,
                    item.GetProperty("json").GetString()!.Replace("$product", "22222222-2222-2222-2222-222222222222", StringComparison.Ordinal)
                        .Replace("$other", "33333333-3333-3333-3333-333333333333", StringComparison.Ordinal),
                    item.GetProperty("items").GetInt32(),
                    item.GetProperty("quantity").GetInt64(),
                    item.GetProperty("invalid").GetBoolean(),
                ];
            }
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Parse_UsesExistingIdentityAndQuantityContract(string name, string json, int items, long quantity, bool invalid)
        {
            Assert.NotEmpty(name);
            var result = CartCookieParser.Parse(json);
            Assert.Equal(items, result.Items.Count);
            Assert.Equal(quantity, result.Items.Sum(item => (long)item.Quantity));
            Assert.Equal(invalid, result.HasInvalidItems);
            Assert.All(result.Items, item =>
            {
                Assert.NotEqual(Guid.Empty, item.ProductId);
                Assert.NotEqual(Guid.Empty, item.VariantId);
                Assert.True(item.Quantity > 0);
            });
        }

        [Fact]
        public void Parse_PreservesVariantsAndSafeMetadataWithoutTrustingPrices()
        {
            var product = Guid.NewGuid();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var json = $"[{{\"ProductId\":\"{product}\",\"VariantId\":\"{first}\",\"Quantity\":2,\"SizeValue\":\"10\",\"UnitPrice\":0.01}},{{\"ProductId\":\"{product}\",\"VariantId\":\"{second}\",\"Quantity\":3}}]";
            var result = CartCookieParser.Parse(json);
            Assert.False(result.HasInvalidItems);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal(first, result.Items[0].VariantId);
            Assert.Equal(second, result.Items[1].VariantId);
            Assert.Equal("10", result.Items[0].SizeValue);
            Assert.Equal(0.01m, result.Items[0].UnitPrice);
        }

        [Fact]
        public void Parse_DuplicateNormalizationRetainsCanonicalCheckoutIntent()
        {
            var product = Guid.NewGuid();
            var variant = Guid.NewGuid();
            var original = new BlazorShop.Application.DTOs.Payment.Checkout
            {
                PaymentMethodId = Guid.NewGuid(),
                Carts = [new CartLineRequest(product, variant, 2), new CartLineRequest(product, variant, 3)],
            };
            var parsed = CartCookieParser.Parse(JsonSerializer.Serialize(new[]
            {
                new ProcessCart { ProductId = product, VariantId = variant, Quantity = 2 },
                new ProcessCart { ProductId = product, VariantId = variant, Quantity = 3 },
            }));
            var normalized = new BlazorShop.Application.DTOs.Payment.Checkout
            {
                PaymentMethodId = original.PaymentMethodId,
                Carts = parsed.Items.Select(item => new CartLineRequest(item.ProductId, item.VariantId, item.Quantity)).ToArray(),
            };
            Assert.True(CheckoutIntentCanonicalizer.Canonicalize(original).IsValid);
            Assert.Equal(CheckoutIntentCanonicalizer.Canonicalize(original).Fingerprint, CheckoutIntentCanonicalizer.Canonicalize(normalized).Fingerprint);
        }
    }
}
