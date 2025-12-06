namespace BlazorShop.Tests.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;

    using Moq;

    using Xunit;

    public class ProductServiceTests
    {
        private readonly Mock<IGenericRepository<Product>> _mockProductRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly ProductService _productService;

        public ProductServiceTests()
        {
            // Moq init
            this._mockProductRepository = new Mock<IGenericRepository<Product>>();
            this._mockMapper = new Mock<IMapper>();

            // Create service
            this._productService = new ProductService(this._mockProductRepository.Object, this._mockMapper.Object);
        }

        [Fact]
        public async Task GetAllAsync_WhenProductsExist_ShouldReturnMappedProducts()
        {
            // Arrange
            var products = new List<Product>
            {
                new Product { Id = Guid.NewGuid(), Name = "Product1" },
                new Product { Id = Guid.NewGuid(), Name = "Product2" }
            };

            var mappedProducts = new List<GetProduct>
            {
                new GetProduct { Id = products[0].Id, Name = "Product1" },
                new GetProduct { Id = products[1].Id, Name = "Product2" }
            };

            // Moq config
            this._mockProductRepository.Setup(repo => repo.GetAllAsync())
                                  .ReturnsAsync(products);

            this._mockMapper.Setup(mapper => mapper.Map<IEnumerable<GetProduct>>(products))
                       .Returns(mappedProducts);

            // Act
            var result = await this._productService.GetAllAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mappedProducts, result);
            this._mockProductRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
            this._mockMapper.Verify(mapper => mapper.Map<IEnumerable<GetProduct>>(products), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_WhenNoProductsExist_ShouldReturnEmptyList()
        {
            // Arrange
            var products = new List<Product>();

            this._mockProductRepository.Setup(repo => repo.GetAllAsync())
                                  .ReturnsAsync(products);

            this._mockMapper.Setup(mapper => mapper.Map<IEnumerable<GetProduct>>(products))
                       .Returns(new List<GetProduct>());

            // Act
            var result = await this._productService.GetAllAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            this._mockProductRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
            this._mockMapper.Verify(mapper => mapper.Map<IEnumerable<GetProduct>>(products), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_WhenProductExists_ShouldReturnMappedProduct()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var product = new Product { Id = productId, Name = "Product1" };
            var mappedProduct = new GetProduct { Id = productId, Name = "Product1" };
            this._mockProductRepository.Setup(repo => repo.GetByIdAsync(productId))
                                  .ReturnsAsync(product);
            this._mockMapper.Setup(mapper => mapper.Map<GetProduct>(product))
                       .Returns(mappedProduct);

            // Act
            var result = await this._productService.GetByIdAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mappedProduct, result);
            this._mockProductRepository.Verify(repo => repo.GetByIdAsync(productId), Times.Once);
            this._mockMapper.Verify(mapper => mapper.Map<GetProduct>(product), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_WhenProductDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var productId = Guid.NewGuid();
            this._mockProductRepository.Setup(repo => repo.GetByIdAsync(productId))
                .ReturnsAsync((Product?)null);

            // Act
            var result = await this._productService.GetByIdAsync(productId);

            // Assert
            Assert.Null(result);
            this._mockProductRepository.Verify(repo => repo.GetByIdAsync(productId), Times.Once);
            this._mockMapper.Verify(mapper => mapper.Map<GetProduct>(It.IsAny<Product>()), Times.Never);
        }


        [Fact]
        public async Task AddAsync_WhenProductIsAdded_ShouldReturnSuccessResponse()
        {
            // Arrange
            var product = new CreateProduct { Name = "Product1" };
            var mappedProduct = new Product { Name = "Product1" };
            this._mockMapper.Setup(mapper => mapper.Map<Product>(product))
                       .Returns(mappedProduct);
            this._mockProductRepository.Setup(repo => repo.AddAsync(mappedProduct))
                                  .ReturnsAsync(1);

            // Act
            var result = await this._productService.AddAsync(product);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("Product added successfully", result.Message);
            this._mockMapper.Verify(mapper => mapper.Map<Product>(product), Times.Once);
            this._mockProductRepository.Verify(repo => repo.AddAsync(mappedProduct), Times.Once);
        }

        [Fact]
        public async Task AddAsync_WhenProductIsNotAdded_ShouldReturnFailureResponse()
        {
            // Arrange
            var product = new CreateProduct { Name = "Product1" };
            var mappedProduct = new Product { Name = "Product1" };
            this._mockMapper.Setup(mapper => mapper.Map<Product>(product))
                       .Returns(mappedProduct);
            this._mockProductRepository.Setup(repo => repo.AddAsync(mappedProduct))
                                  .ReturnsAsync(0);

            // Act
            var result = await this._productService.AddAsync(product);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Product not added", result.Message);
            this._mockMapper.Verify(mapper => mapper.Map<Product>(product), Times.Once);
            this._mockProductRepository.Verify(repo => repo.AddAsync(mappedProduct), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenProductIsUpdated_ShouldReturnSuccessResponse()
        {
            // Arrange
            var product = new UpdateProduct { Id = Guid.NewGuid(), Name = "Product1" };
            var mappedProduct = new Product { Id = product.Id, Name = "Product1" };
            this._mockMapper.Setup(mapper => mapper.Map<Product>(product))
                       .Returns(mappedProduct);
            this._mockProductRepository.Setup(repo => repo.UpdateAsync(mappedProduct))
                                  .ReturnsAsync(1);

            // Act
            var result = await this._productService.UpdateAsync(product);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("Product updated successfully", result.Message);
            this._mockMapper.Verify(mapper => mapper.Map<Product>(product), Times.Once);
            this._mockProductRepository.Verify(repo => repo.UpdateAsync(mappedProduct), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenProductIsNotUpdated_ShouldReturnFailureResponse()
        {
            // Arrange
            var product = new UpdateProduct { Id = Guid.NewGuid(), Name = "Product1" };
            var mappedProduct = new Product { Id = product.Id, Name = "Product1" };
            this._mockMapper.Setup(mapper => mapper.Map<Product>(product))
                       .Returns(mappedProduct);
            this._mockProductRepository.Setup(repo => repo.UpdateAsync(mappedProduct))
                                  .ReturnsAsync(0);

            // Act
            var result = await this._productService.UpdateAsync(product);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Product not found", result.Message);
            this._mockMapper.Verify(mapper => mapper.Map<Product>(product), Times.Once);
            this._mockProductRepository.Verify(repo => repo.UpdateAsync(mappedProduct), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenProductIsDeleted_ShouldReturnSuccessResponse()
        {
            // Arrange
            var productId = Guid.NewGuid();
            this._mockProductRepository.Setup(repo => repo.DeleteAsync(productId))
                                  .ReturnsAsync(1);

            // Act
            var result = await this._productService.DeleteAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("Product deleted successfully", result.Message);
            this._mockProductRepository.Verify(repo => repo.DeleteAsync(productId), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenProductIsNotDeleted_ShouldReturnFailureResponse()
        {
            // Arrange
            var productId = Guid.NewGuid();
            this._mockProductRepository.Setup(repo => repo.DeleteAsync(productId))
                                  .ReturnsAsync(0);

            // Act
            var result = await this._productService.DeleteAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Product not found", result.Message);
            this._mockProductRepository.Verify(repo => repo.DeleteAsync(productId), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WhenProductDoesNotExist_ShouldReturnFailureResponse()
        {
            // Arrange
            var productId = Guid.NewGuid();
            this._mockProductRepository.Setup(repo => repo.DeleteAsync(productId))
                                  .ReturnsAsync(-1);

            // Act
            var result = await this._productService.DeleteAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Product not found", result.Message);
            this._mockProductRepository.Verify(repo => repo.DeleteAsync(productId), Times.Once);
        }
    }
}
