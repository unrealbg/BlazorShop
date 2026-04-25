namespace BlazorShop.Tests.Infrastructure.Services.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Admin.Orders;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Contracts.Payment;
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
