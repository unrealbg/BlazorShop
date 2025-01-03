namespace BlazorShop.Tests.Presentation.Services
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Services;

    using Microsoft.AspNetCore.Components.Forms;

    using Moq;

    using Xunit;

    public class FileUploadServiceTests
    {
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;
        private readonly Mock<IBrowserFile> _browserFileMock;
        private readonly FileUploadService _fileUploadService;

        public FileUploadServiceTests()
        {
            this._httpClientHelperMock = new Mock<IHttpClientHelper>();
            this._apiCallHelperMock = new Mock<IApiCallHelper>();
            this._browserFileMock = new Mock<IBrowserFile>();
            this._fileUploadService = new FileUploadService(this._httpClientHelperMock.Object, this._apiCallHelperMock.Object);
        }

        [Fact]
        public async Task UploadFileAsync_Success_ReturnsFileUploadResponse()
        {
            // Arrange
            var privateClient = new HttpClient();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
            var fileUploadResponse = new FileUploadResponse();

            this._httpClientHelperMock.Setup(h => h.GetPrivateClientAsync())
                .ReturnsAsync(privateClient);

            this._browserFileMock.Setup(f => f.OpenReadStream(It.IsAny<long>(), default))
                .Returns(new System.IO.MemoryStream());
            this._browserFileMock.Setup(f => f.ContentType)
                .Returns("application/octet-stream");
            this._browserFileMock.Setup(f => f.Name)
                .Returns("testfile.jpg");

            this._apiCallHelperMock.Setup(a => a.ApiCallTypeCall<MultipartFormDataContent>(It.IsAny<ApiCall>()))
                .ReturnsAsync(httpResponse);
            this._apiCallHelperMock.Setup(a => a.GetServiceResponse<FileUploadResponse>(httpResponse))
                .ReturnsAsync(fileUploadResponse);

            // Act
            var result = await this._fileUploadService.UploadFileAsync(this._browserFileMock.Object);

            // Assert
            Assert.Equal(fileUploadResponse, result);
        }

        [Fact]
        public async Task UploadFileAsync_Failure_ThrowsException()
        {
            // Arrange
            var privateClient = new HttpClient();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);

            this._httpClientHelperMock.Setup(h => h.GetPrivateClientAsync())
                .ReturnsAsync(privateClient);

            this._browserFileMock.Setup(f => f.OpenReadStream(It.IsAny<long>(), default))
                .Returns(new System.IO.MemoryStream());
            this._browserFileMock.Setup(f => f.ContentType)
                .Returns("application/octet-stream");
            this._browserFileMock.Setup(f => f.Name)
                .Returns("testfile.jpg");

            this._apiCallHelperMock.Setup(a => a.ApiCallTypeCall<MultipartFormDataContent>(It.IsAny<ApiCall>()))
                .ReturnsAsync(httpResponse);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => this._fileUploadService.UploadFileAsync(this._browserFileMock.Object));
        }
    }
}
