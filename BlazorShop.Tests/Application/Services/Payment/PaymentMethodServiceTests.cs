namespace BlazorShop.Tests.Application.Services.Payment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoMapper;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Testing;
    using Microsoft.Extensions.Options;
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
                _mapperMock.Object,
                Options.Create(new StripeOptions { Enabled = true }),
                Options.Create(SyntheticBankTransfer.CreateSettings()));
        }

        [Theory]
        [InlineData("iban", null)]
        [InlineData("iban", " ")]
        [InlineData("iban", "YOUR_IBAN")]
        [InlineData("iban", "BG00UNCR70001512345678")]
        [InlineData("iban", "BG01ZZZZ00000012345678")]
        [InlineData("iban", "B000ZZZZ00000012345678")]
        [InlineData("iban", "BGXXZZZZ00000012345678")]
        [InlineData("iban", "BG99ZZZZ00000012345678")]
        [InlineData("iban", "BG12SHORT")]
        [InlineData("iban", "BG12ZZZZ000000123456781234567812345678")]
        [InlineData("iban", "BG12ZZZZ-00000012345678")]
        [InlineData("iban", "BG12ZZZZ\t00000012345678")]
        [InlineData("iban", "BG12ZZZZ0000001234567é")]
        [InlineData("iban", "BG12000000000000000000")]
        [InlineData("beneficiary", "YOUR_BENEFICIARY")]
        [InlineData("beneficiary", "N/A")]
        [InlineData("beneficiary", "...")]
        [InlineData("bank", "<BANK_NAME>")]
        [InlineData("bank", "CHANGE-ME")]
        [InlineData("bank", "Test Bank")]
        public async Task InvalidBankSettings_HideStableIdAndBankLabelWithoutHidingOtherMethods(string field, string? value)
        {
            var settings = SyntheticBankTransfer.CreateSettings();
            if (field == "iban") settings.Iban = value!;
            else if (field == "beneficiary") settings.Beneficiary = value!;
            else settings.BankName = value!;
            _paymentMethodMock.Setup(repository => repository.GetPaymentMethodsAsync()).ReturnsAsync([
                new PaymentMethod { Id = PaymentMethodIds.BankTransfer, Name = "Localized label" },
                new PaymentMethod { Id = Guid.NewGuid(), Name = "Bank Transfer" },
                new PaymentMethod { Id = PaymentMethodIds.CashOnDelivery, Name = "Cash on Delivery" },
                new PaymentMethod { Id = PaymentMethodIds.CreditCard, Name = "Credit Card" },
            ]);
            _mapperMock.Setup(mapper => mapper.Map<IEnumerable<GetPaymentMethod>>(It.IsAny<object>()))
                .Returns((object methods) => ((IEnumerable<PaymentMethod>)methods)
                    .Select(method => new GetPaymentMethod { Id = method.Id, Name = method.Name }));
            var service = new PaymentMethodService(_paymentMethodMock.Object, _mapperMock.Object,
                Options.Create(new StripeOptions { Enabled = true }), Options.Create(settings));

            var result = (await service.GetPaymentMethodsAsync()).ToArray();

            Assert.Equal(2, result.Length);
            Assert.Contains(result, method => method.Id == PaymentMethodIds.CashOnDelivery);
            Assert.Contains(result, method => method.Id == PaymentMethodIds.CreditCard);
        }

        [Fact]
        public async Task GetPaymentMethodsAsync_ShouldReturnMappedPaymentMethods_WhenMethodsExist()
        {
            // Arrange
            var paymentMethods = new List<PaymentMethod>
            {
                new PaymentMethod { Id = Guid.NewGuid(), Name = "Credit Card" },
                new PaymentMethod { Id = Guid.NewGuid(), Name = "Cash on Delivery" },
                new PaymentMethod { Id = Guid.NewGuid(), Name = "PayPal" }
            };
            var supportedMethods = paymentMethods.Where(paymentMethod => paymentMethod.Name != "PayPal").ToList();
            var mappedMethods = new List<GetPaymentMethod>
            {
                new GetPaymentMethod { Id = paymentMethods[0].Id, Name = paymentMethods[0].Name },
                new GetPaymentMethod { Id = paymentMethods[1].Id, Name = paymentMethods[1].Name }
            };

            _paymentMethodMock
                .Setup(pm => pm.GetPaymentMethodsAsync())
                .ReturnsAsync(paymentMethods);
            _mapperMock
                .Setup(m => m.Map<IEnumerable<GetPaymentMethod>>(supportedMethods))
                .Returns(mappedMethods);

            // Act
            var result = await _paymentMethodService.GetPaymentMethodsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.Contains(result, pm => pm.Name == "Credit Card");
            Assert.Contains(result, pm => pm.Name == "Cash on Delivery");
            Assert.DoesNotContain(result, pm => pm.Name == "PayPal");
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
        public async Task GetPaymentMethodsAsync_ShouldReturnEmptyList_WhenRepositoryReturnsNull()
        {
            // Arrange
            _paymentMethodMock
                .Setup(pm => pm.GetPaymentMethodsAsync())
                .ReturnsAsync((IEnumerable<PaymentMethod>)null!);

            // Act
            var result = await _paymentMethodService.GetPaymentMethodsAsync();

            // Assert
            Assert.Empty(result);
            _mapperMock.Verify(m => m.Map<IEnumerable<GetPaymentMethod>>(It.IsAny<IEnumerable<PaymentMethod>>()), Times.Never);
        }

        [Fact]
        public async Task GetPaymentMethodsAsync_ShouldHideCreditCard_WhenStripeIsDisabled()
        {
            // Arrange
            var paymentMethods = new List<PaymentMethod>
            {
                new PaymentMethod { Id = Guid.NewGuid(), Name = "Credit Card" },
                new PaymentMethod { Id = Guid.NewGuid(), Name = "Cash on Delivery" },
                new PaymentMethod { Id = Guid.NewGuid(), Name = "Bank Transfer" }
            };
            var supportedMethods = paymentMethods.Where(paymentMethod => paymentMethod.Name != "Credit Card").ToList();
            var mappedMethods = supportedMethods
                .Select(paymentMethod => new GetPaymentMethod { Id = paymentMethod.Id, Name = paymentMethod.Name })
                .ToList();
            var paymentMethodService = new PaymentMethodService(
                _paymentMethodMock.Object,
                _mapperMock.Object,
                Options.Create(new StripeOptions { Enabled = false }),
                Options.Create(SyntheticBankTransfer.CreateSettings()));

            _paymentMethodMock
                .Setup(pm => pm.GetPaymentMethodsAsync())
                .ReturnsAsync(paymentMethods);
            _mapperMock
                .Setup(m => m.Map<IEnumerable<GetPaymentMethod>>(supportedMethods))
                .Returns(mappedMethods);

            // Act
            var result = await paymentMethodService.GetPaymentMethodsAsync();

            // Assert
            Assert.DoesNotContain(result, paymentMethod => paymentMethod.Name == "Credit Card");
            Assert.Contains(result, paymentMethod => paymentMethod.Name == "Cash on Delivery");
            Assert.Contains(result, paymentMethod => paymentMethod.Name == "Bank Transfer");
        }
    }
}
