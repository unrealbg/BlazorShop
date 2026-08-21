namespace BlazorShop.Tests.Application.Services.Payment
{
    using BlazorShop.Application;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Payment;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    using Xunit;

    public sealed class CurrencyMoneyTests
    {
        [Theory]
        [InlineData(" eur ", "EUR")]
        [InlineData("usd", "USD")]
        [InlineData("GBP", "GBP")]
        public void NormalizeCurrency_NormalizesSupportedCodes(string input, string expected)
        {
            Assert.Equal(expected, CurrencyMoney.NormalizeCurrency(input));
            Assert.True(CurrencyMoney.IsSupportedCurrency(input));
        }

        [Theory]
        [InlineData(1.004, 100)]
        [InlineData(1.005, 101)]
        [InlineData(19.999, 2000)]
        public void ToMinorUnits_UsesCurrencyPrecisionAndAwayFromZeroRounding(
            decimal amount,
            long expectedMinor)
        {
            Assert.Equal(expectedMinor, CurrencyMoney.ToMinorUnits(amount, "EUR"));
        }

        [Fact]
        public void ToMinorUnits_RejectsOverflow()
        {
            Assert.Throws<OverflowException>(() => CurrencyMoney.ToMinorUnits(decimal.MaxValue, "EUR"));
        }

        [Fact]
        public void UnsupportedCurrency_IsRejected()
        {
            Assert.False(CurrencyMoney.IsSupportedCurrency("JPY"));
            Assert.Throws<ArgumentOutOfRangeException>(() => CurrencyMoney.GetMinorUnitPrecision("JPY"));
        }

        [Fact]
        public void CommerceConfiguration_IsNormalizedOnce()
        {
            using var provider = CreateProvider(" eur ");

            Assert.Equal("EUR", provider.GetRequiredService<IOptions<CommerceOptions>>().Value.Currency);
        }

        [Fact]
        public void CommerceConfiguration_RejectsUnsupportedCurrency()
        {
            using var provider = CreateProvider("JPY");

            Assert.Throws<OptionsValidationException>(
                () => provider.GetRequiredService<IOptions<CommerceOptions>>().Value);
        }

        private static ServiceProvider CreateProvider(string currency)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Commerce:Currency"] = currency,
                    ["ClientApp:BaseUrl"] = "https://shop.example.com",
                })
                .Build();
            return new ServiceCollection().AddApplication(configuration).BuildServiceProvider();
        }
    }
}
