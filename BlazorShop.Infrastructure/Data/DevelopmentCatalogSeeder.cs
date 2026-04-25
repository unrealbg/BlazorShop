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
                new SeedCategory(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    "Sneakers",
                    "sneakers",
                    "Browse the published sneakers collection in the BlazorShop storefront.",
                    "Discover lightweight everyday footwear, performance runners, and featured sneaker drops."),
                new SeedCategory(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    "Outerwear",
                    "outerwear",
                    "Browse published jackets and layered essentials in the BlazorShop storefront.",
                    "Explore weather-ready layers and lightweight jackets curated for the public storefront."),
                new SeedCategory(
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    "Accessories",
                    "accessories",
                    "Browse published bags, caps, and accessories in the BlazorShop storefront.",
                    "Discover day-to-day accessories and compact essentials on route-based category pages.")
            };

            var categoryIds = categories.Select(category => category.Id).ToArray();
            var categoryNames = categories.Select(category => category.Name).ToArray();

            var existingCategories = await dbContext.Categories
                .Where(category => categoryIds.Contains(category.Id)
                    || (category.Name != null && categoryNames.Contains(category.Name)))
                .ToListAsync(cancellationToken);

            var resolvedCategoryIds = new Dictionary<Guid, Guid>();
            var hasChanges = false;

            foreach (var categorySeed in categories)
            {
                var existingCategory = existingCategories.FirstOrDefault(category => category.Id == categorySeed.Id)
                    ?? existingCategories.FirstOrDefault(category => string.Equals(category.Name, categorySeed.Name, StringComparison.OrdinalIgnoreCase));

                if (existingCategory is not null)
                {
                    hasChanges |= ApplyCategorySeed(existingCategory, categorySeed);
                    resolvedCategoryIds[categorySeed.Id] = existingCategory.Id;
                    continue;
                }

                var category = new Category
                {
                    Id = categorySeed.Id
                };

                ApplyCategorySeed(category, categorySeed);

                await dbContext.Categories.AddAsync(category, cancellationToken);
                existingCategories.Add(category);
                resolvedCategoryIds[categorySeed.Id] = category.Id;
                hasChanges = true;
            }

            var products = new[]
            {
                new SeedProduct(
                    Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
                    "Metro Runner",
                    "metro-runner",
                    "Lightweight daily sneakers for city walking.",
                    89.00m,
                    "/images/bg1.png",
                    18,
                    new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc),
                    categories[0].Id,
                    "Shop the Metro Runner on the BlazorShop storefront.",
                    "A lightweight daily sneaker built for route-based product discovery in the public storefront."),
                new SeedProduct(
                    Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"),
                    "Northline Jacket",
                    "northline-jacket",
                    "Wind-resistant jacket for cooler spring evenings.",
                    129.00m,
                    "/images/bg2.png",
                    9,
                    new DateTime(2026, 4, 9, 10, 0, 0, DateTimeKind.Utc),
                    categories[1].Id,
                    "Shop the Northline Jacket on the BlazorShop storefront.",
                    "A lightweight outerwear option seeded as a published public product for SSR route verification."),
                new SeedProduct(
                    Guid.Parse("cccccccc-3333-3333-3333-333333333333"),
                    "Canvas Weekender",
                    "canvas-weekender",
                    "Compact carry bag for everyday commuting.",
                    74.00m,
                    "/images/bg.png",
                    14,
                    new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc),
                    categories[2].Id,
                    "Shop the Canvas Weekender on the BlazorShop storefront.",
                    "A compact accessory seeded for published product route validation and public catalog cards."),
                new SeedProduct(
                    Guid.Parse("dddddddd-4444-4444-4444-444444444444"),
                    "Aero Street Cap",
                    "aero-street-cap",
                    "Minimal six-panel cap with soft cotton finish.",
                    29.00m,
                    "/images/banner-bg.jpg",
                    25,
                    new DateTime(2026, 4, 7, 10, 0, 0, DateTimeKind.Utc),
                    categories[2].Id,
                    "Shop the Aero Street Cap on the BlazorShop storefront.",
                    "A published accessories product used to validate public product pages and not-found handling.")
            };

            var productIds = products.Select(product => product.Id).ToArray();
            var existingProducts = await dbContext.Products
                .Where(product => productIds.Contains(product.Id))
                .ToListAsync(cancellationToken);

            var missingProducts = products
                .Where(product => existingProducts.All(existingProduct => existingProduct.Id != product.Id))
                .Select(product =>
                {
                    var seededProduct = new Product
                    {
                        Id = product.Id
                    };

                    ApplyProductSeed(seededProduct, product, resolvedCategoryIds[product.CategorySeedId]);
                    return seededProduct;
                })
                .ToList();

            foreach (var existingProduct in existingProducts)
            {
                var productSeed = products.First(product => product.Id == existingProduct.Id);
                hasChanges |= ApplyProductSeed(existingProduct, productSeed, resolvedCategoryIds[productSeed.CategorySeedId]);
            }

            if (!hasChanges && missingProducts.Count == 0)
            {
                return;
            }

            if (missingProducts.Count > 0)
            {
                await dbContext.Products.AddRangeAsync(missingProducts, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private static bool ApplyCategorySeed(Category category, SeedCategory seed)
        {
            var hasChanges = false;

            hasChanges |= AssignIfDifferent(() => category.Name, value => category.Name = value, seed.Name);
            hasChanges |= AssignIfDifferent(() => category.Slug, value => category.Slug = value, seed.Slug);
            hasChanges |= AssignIfDifferent(() => category.MetaTitle, value => category.MetaTitle = value, seed.Name);
            hasChanges |= AssignIfDifferent(() => category.MetaDescription, value => category.MetaDescription = value, seed.MetaDescription);
            hasChanges |= AssignIfDifferent(() => category.OgTitle, value => category.OgTitle = value, seed.Name);
            hasChanges |= AssignIfDifferent(() => category.OgDescription, value => category.OgDescription = value, seed.MetaDescription);
            hasChanges |= AssignIfDifferent(() => category.SeoContent, value => category.SeoContent = value, seed.SeoContent);

            if (!category.IsPublished)
            {
                category.IsPublished = true;
                hasChanges = true;
            }

            if (!category.RobotsIndex)
            {
                category.RobotsIndex = true;
                hasChanges = true;
            }

            if (!category.RobotsFollow)
            {
                category.RobotsFollow = true;
                hasChanges = true;
            }

            return hasChanges;
        }

        private static bool ApplyProductSeed(Product product, SeedProduct seed, Guid categoryId)
        {
            var hasChanges = false;

            hasChanges |= AssignIfDifferent(() => product.Name, value => product.Name = value, seed.Name);
            hasChanges |= AssignIfDifferent(() => product.Slug, value => product.Slug = value, seed.Slug);
            hasChanges |= AssignIfDifferent(() => product.Description, value => product.Description = value, seed.Description);
            hasChanges |= AssignIfDifferent(() => product.Price, value => product.Price = value, seed.Price);
            hasChanges |= AssignIfDifferent(() => product.Image, value => product.Image = value, seed.Image);
            hasChanges |= AssignIfDifferent(() => product.Quantity, value => product.Quantity = value, seed.Quantity);
            hasChanges |= AssignIfDifferent(() => product.CreatedOn, value => product.CreatedOn = value, seed.CreatedOn);
            hasChanges |= AssignIfDifferent(() => product.MetaTitle, value => product.MetaTitle = value, seed.Name);
            hasChanges |= AssignIfDifferent(() => product.MetaDescription, value => product.MetaDescription = value, seed.MetaDescription);
            hasChanges |= AssignIfDifferent(() => product.OgTitle, value => product.OgTitle = value, seed.Name);
            hasChanges |= AssignIfDifferent(() => product.OgDescription, value => product.OgDescription = value, seed.MetaDescription);
            hasChanges |= AssignIfDifferent(() => product.SeoContent, value => product.SeoContent = value, seed.SeoContent);

            if (product.CategoryId != categoryId)
            {
                product.CategoryId = categoryId;
                hasChanges = true;
            }

            if (!product.IsPublished)
            {
                product.IsPublished = true;
                hasChanges = true;
            }

            var publishedOn = seed.CreatedOn.AddHours(2);
            if (product.PublishedOn != publishedOn)
            {
                product.PublishedOn = publishedOn;
                hasChanges = true;
            }

            if (!product.RobotsIndex)
            {
                product.RobotsIndex = true;
                hasChanges = true;
            }

            if (!product.RobotsFollow)
            {
                product.RobotsFollow = true;
                hasChanges = true;
            }

            return hasChanges;
        }

        private static bool AssignIfDifferent<T>(Func<T> getter, Action<T> setter, T value)
            where T : IEquatable<T>
        {
            if (getter().Equals(value))
            {
                return false;
            }

            setter(value);
            return true;
        }

        private static bool AssignIfDifferent(Func<string?> getter, Action<string?> setter, string? value)
        {
            if (string.Equals(getter(), value, StringComparison.Ordinal))
            {
                return false;
            }

            setter(value);
            return true;
        }

        private sealed record SeedCategory(Guid Id, string Name, string Slug, string MetaDescription, string SeoContent);

        private sealed record SeedProduct(
            Guid Id,
            string Name,
            string Slug,
            string Description,
            decimal Price,
            string Image,
            int Quantity,
            DateTime CreatedOn,
            Guid CategorySeedId,
            string MetaDescription,
            string SeoContent);
    }
}