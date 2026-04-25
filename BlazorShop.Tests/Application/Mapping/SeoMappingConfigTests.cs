namespace BlazorShop.Tests.Application.Mapping
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Mapping;
    using BlazorShop.Domain.Entities;

    using Microsoft.Extensions.Logging;

    using Xunit;

    public class SeoMappingConfigTests
    {
        private readonly IMapper _mapper;
        private readonly ILoggerFactory _loggerFactory;

        public SeoMappingConfigTests()
        {
            this._loggerFactory = LoggerFactory.Create(_ => { });

            var configuration = new MapperConfiguration(cfg => cfg.AddProfile<MappingConfig>(), this._loggerFactory);
            this._mapper = configuration.CreateMapper();
        }

        [Fact]
        public void Map_ProductToProductSeoDto_MapsSeoFields()
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Slug = "running-shoes",
                MetaTitle = "Running Shoes",
                MetaDescription = "Fast and lightweight shoes.",
                CanonicalUrl = "https://shop.example.com/products/running-shoes",
                OgTitle = "Shop Running Shoes",
                OgDescription = "Fast and lightweight shoes.",
                OgImage = "https://cdn.example.com/shoes.png",
                RobotsIndex = true,
                RobotsFollow = false,
                SeoContent = "Long-form SEO content",
                IsPublished = true,
                PublishedOn = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc),
            };

            var result = this._mapper.Map<ProductSeoDto>(product);

            Assert.Equal(product.Id, result.ProductId);
            Assert.Equal(product.Slug, result.Slug);
            Assert.Equal(product.MetaTitle, result.MetaTitle);
            Assert.Equal(product.MetaDescription, result.MetaDescription);
            Assert.Equal(product.CanonicalUrl, result.CanonicalUrl);
            Assert.Equal(product.OgTitle, result.OgTitle);
            Assert.Equal(product.OgDescription, result.OgDescription);
            Assert.Equal(product.OgImage, result.OgImage);
            Assert.Equal(product.RobotsIndex, result.RobotsIndex);
            Assert.Equal(product.RobotsFollow, result.RobotsFollow);
            Assert.Equal(product.SeoContent, result.SeoContent);
            Assert.Equal(product.IsPublished, result.IsPublished);
            Assert.Equal(product.PublishedOn, result.PublishedOn);
        }

        [Fact]
        public void Map_UpdateProductSeoDtoOntoExistingProduct_PreservesNonSeoFields()
        {
            var entity = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Running Shoes",
                Description = "Catalog description",
                Price = 99.99m,
                Quantity = 10,
                CategoryId = Guid.NewGuid(),
                Slug = "old-slug",
                RobotsIndex = true,
                RobotsFollow = true,
                IsPublished = true,
            };
            var update = new UpdateProductSeoDto
            {
                ProductId = entity.Id,
                Slug = "running-shoes",
                MetaTitle = "Running Shoes",
                RobotsIndex = false,
                RobotsFollow = true,
                IsPublished = true,
                PublishedOn = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc),
            };

            this._mapper.Map(update, entity);

            Assert.Equal("Running Shoes", entity.Name);
            Assert.Equal("Catalog description", entity.Description);
            Assert.Equal(99.99m, entity.Price);
            Assert.Equal(10, entity.Quantity);
            Assert.Equal(update.Slug, entity.Slug);
            Assert.Equal(update.MetaTitle, entity.MetaTitle);
            Assert.False(entity.RobotsIndex);
            Assert.True(entity.RobotsFollow);
            Assert.Equal(update.PublishedOn, entity.PublishedOn);
        }

        [Fact]
        public void Map_CategoryToGetCategory_WhenSeoIsPublished_MapsPublicSeoFields()
        {
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = "Shoes",
                MetaTitle = "Shop Shoes",
                MetaDescription = "Browse the latest shoes.",
                CanonicalUrl = "https://shop.example.com/categories/shoes",
                OgTitle = "Shoes at BlazorShop",
                OgDescription = "Browse the latest shoes.",
                OgImage = "https://cdn.example.com/shoes.png",
                RobotsIndex = false,
                RobotsFollow = true,
                IsPublished = true,
            };

            var result = this._mapper.Map<GetCategory>(category);

            Assert.Equal(category.MetaTitle, result.MetaTitle);
            Assert.Equal(category.MetaDescription, result.MetaDescription);
            Assert.Equal(category.CanonicalUrl, result.CanonicalUrl);
            Assert.Equal(category.OgTitle, result.OgTitle);
            Assert.Equal(category.OgDescription, result.OgDescription);
            Assert.Equal(category.OgImage, result.OgImage);
            Assert.False(result.RobotsIndex);
            Assert.True(result.RobotsFollow);
        }

        [Fact]
        public void Map_CategoryToGetCategory_WhenSeoIsNotPublished_HidesPublicSeoFields()
        {
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = "Shoes",
                MetaTitle = "Draft title",
                MetaDescription = "Draft description",
                CanonicalUrl = "https://shop.example.com/categories/shoes",
                OgTitle = "Draft OG title",
                OgDescription = "Draft OG description",
                OgImage = "https://cdn.example.com/shoes.png",
                RobotsIndex = false,
                RobotsFollow = false,
                IsPublished = false,
            };

            var result = this._mapper.Map<GetCategory>(category);

            Assert.Null(result.MetaTitle);
            Assert.Null(result.MetaDescription);
            Assert.Null(result.CanonicalUrl);
            Assert.Null(result.OgTitle);
            Assert.Null(result.OgDescription);
            Assert.Null(result.OgImage);
            Assert.True(result.RobotsIndex);
            Assert.True(result.RobotsFollow);
        }
    }
}