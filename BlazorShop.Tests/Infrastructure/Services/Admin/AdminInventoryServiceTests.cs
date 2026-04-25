namespace BlazorShop.Tests.Infrastructure.Services.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Admin.Inventory;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Services.Admin;

    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    public class AdminInventoryServiceTests
    {
        [Fact]
        public async Task GetAsync_FiltersLowStockProducts()
        {
            await using var context = CreateContext();
            var category = new Category { Id = Guid.NewGuid(), Name = "Shoes" };
            var product = new Product { Id = Guid.NewGuid(), Name = "Low shoe", Quantity = 2, CategoryId = category.Id, Category = category };
            context.Categories.Add(category);
            context.Products.Add(product);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.GetAsync(new AdminInventoryQueryDto { LowStockOnly = true, LowStockThreshold = 5 });

            Assert.Single(result.Items);
            Assert.True(result.Items[0].IsLowStock);
        }

        [Fact]
        public async Task UpdateVariantStockAsync_UpdatesVariantStock()
        {
            await using var context = CreateContext();
            var category = new Category { Id = Guid.NewGuid(), Name = "Shoes" };
            var product = new Product { Id = Guid.NewGuid(), Name = "Variant shoe", Quantity = 10, CategoryId = category.Id, Category = category };
            var variant = new ProductVariant { Id = Guid.NewGuid(), ProductId = product.Id, Product = product, SizeValue = "42", Stock = 1 };
            context.Categories.Add(category);
            context.Products.Add(product);
            context.ProductVariants.Add(variant);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.UpdateVariantStockAsync(variant.Id, new UpdateVariantStockDto { Stock = 8 });

            Assert.True(result.Success);
            Assert.Equal(8, (await context.ProductVariants.FindAsync(variant.Id))!.Stock);
        }

        private static AdminInventoryService CreateService(AppDbContext context)
        {
            var audit = new Mock<IAdminAuditService>();
            audit.Setup(service => service.LogAsync(It.IsAny<CreateAdminAuditLogDto>()))
                .ReturnsAsync(new ServiceResponse<AdminAuditLogDto>(true)
                {
                    Payload = new AdminAuditLogDto { Id = Guid.NewGuid() },
                    ResponseType = ServiceResponseType.Success,
                });

            return new AdminInventoryService(context, audit.Object);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"admin-inventory-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }
    }
}
