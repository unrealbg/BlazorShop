namespace BlazorShop.Tests.Application.Services.Payment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoMapper;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using Moq;
    using Xunit;

    public class PaymentMethodServiceTests
    {
        private readonly PaymentMethodService _paymentMethodService;
        private readonly Mock<IPaymentMethod> _paymentMethodMock;
        private readonly Mock<IMapper> _mapperMock;

        public PaymentMethodServiceTests()
        {
            _paymentMethodMock = new Mock<IPaymentMethod>();
            _mapperMock = new Mock<IMapper>();

            _paymentMethodService = new PaymentMethodService(
                _paymentMethodMock.Object,
                _mapperMock.Object);
        }

        [Fact]
        public async Task GetPaymentMethodsAsync_ShouldReturnMappedPaymentMethods_WhenMethodsExist()
        {
            // Arrange
            var paymentMethods = new List<PaymentMethod>
            {
                new PaymentMethod { Id = Guid.NewGuid(), Name = "Credit Card" },
                new PaymentMethod { Id = Guid.NewGuid(), Name = "PayPal" }
            };
            var mappedMethods = new List<GetPaymentMethod>
            {
                new GetPaymentMethod { Id = paymentMethods[0].Id, Name = paymentMethods[0].Name },
                new GetPaymentMethod { Id = paymentMethods[1].Id, Name = paymentMethods[1].Name }
            };

            _paymentMethodMock
                .Setup(pm => pm.GetPaymentMethodsAsync())
                .ReturnsAsync(paymentMethods);
            _mapperMock
                .Setup(m => m.Map<IEnumerable<GetPaymentMethod>>(paymentMethods))
                .Returns(mappedMethods);

            // Act
            var result = await _paymentMethodService.GetPaymentMethodsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.Contains(result, pm => pm.Name == "Credit Card");
            Assert.Contains(result, pm => pm.Name == "PayPal");
        }

        [Fact]
        public async Task GetPaymentMethodsAsync_ShouldReturnEmptyList_WhenNoMethodsExist()
        {
            // Arrange
            var paymentMethods = new List<PaymentMethod>();
            var mappedMethods = new List<GetPaymentMethod>();
            _paymentMethodMock
                .Setup(pm => pm.GetPaymentMethodsAsync())
                .ReturnsAsync(paymentMethods);
            _mapperMock
                .Setup(m => m.Map<IEnumerable<GetPaymentMethod>>(paymentMethods))
                .Returns(mappedMethods);

            // Act
            var result = await _paymentMethodService.GetPaymentMethodsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetPaymentMethodsAsync_ShouldReturnEmptyList_WhenMethodsIsNull()
        {
            // Arrange
            IEnumerable<PaymentMethod> paymentMethods = null;
            IEnumerable<GetPaymentMethod> mappedMethods = null;
            _paymentMethodMock
                .Setup(pm => pm.GetPaymentMethodsAsync())
                .ReturnsAsync(paymentMethods);
            _mapperMock
                .Setup(m => m.Map<IEnumerable<GetPaymentMethod>>(paymentMethods))
                .Returns(mappedMethods);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await _paymentMethodService.GetPaymentMethodsAsync();
            });
        }
    }
}
