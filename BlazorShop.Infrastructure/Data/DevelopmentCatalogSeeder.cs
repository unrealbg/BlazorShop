namespace BlazorShop.Infrastructure.Data
{
    using BlazorShop.Domain.Entities;

    using Microsoft.EntityFrameworkCore;

    public static class DevelopmentCatalogSeeder
    {
        public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
        {
            var categories = new[]
            {
                new SeedCategory(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Sneakers"),
                new SeedCategory(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Outerwear"),
                new SeedCategory(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Accessories")
            };

            var categoryIds = categories.Select(category => category.Id).ToArray();
            var categoryNames = categories.Select(category => category.Name).ToArray();

            var existingCategories = await dbContext.Categories
                .Where(category => categoryIds.Contains(category.Id)
                    || (category.Name != null && categoryNames.Contains(category.Name)))
                .ToListAsync(cancellationToken);

            var resolvedCategoryIds = new Dictionary<Guid, Guid>();
            var hasNewCategories = false;

            foreach (var categorySeed in categories)
            {
                var existingCategory = existingCategories.FirstOrDefault(category => category.Id == categorySeed.Id)
                    ?? existingCategories.FirstOrDefault(category => string.Equals(category.Name, categorySeed.Name, StringComparison.OrdinalIgnoreCase));

                if (existingCategory is not null)
                {
                    resolvedCategoryIds[categorySeed.Id] = existingCategory.Id;
                    continue;
                }

                var category = new Category
                {
                    Id = categorySeed.Id,
                    Name = categorySeed.Name
                };

                await dbContext.Categories.AddAsync(category, cancellationToken);
                existingCategories.Add(category);
                resolvedCategoryIds[categorySeed.Id] = category.Id;
                hasNewCategories = true;
            }

            var products = new[]
            {
                new SeedProduct(
                    Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
                    "Metro Runner",
                    "Lightweight daily sneakers for city walking.",
                    89.00m,
                    "/images/bg1.png",
                    18,
                    new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc),
                    categories[0].Id),
                new SeedProduct(
                    Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"),
                    "Northline Jacket",
                    "Wind-resistant jacket for cooler spring evenings.",
                    129.00m,
                    "/images/bg2.png",
                    9,
                    new DateTime(2026, 4, 9, 10, 0, 0, DateTimeKind.Utc),
                    categories[1].Id),
                new SeedProduct(
                    Guid.Parse("cccccccc-3333-3333-3333-333333333333"),
                    "Canvas Weekender",
                    "Compact carry bag for everyday commuting.",
                    74.00m,
                    "/images/bg.png",
                    14,
                    new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc),
                    categories[2].Id),
                new SeedProduct(
                    Guid.Parse("dddddddd-4444-4444-4444-444444444444"),
                    "Aero Street Cap",
                    "Minimal six-panel cap with soft cotton finish.",
                    29.00m,
                    "/images/banner-bg.jpg",
                    25,
                    new DateTime(2026, 4, 7, 10, 0, 0, DateTimeKind.Utc),
                    categories[2].Id)
            };

            var productIds = products.Select(product => product.Id).ToArray();
            var existingProductIds = await dbContext.Products
                .Where(product => productIds.Contains(product.Id))
                .Select(product => product.Id)
                .ToListAsync(cancellationToken);

            var missingProducts = products
                .Where(product => !existingProductIds.Contains(product.Id))
                .Select(product => new Product
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Image = product.Image,
                    Quantity = product.Quantity,
                    CreatedOn = product.CreatedOn,
                    CategoryId = resolvedCategoryIds[product.CategorySeedId]
                })
                .ToList();

            if (!hasNewCategories && missingProducts.Count == 0)
            {
                return;
            }

            if (missingProducts.Count > 0)
            {
                await dbContext.Products.AddRangeAsync(missingProducts, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private sealed record SeedCategory(Guid Id, string Name);

        private sealed record SeedProduct(
            Guid Id,
            string Name,
            string Description,
            decimal Price,
            string Image,
            int Quantity,
            DateTime CreatedOn,
            Guid CategorySeedId);
    }
}