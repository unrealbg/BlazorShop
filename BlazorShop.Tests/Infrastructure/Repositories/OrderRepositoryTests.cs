namespace BlazorShop.Tests.Infrastructure.Repositories
{
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Repositories.Payment;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public sealed class OrderRepositoryTests
    {
        [Fact]
        public async Task GetByUserIdAsync_ReturnsOnlyOwnedOrdersWithPersistedLines()
        {
            await using var context = CreateContext();
            var ownOrder = CreateOrder("owner", "OWN", "Owned snapshot");
            var foreignOrder = CreateOrder("another-user", "FOREIGN", "Foreign snapshot");
            context.Orders.AddRange(ownOrder, foreignOrder);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            var repository = new OrderRepository(context);

            var result = await repository.GetByUserIdAsync("owner");

            var returnedOrder = Assert.Single(result);
            Assert.Equal(ownOrder.Id, returnedOrder.Id);
            Assert.Equal("OWN", returnedOrder.Reference);
            Assert.Equal("Owned snapshot", Assert.Single(returnedOrder.Lines).ProductNameSnapshot);
            Assert.DoesNotContain(result, order => order.Id == foreignOrder.Id);
        }

        private static Order CreateOrder(string userId, string reference, string productName)
        {
            return new Order
            {
                UserId = userId,
                Reference = reference,
                TotalAmount = 10m,
                SubtotalAmount = 10m,
                Currency = "EUR",
                Lines =
                [
                    new OrderLine
                    {
                        ProductId = Guid.NewGuid(),
                        ProductNameSnapshot = productName,
                        Quantity = 1,
                        UnitPrice = 10m,
                        LineTotal = 10m,
                    },
                ],
            };
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"order-repository-tests-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }
    }
}
