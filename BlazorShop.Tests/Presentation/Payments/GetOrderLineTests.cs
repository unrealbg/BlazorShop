namespace BlazorShop.Tests.Presentation.Payments
{
    using BlazorShop.Web.Shared.Models.Payment;

    using Xunit;

    public class GetOrderLineTests
    {
        [Theory]
        [InlineData("ShoesEU", "42", "EU 42")]
        [InlineData("ShoesUS", "10", "US 10")]
        [InlineData("ShoesUK", "10", "UK 10")]
        public void VariantLabel_IncludesHistoricalSizeScale(string sizeScale, string sizeValue, string expected)
        {
            var line = new GetOrderLine
            {
                SizeScale = sizeScale,
                SizeValue = sizeValue,
            };

            Assert.Equal(expected, line.VariantLabel);
        }
    }
}
