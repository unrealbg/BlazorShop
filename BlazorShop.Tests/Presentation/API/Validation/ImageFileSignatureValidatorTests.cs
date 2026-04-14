namespace BlazorShop.Tests.Presentation.API.Validation
{
    using BlazorShop.API.Validation;

    using Xunit;

    public class ImageFileSignatureValidatorTests
    {
        [Fact]
        public async Task IsValidAsync_WhenJpegHeaderMatchesContentType_ReturnsTrue()
        {
            await using var stream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

            var isValid = await ImageFileSignatureValidator.IsValidAsync(stream, "image/jpeg");

            Assert.True(isValid);
        }

        [Fact]
        public async Task IsValidAsync_WhenHeaderDoesNotMatchContentType_ReturnsFalse()
        {
            await using var stream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

            var isValid = await ImageFileSignatureValidator.IsValidAsync(stream, "image/png");

            Assert.False(isValid);
        }

        [Fact]
        public async Task IsValidAsync_WhenWebpHeaderMatchesContentType_ReturnsTrue()
        {
            await using var stream = new MemoryStream([0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38]);

            var isValid = await ImageFileSignatureValidator.IsValidAsync(stream, "image/webp");

            Assert.True(isValid);
        }
    }
}