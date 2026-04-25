namespace BlazorShop.Tests.Infrastructure.Services.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Admin.Settings;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Services.Admin;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public class AdminSettingsServiceTests
    {
        [Fact]
        public async Task UpdateStoreAsync_RejectsInvalidCurrency()
        {
            await using var context = CreateContext();
            var service = CreateService(context);

            var result = await service.UpdateStoreAsync(new UpdateStoreSettingsDto
            {
                StoreName = "Shop",
                DefaultCurrency = "EURO",
                DefaultCulture = "en-US",
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.ValidationError, result.ResponseType);
        }

        [Fact]
        public async Task UpdateOrdersAsync_RejectsGuestCheckoutWhenUnsupported()
        {
            await using var context = CreateContext();
            var service = CreateService(context);

            var result = await service.UpdateOrdersAsync(new UpdateOrderSettingsDto
            {
                AllowGuestCheckout = true,
                DefaultShippingStatus = "PendingShipment",
                OrderReferencePrefix = "BS",
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.ValidationError, result.ResponseType);
        }

        private static AdminSettingsService CreateService(AppDbContext context)
        {
            var audit = new Mock<IAdminAuditService>();
            audit.Setup(service => service.LogAsync(It.IsAny<CreateAdminAuditLogDto>()))
                .ReturnsAsync(new ServiceResponse<AdminAuditLogDto>(true)
                {
                    Payload = new AdminAuditLogDto { Id = Guid.NewGuid() },
                    ResponseType = ServiceResponseType.Success,
                });

            return new AdminSettingsService(
                context,
                Options.Create(new EmailSettings()),
                new TestHostEnvironment(),
                new HttpContextAccessor(),
                audit.Object);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"admin-settings-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }

        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Testing";

            public string ApplicationName { get; set; } = "BlazorShop.Tests";

            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
