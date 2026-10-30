namespace BlazorShop.Tests.Presentation.Payments
{
    using System.Net;
    using System.Reflection;

    using BlazorShop.Web.Pages.Payments;
    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared.CookieStorage.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Services.Contracts;

    using Moq;

    using Xunit;

    public class CartCookieCheckoutTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Theory]
        [MemberData(nameof(CartCookieParserTests.Cases), MemberType = typeof(CartCookieParserTests))]
        public async Task LoadCart_UsesSameSafeRowsAsSsrWithoutCookieMutation(string name, string json, int items, long quantity, bool invalid)
        {
            Assert.NotEmpty(name);
            var productId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var products = new Mock<IProductService>();
            products.Setup(service => service.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) =>
                QueryResult<GetProduct>.Succeeded(new GetProduct { Id = id, Name = "Server product", Price = 12.50m, Quantity = int.MaxValue }));
            var cookies = new Mock<IBrowserCookieStorageService>();
            cookies.Setup(service => service.GetAsync("my-cart")).ReturnsAsync(json);
            var cart = CreateCart(cookies, products);

            await LoadAsync(cart);

            var lines = (IReadOnlyList<CheckoutCartLine>)typeof(Cart).GetField("_cartLines", PrivateInstance)!.GetValue(cart)!;
            Assert.Equal(items, lines.Count);
            Assert.Equal(quantity, lines.Sum(line => (long)line.Quantity));
            Assert.All(lines, line => Assert.Equal(12.50m, line.UnitPrice));
            Assert.Equal(invalid, typeof(Cart).GetField("_cartCookieWarning", PrivateInstance)!.GetValue(cart) is not null);
            Assert.Null(typeof(Cart).GetField("_cartLoadError", PrivateInstance)!.GetValue(cart));
            Assert.False((bool)typeof(Cart).GetField("_isCartLoading", PrivateInstance)!.GetValue(cart)!);
            cookies.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            cookies.Verify(service => service.RemoveAsync(It.IsAny<string>()), Times.Never);
            Assert.All(lines.Where(line => line.ProductId == productId), line => Assert.True(line.Quantity > 0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LoadCart_CatalogFailuresRetainExistingRecovery(bool unavailable)
        {
            var product = Guid.NewGuid();
            var cookies = new Mock<IBrowserCookieStorageService>();
            cookies.Setup(service => service.GetAsync("my-cart")).ReturnsAsync($"[null,{{\"ProductId\":\"{product}\",\"Quantity\":2}}]");
            var products = new Mock<IProductService>();
            products.Setup(service => service.GetByIdAsync(product)).ReturnsAsync(QueryResult<GetProduct>.Failed("Catalog unavailable", unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NotFound));
            var cart = CreateCart(cookies, products);
            await LoadAsync(cart);
            var lines = (IReadOnlyList<CheckoutCartLine>)typeof(Cart).GetField("_cartLines", PrivateInstance)!.GetValue(cart)!;
            if (unavailable)
            {
                Assert.Empty(lines);
                Assert.NotNull(typeof(Cart).GetField("_cartLoadError", PrivateInstance)!.GetValue(cart));
            }
            else
            {
                Assert.True(Assert.Single(lines).IsUnavailable);
            }
        }

        [Fact]
        public async Task LoadCart_UnrepresentablePriceShowsWarningWithoutChangingQuantity()
        {
            var product = Guid.NewGuid();
            var cookies = new Mock<IBrowserCookieStorageService>();
            cookies.Setup(service => service.GetAsync("my-cart")).ReturnsAsync($"[{{\"ProductId\":\"{product}\",\"Quantity\":2147483647,\"UnitPrice\":0.01}}]");
            var products = new Mock<IProductService>();
            products.Setup(service => service.GetByIdAsync(product)).ReturnsAsync(QueryResult<GetProduct>.Succeeded(new GetProduct { Id = product, Price = decimal.MaxValue }));
            var cart = CreateCart(cookies, products);
            await LoadAsync(cart);
            Assert.True((bool)typeof(Cart).GetField("_hasTotalOverflow", PrivateInstance)!.GetValue(cart)!);
            Assert.Equal("Unavailable", typeof(Cart).GetProperty("CartGrandTotalDisplay", PrivateInstance)!.GetValue(cart));
            var line = Assert.Single((IReadOnlyList<CheckoutCartLine>)typeof(Cart).GetField("_cartLines", PrivateInstance)!.GetValue(cart)!);
            Assert.Equal(int.MaxValue, line.Quantity);
            Assert.Equal(decimal.MaxValue, line.UnitPrice);
            Assert.Equal("Unavailable", line.LineTotalDisplay);
        }

        private static Cart CreateCart(Mock<IBrowserCookieStorageService> cookies, Mock<IProductService> products)
        {
            var cart = new Cart();
            var methods = new Mock<IPaymentMethodService>();
            methods.Setup(service => service.GetPaymentMethods()).ReturnsAsync(QueryResult<IEnumerable<GetPaymentMethod>>.Succeeded([]));
            typeof(Cart).GetProperty("CookieStorageService", PrivateInstance)!.SetValue(cart, cookies.Object);
            typeof(Cart).GetProperty("ProductService", PrivateInstance)!.SetValue(cart, products.Object);
            typeof(Cart).GetProperty("PaymentMethodService", PrivateInstance)!.SetValue(cart, methods.Object);
            typeof(Cart).GetProperty("QueryFailureNotifier", PrivateInstance)!.SetValue(cart, new QueryFailureNotifier(Mock.Of<INotificationService>()));
            return cart;
        }

        private static Task LoadAsync(Cart cart) => (Task)typeof(Cart).GetMethod("LoadCartAsync", PrivateInstance)!.Invoke(cart, null)!;
    }
}
