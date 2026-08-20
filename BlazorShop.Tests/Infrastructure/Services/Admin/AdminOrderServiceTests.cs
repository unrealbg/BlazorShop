namespace BlazorShop.Tests.Infrastructure.Services.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Admin.Orders;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Services.Admin;

    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    public class AdminOrderServiceTests
    {
        [Fact]
        public async Task UpdateShippingStatusAsync_RejectsInvalidStatus()
        {
            await using var context = CreateContext();
            var service = CreateService(context);

            var result = await service.UpdateShippingStatusAsync(Guid.NewGuid(), new UpdateShippingStatusRequest { ShippingStatus = "Lost" });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.ValidationError, result.ResponseType);
        }

        [Fact]
        public async Task UpdateAdminNoteAsync_SavesNote()
        {
            await using var context = CreateContext();
            var order = new Order { Id = Guid.NewGuid(), Reference = "BS-1", UserId = "user-1" };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.UpdateAdminNoteAsync(order.Id, new UpdateOrderAdminNoteRequest { AdminNote = "Call before shipping" });

            Assert.True(result.Success);
            Assert.Equal("Call before shipping", (await context.Orders.FindAsync(order.Id))!.AdminNote);
        }

        [Fact]
        public async Task GetByIdAsync_UsesPurchaseSnapshotsAfterCatalogMutationAndRemoval()
        {
            await using var context = CreateContext();
            var category = new Category { Id = Guid.NewGuid(), Name = "Shoes" };
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Original Runner",
                Price = 80m,
                CategoryId = category.Id,
            };
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Sku = "ORIGINAL-SKU",
                SizeScale = SizeScale.ShoesUS,
                SizeValue = "10",
                Color = "Black",
                Price = 95m,
                Stock = 3,
            };
            var order = new Order
            {
                Id = Guid.NewGuid(),
                Reference = "BS-2",
                UserId = string.Empty,
                Lines =
                [
                    new OrderLine
                    {
                        ProductId = product.Id,
                        ProductVariantId = variant.Id,
                        ProductNameSnapshot = "Original Runner",
                        SkuSnapshot = "ORIGINAL-SKU",
                        SizeScaleSnapshot = "ShoesUS",
                        SizeValueSnapshot = "10",
                        ColorSnapshot = "Black",
                        Quantity = 1,
                        UnitPrice = 95m,
                        LineTotal = 95m,
                    },
                ],
            };
            context.AddRange(category, product, variant, order);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            product.Name = "Renamed Runner";
            product.Price = 1m;
            variant.Sku = "CHANGED-SKU";
            variant.SizeScale = SizeScale.ShoesUK;
            variant.SizeValue = "10";
            variant.Color = "White";
            variant.Price = 1m;
            await context.SaveChangesAsync();

            var resultAfterMutation = await service.GetByIdAsync(order.Id);

            AssertSnapshot(resultAfterMutation, variant.Id);

            context.ProductVariants.Remove(variant);
            context.Products.Remove(product);
            await context.SaveChangesAsync();

            var resultAfterRemoval = await service.GetByIdAsync(order.Id);

            AssertSnapshot(resultAfterRemoval, variant.Id);
        }

        private static void AssertSnapshot(ServiceResponse<GetOrder> result, Guid variantId)
        {
            Assert.True(result.Success);
            var line = Assert.Single(result.Payload!.Lines);
            Assert.Equal(variantId, line.VariantId);
            Assert.Equal("Original Runner", line.ProductName);
            Assert.Equal("ORIGINAL-SKU", line.Sku);
            Assert.Equal("ShoesUS", line.SizeScale);
            Assert.Equal("10", line.SizeValue);
            Assert.Equal("Black", line.Color);
            Assert.Equal(95m, line.UnitPrice);
            Assert.Equal(95m, line.LineTotal);
        }

        private static AdminOrderService CreateService(AppDbContext context)
        {
            var tracking = new Mock<IOrderTrackingService>();
            tracking.Setup(service => service.UpdateTrackingAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            tracking.Setup(service => service.UpdateShippingStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(true);

            var audit = new Mock<IAdminAuditService>();
            audit.Setup(service => service.LogAsync(It.IsAny<CreateAdminAuditLogDto>()))
                .ReturnsAsync(new ServiceResponse<AdminAuditLogDto>(true)
                {
                    Payload = new AdminAuditLogDto { Id = Guid.NewGuid() },
                    ResponseType = ServiceResponseType.Success,
                });

            return new AdminOrderService(context, tracking.Object, audit.Object);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"admin-orders-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }
    }
}
