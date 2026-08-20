namespace BlazorShop.Tests.Application.Services.Payment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.Extensions.Options;
    using Moq;

    using Xunit;

    public class CartServiceTests
    {
        private readonly Mock<ICart> _cartMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IProductReadRepository> _productReadRepositoryMock;
        private readonly Mock<IPaymentMethodService> _paymentMethodServiceMock;
        private readonly Mock<IPaymentService> _paymentServiceMock;
        private readonly Mock<IPayPalPaymentService> _paypalServiceMock;
        private readonly Mock<IAppUserManager> _userManagerMock;
        private readonly Mock<IOrderRepository> _orderRepositoryMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<IOptions<BankTransferSettings>> _btOptionsMock;
        private readonly CartService _cartService;

        public CartServiceTests()
        {
            _cartMock = new Mock<ICart>();
            _mapperMock = new Mock<IMapper>();
            _productReadRepositoryMock = new Mock<IProductReadRepository>();
            _paymentMethodServiceMock = new Mock<IPaymentMethodService>();
            _paymentServiceMock = new Mock<IPaymentService>();
            _paypalServiceMock = new Mock<IPayPalPaymentService>();
            _userManagerMock = new Mock<IAppUserManager>();
            _orderRepositoryMock = new Mock<IOrderRepository>();
            _emailServiceMock = new Mock<IEmailService>();
            _btOptionsMock = new Mock<IOptions<BankTransferSettings>>();
            _btOptionsMock.Setup(o => o.Value).Returns(new BankTransferSettings());
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductVariantsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, ProductVariant>());

            _cartService = new CartService(
                _cartMock.Object,
                _mapperMock.Object,
                _productReadRepositoryMock.Object,
                _paymentMethodServiceMock.Object,
                _paymentServiceMock.Object,
                _paypalServiceMock.Object,
                _userManagerMock.Object,
                _orderRepositoryMock.Object,
                _emailServiceMock.Object,
                _btOptionsMock.Object);
        }

        [Fact]
        public async Task SaveCheckoutHistoryAsync_ShouldReturnSuccess_WhenHistorySaved()
        {
            // Arrange
            var orderItems = new List<CreateOrderItem>
            {
                new CreateOrderItem
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 2,
                    UserId = "spoofed-user"
                }
            };
            var mappedData = new List<OrderItem>();
            List<CreateOrderItem>? capturedItems = null;
            _mapperMock
                .Setup(m => m.Map<IEnumerable<OrderItem>>(It.IsAny<object>()))
                .Callback<object>(items => capturedItems = ((IEnumerable<CreateOrderItem>)items).ToList())
                .Returns(mappedData);
            _cartMock
                .Setup(c => c.SaveCheckoutHistory(mappedData))
                .ReturnsAsync(1);

            // Act
            var result = await _cartService.SaveCheckoutHistoryAsync("user123", orderItems);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Checkout history saved successfully", result.Message);
            Assert.NotNull(capturedItems);
            Assert.Single(capturedItems!);
            Assert.All(capturedItems!, item => Assert.Equal("user123", item.UserId));
        }

        [Fact]
        public async Task SaveCheckoutHistoryAsync_ShouldReturnFailure_WhenHistoryNotSaved()
        {
            // Arrange
            var orderItems = new List<CreateOrderItem>
            {
                new()
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 1,
                    UserId = "other-user",
                }
            };
            var mappedData = new List<OrderItem>();
            _mapperMock
                .Setup(m => m.Map<IEnumerable<OrderItem>>(It.IsAny<object>()))
                .Returns(mappedData);
            _cartMock
                .Setup(c => c.SaveCheckoutHistory(mappedData))
                .ReturnsAsync(0);

            // Act
            var result = await _cartService.SaveCheckoutHistoryAsync("user123", orderItems);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to save checkout history", result.Message);
        }

        [Fact]
        public async Task SaveCheckoutHistoryAsync_ShouldReturnFailure_WhenUserIdIsMissing()
        {
            var result = await _cartService.SaveCheckoutHistoryAsync(string.Empty, Array.Empty<CreateOrderItem>());

            Assert.False(result.Success);
            Assert.Equal("A signed-in user is required to save checkout history.", result.Message);
            _cartMock.Verify(cart => cart.SaveCheckoutHistory(It.IsAny<IEnumerable<OrderItem>>()), Times.Never);
        }

        [Fact]
        public async Task CheckoutAsync_ShouldReturnSuccess_WhenPaymentMethodIsValid()
        {
            // Arrange
            var paymentMethodId = Guid.NewGuid();
            var checkout = new Checkout
            {
                PaymentMethodId = paymentMethodId,
                Carts = [new CartLineRequest(Guid.NewGuid(), null, 1)],
            };
            var products = new List<Product>
            {
                new Product
                {
                    Id = checkout.Carts.First().ProductId,
                    Price = 10m,
                    Quantity = 10,
                }
            };
            var totalAmount = 10m;
            var orderId = Guid.NewGuid();
            Order? createdOrder = null;
            IReadOnlyCollection<ResolvedCartLine>? paymentLines = null;
            _productReadRepositoryMock
                .Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(products.ToDictionary(product => product.Id));
            _paymentMethodServiceMock
                .Setup(s => s.GetPaymentMethodsAsync())
                .ReturnsAsync(new List<GetPaymentMethod>
                {
                    new GetPaymentMethod
                    {
                        Id = paymentMethodId,
                        Name = "Credit Card"
                    }
                });
            _paymentServiceMock
                .Setup(s => s.Pay(It.IsAny<IReadOnlyCollection<ResolvedCartLine>>(), orderId))
                .Callback<IReadOnlyCollection<ResolvedCartLine>, Guid>((lines, _) => paymentLines = lines)
                .ReturnsAsync(new ServiceResponse(true, "Payment successful"));
            _orderRepositoryMock
                .Setup(repository => repository.CreateAsync(It.IsAny<Order>()))
                .Callback<Order>(order =>
                {
                    order.Id = orderId;
                    createdOrder = order;
                })
                .ReturnsAsync(orderId);

            // Act
            var result = await _cartService.CheckoutAsync(checkout);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Payment successful", result.Message);
            Assert.NotNull(createdOrder);
            Assert.Equal(PaymentOrderStatus.PendingPayment, createdOrder!.Status);
            var resolvedLine = Assert.Single(paymentLines!);
            Assert.Equal(totalAmount, resolvedLine.UnitPrice);
            Assert.Equal(createdOrder.Lines.Single().UnitPrice, resolvedLine.UnitPrice);
            _paymentServiceMock.Verify(
                service => service.Pay(It.IsAny<IReadOnlyCollection<ResolvedCartLine>>(), createdOrder.Id),
                Times.Once);
        }

        [Fact]
        public async Task CheckoutAsync_ShouldReturnFailure_WhenPaymentMethodIsInvalid()
        {
            // Arrange
            var checkout = new Checkout
            {
                PaymentMethodId = Guid.NewGuid(),
                Carts = [],
            };
            _paymentMethodServiceMock
                .Setup(s => s.GetPaymentMethodsAsync())
                .ReturnsAsync(new List<GetPaymentMethod>
                {
                    new GetPaymentMethod
                    {
                        Id = Guid.NewGuid(),
                        Name = "Credit Card"
                    }
                });

            // Act
            var result = await _cartService.CheckoutAsync(checkout);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid payment method", result.Message);
        }

        [Fact]
        public async Task ConfirmOrderAsync_ShouldPersistRealOrder_ForConfirmedCheckout()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var carts = new[] { new CartLineRequest(productId, null, 2) };
            var products = new List<Product>
            {
                new Product
                {
                    Id = productId,
                    Price = 12.5m,
                    Quantity = 10,
                }
            };
            Order? createdOrder = null;

            _productReadRepositoryMock
                .Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(products.ToDictionary(product => product.Id));

            _orderRepositoryMock
                .Setup(repository => repository.CreateAsync(It.IsAny<Order>()))
                .Callback<Order>(order => createdOrder = order)
                .ReturnsAsync(Guid.NewGuid());

            // Act
            var result = await _cartService.ConfirmOrderAsync(carts, "user-1");

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(createdOrder);
            Assert.Equal("user-1", createdOrder!.UserId);
            Assert.Equal("Pending", createdOrder.Status);
            Assert.StartsWith("COD-", createdOrder.Reference, StringComparison.Ordinal);
            Assert.Equal(25m, createdOrder.TotalAmount);
            Assert.Single(createdOrder.Lines);
            Assert.Equal(2, createdOrder.Lines.First().Quantity);
            Assert.Equal(12.5m, createdOrder.Lines.First().UnitPrice);
            _orderRepositoryMock.Verify(repository => repository.CreateAsync(It.IsAny<Order>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmOrderAsync_PersistsSelectedVariantAndAuthoritativePrice()
        {
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            var product = new Product
            {
                Id = productId,
                Name = "Runner",
                Price = 80m,
                Quantity = 0,
            };
            var variant = new ProductVariant
            {
                Id = variantId,
                ProductId = productId,
                Price = 95m,
                Stock = 4,
                Sku = "RUN-42",
                SizeValue = "42",
            };
            Order? createdOrder = null;

            _productReadRepositoryMock
                .Setup(repository => repository.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, Product> { [productId] = product });
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductVariantsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, ProductVariant> { [variantId] = variant });
            _orderRepositoryMock
                .Setup(repository => repository.CreateAsync(It.IsAny<Order>()))
                .Callback<Order>(order => createdOrder = order)
                .ReturnsAsync(Guid.NewGuid());

            var result = await _cartService.ConfirmOrderAsync(
                [new CartLineRequest(productId, variantId, 2)],
                "user-1");

            Assert.True(result.Success);
            Assert.NotNull(createdOrder);
            var orderLine = Assert.Single(createdOrder!.Lines);
            Assert.Equal(variantId, orderLine.ProductVariantId);
            Assert.Equal(95m, orderLine.UnitPrice);
            Assert.Equal(190m, createdOrder.TotalAmount);
        }

        [Fact]
        public async Task ConfirmOrderAsync_RejectsUnknownVariant()
        {
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, Product>
                {
                    [productId] = new Product { Id = productId, Price = 50m, Quantity = 5 },
                });

            var result = await _cartService.ConfirmOrderAsync(
                [new CartLineRequest(productId, variantId, 1)],
                "user-1");

            Assert.False(result.Success);
            Assert.Contains("variant no longer exists", result.Message, StringComparison.OrdinalIgnoreCase);
            _orderRepositoryMock.Verify(repository => repository.CreateAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task ConfirmOrderAsync_RejectsUnknownProduct()
        {
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, Product>());

            var result = await _cartService.ConfirmOrderAsync(
                [new CartLineRequest(Guid.NewGuid(), null, 1)],
                "user-1");

            Assert.False(result.Success);
            Assert.Contains("product in the cart no longer exists", result.Message, StringComparison.OrdinalIgnoreCase);
            _orderRepositoryMock.Verify(repository => repository.CreateAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task ConfirmOrderAsync_RejectsVariantFromAnotherProduct()
        {
            var productId = Guid.NewGuid();
            var otherProductId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, Product>
                {
                    [productId] = new Product { Id = productId, Price = 50m, Quantity = 5 },
                });
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductVariantsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, ProductVariant>
                {
                    [variantId] = new ProductVariant { Id = variantId, ProductId = otherProductId, Price = 60m, Stock = 5 },
                });

            var result = await _cartService.ConfirmOrderAsync(
                [new CartLineRequest(productId, variantId, 1)],
                "user-1");

            Assert.False(result.Success);
            Assert.Contains("does not belong", result.Message, StringComparison.OrdinalIgnoreCase);
            _orderRepositoryMock.Verify(repository => repository.CreateAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task ConfirmOrderAsync_RejectsUnpublishedProduct()
        {
            var productId = Guid.NewGuid();
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, Product>
                {
                    [productId] = new Product
                    {
                        Id = productId,
                        Price = 50m,
                        Quantity = 5,
                        IsPublished = false,
                        PublishedOn = null,
                    },
                });

            var result = await _cartService.ConfirmOrderAsync(
                [new CartLineRequest(productId, null, 1)],
                "user-1");

            Assert.False(result.Success);
            Assert.Contains("not currently purchasable", result.Message, StringComparison.OrdinalIgnoreCase);
            _orderRepositoryMock.Verify(repository => repository.CreateAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task ConfirmOrderAsync_RejectsOutOfStockVariant()
        {
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, Product>
                {
                    [productId] = new Product { Id = productId, Price = 50m, Quantity = 5 },
                });
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductVariantsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, ProductVariant>
                {
                    [variantId] = new ProductVariant
                    {
                        Id = variantId,
                        ProductId = productId,
                        Price = 60m,
                        Stock = 0,
                    },
                });

            var result = await _cartService.ConfirmOrderAsync(
                [new CartLineRequest(productId, variantId, 1)],
                "user-1");

            Assert.False(result.Success);
            Assert.Contains("variant is not currently purchasable", result.Message, StringComparison.OrdinalIgnoreCase);
            _orderRepositoryMock.Verify(repository => repository.CreateAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task CheckoutAsync_IgnoresClientPriceAndUsesSameVariantPriceForPaymentAndOrder()
        {
            var paymentMethodId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var checkout = JsonSerializer.Deserialize<Checkout>($$"""
                {
                  "paymentMethodId": "{{paymentMethodId}}",
                  "carts": [
                    {
                      "productId": "{{productId}}",
                      "variantId": "{{variantId}}",
                      "quantity": 2,
                      "unitPrice": 0.01,
                      "sku": "CLIENT-SKU"
                    }
                  ]
                }
                """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var product = new Product { Id = productId, Name = "Runner", Price = 80m, Quantity = 0 };
            var variant = new ProductVariant
            {
                Id = variantId,
                ProductId = productId,
                Price = 95m,
                Stock = 4,
                Sku = "SERVER-SKU",
            };
            Order? createdOrder = null;
            IReadOnlyCollection<ResolvedCartLine>? paymentLines = null;

            _productReadRepositoryMock
                .Setup(repository => repository.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, Product> { [productId] = product });
            _productReadRepositoryMock
                .Setup(repository => repository.GetProductVariantsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, ProductVariant> { [variantId] = variant });
            _paymentMethodServiceMock
                .Setup(service => service.GetPaymentMethodsAsync())
                .ReturnsAsync([new GetPaymentMethod { Id = paymentMethodId, Name = "Credit Card" }]);
            _orderRepositoryMock
                .Setup(repository => repository.CreateAsync(It.IsAny<Order>()))
                .Callback<Order>(order =>
                {
                    order.Id = orderId;
                    createdOrder = order;
                })
                .ReturnsAsync(orderId);
            _paymentServiceMock
                .Setup(service => service.Pay(It.IsAny<IReadOnlyCollection<ResolvedCartLine>>(), orderId))
                .Callback<IReadOnlyCollection<ResolvedCartLine>, Guid>((lines, _) => paymentLines = lines)
                .ReturnsAsync(new ServiceResponse(true, "https://payment.example/checkout"));

            var result = await _cartService.CheckoutAsync(checkout, "user-1");

            Assert.True(result.Success);
            Assert.NotNull(createdOrder);
            var orderLine = Assert.Single(createdOrder!.Lines);
            var paymentLine = Assert.Single(paymentLines!);
            Assert.Equal(variantId, orderLine.ProductVariantId);
            Assert.Equal(95m, orderLine.UnitPrice);
            Assert.Equal(95m, paymentLine.UnitPrice);
            Assert.Equal("SERVER-SKU", paymentLine.Sku);
            Assert.Equal(190m, createdOrder.TotalAmount);
        }

        [Fact]
        public async Task GetOrderItemsAsync_ShouldReturnOrderItems_WhenHistoryExists()
        {
            // Arrange
            var history = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 2,
                    UserId = "user123",
                    CreatedOn = DateTime.UtcNow
                }
            };
            var products = new List<Product>
            {
                new Product
                {
                    Id = history.First().ProductId,
                    Name = "Product 1",
                    Price = 15m
                }
            };
            var user = new AppUser
            {
                Id = "user123",
                UserName = "testuser",
                Email = "testuser@example.com"
            };
            _cartMock
                .Setup(c => c.GetAllCheckoutHistory())
                .ReturnsAsync(history);
            _productReadRepositoryMock
                .Setup(r => r.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(products.ToDictionary(product => product.Id));
            _userManagerMock
                .Setup(u => u.GetUserByIdAsync("user123"))
                .ReturnsAsync(user);

            // Act
            var result = await _cartService.GetOrderItemsAsync();

            // Assert
            Assert.NotNull(result);
            var orderItems = result.ToList();
            Assert.Single(orderItems);
            Assert.Equal("Product 1", orderItems[0].ProductName);
            Assert.Equal(2, orderItems[0].QuantityOrdered);
            Assert.Equal("testuser", orderItems[0].CustomerName);
            Assert.Equal("testuser@example.com", orderItems[0].CustomerEmail);
            Assert.Equal(30m, orderItems[0].AmountPayed);
        }

        [Fact]
        public async Task GetOrderItemsAsync_ShouldReturnEmptyList_WhenHistoryIsNull()
        {
            // Arrange
            _cartMock
                .Setup(c => c.GetAllCheckoutHistory())
                .ReturnsAsync((IEnumerable<OrderItem>)null!);

            // Act
            var result = await _cartService.GetOrderItemsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetCheckoutHistoryByUserId_ShouldReturnOrderItems_WhenHistoryExists()
        {
            // Arrange
            var userId = "user1";
            var history = new List<OrderItem>
                              {
                                  new OrderItem
                                      {
                                          ProductId = Guid.NewGuid(),
                                          Quantity = 3,
                                          UserId = userId,
                                          CreatedOn = DateTime.UtcNow
                                      }
                              };

            var products = new List<Product>
                               {
                                   new Product
                                       {
                                           Id = history.First().ProductId,
                                           Name = "Product B",
                                           Price = 30m
                                       }
                               };

            var user = new AppUser
                           {
                               Id = userId,
                               UserName = "User One",
                               Email = "user1@example.com"
                           };

            _cartMock
                .Setup(cart => cart.GetCheckoutHistoryByUserId(userId))
                .ReturnsAsync(history);

            _productReadRepositoryMock
                .Setup(repo => repo.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(products.ToDictionary(product => product.Id));

            _userManagerMock
                .Setup(manager => manager.GetUserByIdAsync(userId))
                .ReturnsAsync(user);

            // Act
            var orderItems = await _cartService.GetCheckoutHistoryByUserId(userId);

            // Assert
            Assert.Single(orderItems);
            var item = orderItems.First();
            Assert.Equal("User One", item.CustomerName);
            Assert.Equal("user1@example.com", item.CustomerEmail);
            Assert.Equal("Product B", item.ProductName);
            Assert.Equal(90m, item.AmountPayed);
            Assert.Equal(3, item.QuantityOrdered);
        }

        [Fact]
        public async Task GetCheckoutHistoryByUserId_ShouldReturnEmptyList_WhenNoHistoryExists()
        {
            // Arrange
            var userId = "user1";

            _cartMock
                .Setup(cart => cart.GetCheckoutHistoryByUserId(userId))
                .ReturnsAsync(new List<OrderItem>());

            // Act
            var orderItems = await _cartService.GetCheckoutHistoryByUserId(userId);

            // Assert
            Assert.Empty(orderItems);
        }
    }
}
