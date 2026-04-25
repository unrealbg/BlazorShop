namespace BlazorShop.Tests.Application.Validations
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Validations.Seo;

    using Xunit;

    public class SeoRedirectDtoValidatorTests
    {
        private readonly SeoRedirectDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenRedirectIsValid_ReturnsSuccess()
        {
            var dto = new SeoRedirectDto
            {
                OldPath = "/products/old-running-shoes",
                NewPath = "/products/running-shoes",
                StatusCode = 301,
                IsActive = true,
            };

            var result = this._validator.Validate(dto);

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(200)]
        [InlineData(307)]
        public void Validate_WhenStatusCodeIsNotAllowed_ReturnsFailure(int statusCode)
        {
            var dto = new SeoRedirectDto
            {
                OldPath = "/products/old-running-shoes",
                NewPath = "/products/running-shoes",
                StatusCode = statusCode,
            };

            var result = this._validator.Validate(dto);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "StatusCode");
        }

        [Fact]
        public void Validate_WhenPathsAreInvalid_ReturnsFailure()
        {
            var dto = new SeoRedirectDto
            {
                OldPath = "products/old-running-shoes",
                NewPath = "/products/old-running-shoes",
                StatusCode = 301,
            };

            var result = this._validator.Validate(dto);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.PropertyName == "OldPath");
        }

        [Fact]
        public void Validate_WhenPathsAreIdentical_ReturnsFailure()
        {
            var dto = new SeoRedirectDto
            {
                OldPath = "/products/running-shoes",
                NewPath = "/products/running-shoes",
                StatusCode = 301,
            };

            var result = this._validator.Validate(dto);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.ErrorMessage == "OldPath and NewPath must be different.");
        }
    }
}