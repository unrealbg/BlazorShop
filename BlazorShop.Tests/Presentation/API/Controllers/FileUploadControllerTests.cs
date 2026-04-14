namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.FileProviders;

    using Xunit;

    public class FileUploadControllerTests
    {
        [Fact]
        public async Task UploadFile_WhenFileMissing_ReturnsBadRequest()
        {
            var contentRootPath = CreateTempDirectory();

            try
            {
                var controller = CreateController(contentRootPath);

                var result = await controller.UploadFile(new FileUploadController.ImageUploadForm());

                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                var response = Assert.IsType<FileUploadResponse>(badRequest.Value);
                Assert.False(response.Success);
                Assert.Equal("No file uploaded.", response.Message);
            }
            finally
            {
                DeleteDirectory(contentRootPath);
            }
        }

        [Fact]
        public async Task UploadFile_WhenSignatureDoesNotMatchContentType_ReturnsBadRequest()
        {
            var contentRootPath = CreateTempDirectory();
            await using var fileStream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

            try
            {
                var controller = CreateController(contentRootPath);
                var file = CreateFormFile(fileStream, "image/png", "avatar.png");

                var result = await controller.UploadFile(new FileUploadController.ImageUploadForm { File = file });

                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                var response = Assert.IsType<FileUploadResponse>(badRequest.Value);
                Assert.False(response.Success);
                Assert.Equal("Invalid image content. The uploaded file does not match its declared type.", response.Message);
                Assert.False(Directory.Exists(Path.Combine(contentRootPath, "uploads")));
            }
            finally
            {
                DeleteDirectory(contentRootPath);
            }
        }

        [Fact]
        public async Task UploadFile_WhenPngIsValid_ReturnsOkAndPersistsSafeExtension()
        {
            var contentRootPath = CreateTempDirectory();
            await using var fileStream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04]);

            try
            {
                var controller = CreateController(contentRootPath);
                var file = CreateFormFile(fileStream, "image/png", "avatar.png");

                var result = await controller.UploadFile(new FileUploadController.ImageUploadForm { File = file });

                var ok = Assert.IsType<OkObjectResult>(result);
                var response = Assert.IsType<FileUploadResponse>(ok.Value);
                var uploadsPath = Path.Combine(contentRootPath, "uploads");
                var savedFiles = Directory.GetFiles(uploadsPath);

                Assert.True(response.Success);
                Assert.Equal("File uploaded successfully.", response.Message);
                Assert.Contains("/uploads/", response.Url, StringComparison.OrdinalIgnoreCase);
                Assert.Single(savedFiles);
                Assert.EndsWith(".png", savedFiles[0], StringComparison.OrdinalIgnoreCase);
                Assert.Contains(Path.GetFileName(savedFiles[0]), response.Url, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                DeleteDirectory(contentRootPath);
            }
        }

        private static FileUploadController CreateController(string contentRootPath)
        {
            var controller = new FileUploadController(new TestWebHostEnvironment { ContentRootPath = contentRootPath })
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };

            controller.HttpContext.Request.Scheme = "https";
            controller.HttpContext.Request.Host = new HostString("shop.example.com");
            return controller;
        }

        private static IFormFile CreateFormFile(Stream stream, string contentType, string fileName)
        {
            return new FormFile(stream, 0, stream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType,
            };
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private sealed class TestWebHostEnvironment : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = "Tests";

            public string ApplicationName { get; set; } = nameof(FileUploadControllerTests);

            public string WebRootPath { get; set; } = string.Empty;

            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

            public string ContentRootPath { get; set; } = string.Empty;

            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}