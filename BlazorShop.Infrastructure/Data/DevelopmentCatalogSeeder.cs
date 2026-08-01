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
                    "Discover day-to-day accessories and compact essentials on route-based category pages."),
                new SeedCategory(
                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    "Apparel",
                    "apparel",
                    "Browse premium T-shirts, knit polos, and versatile everyday clothing in the BlazorShop storefront.",
                    "Discover comfortable wardrobe foundations made with considered fabrics, clean silhouettes, and easy everyday versatility."),
                new SeedCategory(
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    "Watches",
                    "watches",
                    "Browse refined field watches and modern chronographs in the BlazorShop storefront.",
                    "Explore purposeful timepieces that pair durable materials, clean dials, and contemporary everyday styling."),
                new SeedCategory(
                    Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    "Eyewear",
                    "eyewear",
                    "Browse optical frames and sunglasses designed for effortless everyday wear.",
                    "Discover polished acetate frames, lightweight materials, and timeless shapes for workdays, weekends, and travel.")
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
                    "Lightweight city sneakers with breathable mesh, supportive overlays, and responsive cushioning for long days on the move.",
                    89.00m,
                    "/images/products/metro-runner.webp",
                    24,
                    new DateTime(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc),
                    categories[0].Id,
                    "Meet Metro Runner, a lightweight everyday sneaker with breathable mesh, stable support, and soft cushioning for city miles.",
                    "Metro Runner balances breathable technical mesh with a stable heel structure and a sculpted foam sole. It is designed for commutes, travel days, and easy everyday mileage."),
                new SeedProduct(
                    Guid.Parse("eeeeeeee-5555-5555-5555-555555555555"),
                    "Drift Court",
                    "drift-court",
                    "A refined court sneaker combining crisp leather, soft suede panels, and a durable gum sole for effortless everyday wear.",
                    94.00m,
                    "/images/products/drift-court.webp",
                    16,
                    new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
                    categories[0].Id,
                    "Shop Drift Court, a premium everyday sneaker with clean leather, forest-green suede, and a classic gum outsole.",
                    "Drift Court refreshes a familiar court silhouette with tactile suede, smooth leather, and a warm gum outsole. The balanced construction works equally well with relaxed tailoring or weekend denim."),
                new SeedProduct(
                    Guid.Parse("ffffffff-6666-6666-6666-666666666666"),
                    "Terra Pace",
                    "terra-pace",
                    "A capable trail sneaker with ripstop panels, a protective toe guard, and a grippy lugged outsole for mixed terrain.",
                    118.00m,
                    "/images/products/terra-pace.webp",
                    12,
                    new DateTime(2026, 7, 28, 14, 0, 0, DateTimeKind.Utc),
                    categories[0].Id,
                    "Discover Terra Pace, a lightweight trail sneaker with durable ripstop fabric, protective overlays, and confident traction.",
                    "Terra Pace is built for routes that move between pavement, park paths, and weekend trails. Its ripstop upper keeps weight low while the reinforced toe and lugged outsole add protection and grip."),
                new SeedProduct(
                    Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"),
                    "Northline Jacket",
                    "northline-jacket",
                    "A clean, wind-resistant city layer with a high collar, secure zip pockets, and a lightweight feel for changing weather.",
                    129.00m,
                    "/images/products/northline-jacket.webp",
                    14,
                    new DateTime(2026, 7, 31, 11, 0, 0, DateTimeKind.Utc),
                    categories[1].Id,
                    "Shop Northline Jacket, a lightweight wind-resistant layer with a streamlined fit, high collar, and secure everyday storage.",
                    "Northline Jacket is a versatile outer layer for cool commutes and breezy evenings. Matte technical fabric, a protective collar, and low-profile zipped pockets keep the silhouette practical and polished."),
                new SeedProduct(
                    Guid.Parse("99999999-7777-7777-7777-777777777777"),
                    "Harbor Shell",
                    "harbor-shell",
                    "A packable hooded rain shell with waterproof zippers, adjustable coverage, and a breathable lightweight construction.",
                    149.00m,
                    "/images/products/harbor-shell.webp",
                    11,
                    new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc),
                    categories[1].Id,
                    "Explore Harbor Shell, a lightweight packable rain jacket with an adjustable hood and weather-ready waterproof details.",
                    "Harbor Shell offers dependable coverage without the bulk of a heavy jacket. The adjustable hood, coated zippers, and lightly structured fabric make it easy to pack for uncertain forecasts."),
                new SeedProduct(
                    Guid.Parse("88888888-8888-8888-8888-888888888888"),
                    "Alpine Grid Overshirt",
                    "alpine-grid-overshirt",
                    "A warm snap-front overshirt with a subtle woven grid, light insulation, and practical chest pockets for easy layering.",
                    139.00m,
                    "/images/products/alpine-grid-overshirt.webp",
                    8,
                    new DateTime(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc),
                    categories[1].Id,
                    "Layer up with Alpine Grid Overshirt, a lightly insulated woven shirt-jacket with practical pockets and a refined outdoor finish.",
                    "Alpine Grid Overshirt bridges the gap between a shirt and a jacket. Its brushed grid fabric, light quilted lining, and metal snap closure bring warmth and structure without feeling heavy."),
                new SeedProduct(
                    Guid.Parse("cccccccc-3333-3333-3333-333333333333"),
                    "Canvas Weekender",
                    "canvas-weekender",
                    "A structured canvas duffel with reinforced handles, a roomy main compartment, and a removable shoulder strap for short trips.",
                    74.00m,
                    "/images/products/canvas-weekender.webp",
                    18,
                    new DateTime(2026, 7, 26, 9, 0, 0, DateTimeKind.Utc),
                    categories[2].Id,
                    "Pack the Canvas Weekender, a compact travel duffel with durable woven canvas, reinforced handles, and flexible carry options.",
                    "Canvas Weekender keeps overnight packing simple with a wide zip opening and a structured, easy-to-carry shape. Reinforced corners and antique brass hardware give it a timeless travel character."),
                new SeedProduct(
                    Guid.Parse("dddddddd-4444-4444-4444-444444444444"),
                    "Aero Street Cap",
                    "aero-street-cap",
                    "A minimal six-panel cap in brushed cotton twill with a curved brim, tonal stitching, and an adjustable back strap.",
                    29.00m,
                    "/images/products/aero-street-cap.webp",
                    32,
                    new DateTime(2026, 7, 25, 15, 0, 0, DateTimeKind.Utc),
                    categories[2].Id,
                    "Complete an everyday look with Aero Street Cap, a clean charcoal six-panel design made from soft cotton twill.",
                    "Aero Street Cap focuses on shape, texture, and comfortable daily wear. The blank front, tonal seams, curved brim, and adjustable strap create a versatile finish without unnecessary branding."),
                new SeedProduct(
                    Guid.Parse("77777777-9999-9999-9999-999999999999"),
                    "Meridian Daypack",
                    "meridian-daypack",
                    "A streamlined 22-liter backpack with padded straps, organized storage, and durable technical fabric for workdays and day trips.",
                    96.00m,
                    "/images/products/meridian-daypack.webp",
                    20,
                    new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc),
                    categories[2].Id,
                    "Carry more comfortably with Meridian Daypack, a streamlined 22-liter backpack with organized pockets and padded support.",
                    "Meridian Daypack keeps daily essentials organized in a compact 22-liter profile. Padded mesh straps, bottle pockets, a reinforced base, and an accessible front organizer make it ready for commuting or day travel."),
                new SeedProduct(
                    Guid.Parse("10000000-0000-0000-0000-000000000001"),
                    "Coastline Tee",
                    "coastline-tee",
                    "A premium heavyweight cotton T-shirt with a relaxed shape, soft garment finish, and clean ocean-blue color for effortless daily wear.",
                    42.00m,
                    "/images/products/coastline-tee.webp",
                    36,
                    new DateTime(2026, 8, 1, 13, 0, 0, DateTimeKind.Utc),
                    categories[3].Id,
                    "Shop Coastline Tee, a premium heavyweight cotton T-shirt with a relaxed fit and soft ocean-blue garment finish.",
                    "Coastline Tee is cut from substantial, soft-touch cotton that holds its shape while remaining comfortable throughout the day. The clean crew neck and relaxed proportions make it an easy foundation for layered or minimal looks."),
                new SeedProduct(
                    Guid.Parse("10000000-0000-0000-0000-000000000002"),
                    "Atlas Knit Polo",
                    "atlas-knit-polo",
                    "A textured short-sleeve knit polo with an open collar, contrast tipping, and a polished shape that moves easily between casual and refined looks.",
                    68.00m,
                    "/images/products/atlas-knit-polo.webp",
                    18,
                    new DateTime(2026, 8, 1, 13, 15, 0, DateTimeKind.Utc),
                    categories[3].Id,
                    "Discover Atlas Knit Polo, a textured sand knit with an open collar and understated contrast tipping.",
                    "Atlas Knit Polo combines breathable textured yarn with a softly structured silhouette. Its open collar and subtle contrast edges give a familiar warm-weather layer a smarter, more considered finish."),
                new SeedProduct(
                    Guid.Parse("10000000-0000-0000-0000-000000000003"),
                    "Meridian Field Watch",
                    "meridian-field-watch",
                    "A clean stainless steel field watch with a deep navy dial and warm leather strap, designed for reliable everyday timekeeping.",
                    159.00m,
                    "/images/products/meridian-field-watch.webp",
                    10,
                    new DateTime(2026, 8, 1, 13, 30, 0, DateTimeKind.Utc),
                    categories[4].Id,
                    "Shop Meridian Field Watch, a refined stainless steel timepiece with a navy dial and tan leather strap.",
                    "Meridian Field Watch favors clarity and dependable materials over unnecessary detail. A brushed steel case, high-contrast markers, and supple leather strap create a versatile timepiece for work, travel, and weekends."),
                new SeedProduct(
                    Guid.Parse("10000000-0000-0000-0000-000000000004"),
                    "Orbit Chronograph",
                    "orbit-chronograph",
                    "A modern steel chronograph with a charcoal dial, understated subdials, and a durable forest-green strap for an athletic everyday finish.",
                    219.00m,
                    "/images/products/orbit-chronograph.webp",
                    7,
                    new DateTime(2026, 8, 1, 13, 45, 0, DateTimeKind.Utc),
                    categories[4].Id,
                    "Explore Orbit Chronograph, a modern brushed steel watch with a charcoal dial and forest-green performance strap.",
                    "Orbit Chronograph combines precise visual balance with a practical sport profile. The brushed case and tonal subdials keep the design refined, while the flexible performance strap is ready for active daily use."),
                new SeedProduct(
                    Guid.Parse("10000000-0000-0000-0000-000000000005"),
                    "Solstice Frames",
                    "solstice-frames",
                    "Lightweight optical frames in translucent amber acetate with slim metal temples and a softly rounded shape for comfortable all-day wear.",
                    79.00m,
                    "/images/products/solstice-frames.webp",
                    22,
                    new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
                    categories[5].Id,
                    "Discover Solstice Frames, lightweight amber acetate eyewear with polished lines and slim metal temples.",
                    "Solstice Frames pair warm translucent acetate with lightweight metal temples and a balanced rounded silhouette. The polished construction feels distinctive without overpowering an everyday look."),
                new SeedProduct(
                    Guid.Parse("10000000-0000-0000-0000-000000000006"),
                    "Harbor Sunglasses",
                    "harbor-sunglasses",
                    "Timeless matte navy sunglasses with smoke lenses, subtle metal detailing, and a comfortable shape made for bright city days and travel.",
                    86.00m,
                    "/images/products/harbor-sunglasses.webp",
                    19,
                    new DateTime(2026, 8, 1, 14, 15, 0, DateTimeKind.Utc),
                    categories[5].Id,
                    "Shop Harbor Sunglasses, timeless matte navy frames with smoke lenses and subtle brushed metal details.",
                    "Harbor Sunglasses use a familiar, easy-wearing shape with a deep navy finish and discreet metal accents. Balanced proportions and smoke lenses make them a dependable choice for daily sun and weekend travel.")
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
            hasChanges |= AssignIfDifferent(() => product.OgImage, value => product.OgImage = value, seed.Image);
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
