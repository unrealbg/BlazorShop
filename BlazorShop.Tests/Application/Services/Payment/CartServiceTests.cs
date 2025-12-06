namespace BlazorShop.Tests.Application.Services.Payment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        private readonly Mock<IGenericRepository<Product>> _productRepositoryMock;
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
            _productRepositoryMock = new Mock<IGenericRepository<Product>>();
            _paymentMethodServiceMock = new Mock<IPaymentMethodService>();
            _paymentServiceMock = new Mock<IPaymentService>();
            _paypalServiceMock = new Mock<IPayPalPaymentService>();
            _userManagerMock = new Mock<IAppUserManager>();
            _orderRepositoryMock = new Mock<IOrderRepository>();
            _emailServiceMock = new Mock<IEmailService>();
            _btOptionsMock = new Mock<IOptions<BankTransferSettings>>();
            _btOptionsMock.Setup(o => o.Value).Returns(new BankTransferSettings());

            _cartService = new CartService(
                _cartMock.Object,
                _mapperMock.Object,
                _productRepositoryMock.Object,
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
                    UserId = "user123"
                }
            };
            var mappedData = new List<OrderItem>();
            _mapperMock
                .Setup(m => m.Map<IEnumerable<OrderItem>>(orderItems))
                .Returns(mappedData);
            _cartMock
                .Setup(c => c.SaveCheckoutHistory(mappedData))
                .ReturnsAsync(1);

            // Act
            var result = await _cartService.SaveCheckoutHistoryAsync(orderItems);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Checkout history saved successfully", result.Message);
        }

        [Fact]
        public async Task SaveCheckoutHistoryAsync_ShouldReturnFailure_WhenHistoryNotSaved()
        {
            // Arrange
            var orderItems = new List<CreateOrderItem>();
            var mappedData = new List<OrderItem>();
            _mapperMock
                .Setup(m => m.Map<IEnumerable<OrderItem>>(orderItems))
                .Returns(mappedData);
            _cartMock
                .Setup(c => c.SaveCheckoutHistory(mappedData))
                .ReturnsAsync(0);

            // Act
            var result = await _cartService.SaveCheckoutHistoryAsync(orderItems);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to save checkout history", result.Message);
        }

        [Fact]
        public async Task CheckoutAsync_ShouldReturnSuccess_WhenPaymentMethodIsValid()
        {
            // Arrange
            var paymentMethodId = Guid.NewGuid();
            var checkout = new Checkout
            {
                PaymentMethodId = paymentMethodId,
                Carts = new List<ProcessCart>
                {
                    new ProcessCart
                    {
                        ProductId = Guid.NewGuid(),
                        Quantity = 1
                    }
                }
            };
            var products = new List<Product>
            {
                new Product
                {
                    Id = checkout.Carts.First().ProductId,
                    Price = 10m
                }
            };
            var totalAmount = 10m;
            _productRepositoryMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(products);
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
                .Setup(s => s.Pay(totalAmount, products, checkout.Carts))
                .ReturnsAsync(new ServiceResponse(true, "Payment successful"));

            // Act
            var result = await _cartService.CheckoutAsync(checkout);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Payment successful", result.Message);
        }

        [Fact]
        public async Task CheckoutAsync_ShouldReturnFailure_WhenPaymentMethodIsInvalid()
        {
            // Arrange
            var checkout = new Checkout
            {
                PaymentMethodId = Guid.NewGuid(),
                Carts = new List<ProcessCart>()
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
            _productRepositoryMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(products);
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

            _productRepositoryMock
                .Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(products);

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
