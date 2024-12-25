namespace BlazorShop.Tests.Presentation.Services.Authentication
{
    using System.Net;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Authentication;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;

    public class AuthenticationServiceTests
    {
        private readonly AuthenticationService _authenticationService;
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;

        public AuthenticationServiceTests()
        {
            this._httpClientHelperMock = new Mock<IHttpClientHelper>();
            this._apiCallHelperMock = new Mock<IApiCallHelper>();
            this._authenticationService = new AuthenticationService(this._httpClientHelperMock.Object, this._apiCallHelperMock.Object);
        }

        [Fact]
        public async Task CreateUser_ReturnsServiceResponse_WhenApiCallIsSuccessful()
        {
            // Arrange
            var user = new CreateUser
                           {
                               FullName = "John Doe",
                               Email = "john@example.com",
                               Password = "Password123",
                               ConfirmPassword = "Password123"
                           };
            var httpClient = new HttpClient();
            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            var serviceResponse = new ServiceResponse { Success = true, Message = "User created successfully" };

            this._httpClientHelperMock.Setup(h => h.GetPrivateClientAsync()).ReturnsAsync(httpClient);
            this._apiCallHelperMock.Setup(a => a.ApiCallTypeCall<CreateUser>(It.IsAny<ApiCall>())).ReturnsAsync(apiCallResult);
            this._apiCallHelperMock.Setup(a => a.GetServiceResponse<ServiceResponse>(apiCallResult)).ReturnsAsync(serviceResponse);

            // Act
            var result = await this._authenticationService.CreateUser(user);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("User created successfully", result.Message);
        }

        [Fact]
        public async Task CreateUser_ReturnsConnectionError_WhenApiCallFails()
        {
            // Arrange
            var user = new CreateUser
                           {
                               FullName = "John Doe",
                               Email = "john@example.com",
                               Password = "Password123",
                               ConfirmPassword = "Password123"
                           };
            var httpClient = new HttpClient();
            HttpResponseMessage apiCallResult = null;

            this._httpClientHelperMock.Setup(h => h.GetPrivateClientAsync()).ReturnsAsync(httpClient);
            this._apiCallHelperMock.Setup(a => a.ApiCallTypeCall<CreateUser>(It.IsAny<ApiCall>())).ReturnsAsync(apiCallResult);
            this._apiCallHelperMock.Setup(a => a.ConnectionError()).Returns(new ServiceResponse { Success = false, Message = "Connection error" });

            // Act
            var result = await this._authenticationService.CreateUser(user);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Connection error", result.Message);
        }

        [Fact]
        public async Task LoginUser_ReturnsLoginResponse_WhenApiCallIsSuccessful()
        {
            // Arrange
            var user = new LoginUser { Email = "john@example.com", Password = "Password123" };
            var httpClient = new HttpClient();
            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            var loginResponse = new LoginResponse
                                    {
                                        Success = true,
                                        Message = "Login successful",
                                        Token = "token",
                                        RefreshToken = "refreshToken"
                                    };

            this._httpClientHelperMock.Setup(h => h.GetPrivateClientAsync()).ReturnsAsync(httpClient);
            this._apiCallHelperMock.Setup(a => a.ApiCallTypeCall<LoginUser>(It.IsAny<ApiCall>())).ReturnsAsync(apiCallResult);
            this._apiCallHelperMock.Setup(a => a.GetServiceResponse<LoginResponse>(apiCallResult)).ReturnsAsync(loginResponse);

            // Act
            var result = await this._authenticationService.LoginUser(user);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("Login successful", result.Message);
            Assert.Equal("token", result.Token);
            Assert.Equal("refreshToken", result.RefreshToken);
        }

        [Fact]
        public async Task LoginUser_ReturnsErrorResponse_WhenApiCallFails()
        {
            // Arrange
            var user = new LoginUser { Email = "john@example.com", Password = "Password123" };
            var httpClient = new HttpClient();
            var apiCallResult = new HttpResponseMessage(HttpStatusCode.BadRequest)
                                    {
                                        Content = new StringContent("{\"Message\":\"Invalid credentials.\"}")
                                    };
            var errorResponse = new LoginResponse { Success = false, Message = "Invalid credentials." };

            this._httpClientHelperMock.Setup(h => h.GetPrivateClientAsync()).ReturnsAsync(httpClient);
            this._apiCallHelperMock.Setup(a => a.ApiCallTypeCall<LoginUser>(It.IsAny<ApiCall>())).ReturnsAsync(apiCallResult);

            // Act
            var result = await this._authenticationService.LoginUser(user);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Invalid credentials.", result.Message);
        }

        [Fact]
        public async Task ReviveToken_ReturnsLoginResponse_WhenApiCallIsSuccessful()
        {
            // Arrange
            var refreshToken = "refreshToken";
            var httpClient = new HttpClient();
            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            var loginResponse = new LoginResponse
                                    {
                                        Success = true,
                                        Message = "Token revived",
                                        Token = "newToken",
                                        RefreshToken = "newRefreshToken"
                                    };

            this._httpClientHelperMock.Setup(h => h.GetPublicClient()).Returns(httpClient);
            this._apiCallHelperMock.Setup(a => a.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>())).ReturnsAsync(apiCallResult);
            this._apiCallHelperMock.Setup(a => a.GetServiceResponse<LoginResponse>(apiCallResult)).ReturnsAsync(loginResponse);

            // Act
            var result = await this._authenticationService.ReviveToken(refreshToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("Token revived", result.Message);
            Assert.Equal("newToken", result.Token);
            Assert.Equal("newRefreshToken", result.RefreshToken);
        }

        [Fact]
        public async Task ReviveToken_ReturnsErrorResponse_WhenApiCallFails()
        {
            // Arrange
            var refreshToken = "refreshToken";
            var httpClient = new HttpClient();
            HttpResponseMessage apiCallResult = null;

            this._httpClientHelperMock.Setup(h => h.GetPublicClient()).Returns(httpClient);
            this._apiCallHelperMock.Setup(a => a.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>())).ReturnsAsync(apiCallResult);
            this._apiCallHelperMock.Setup(a => a.ConnectionError()).Returns(new ServiceResponse { Success = false, Message = "Connection error" });

            // Act
            var result = await this._authenticationService.ReviveToken(refreshToken);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Connection error", result.Message);
        }
    }
}