namespace BlazorShop.Tests.Presentation.Services.Authentication
{
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Authentication;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;
    using Xunit.Sdk;

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
        public async Task LoginUser_ReturnsErrorResponse_WhenResultIsNull()
        {
            // Arrange
            var user = new LoginUser { Email = "john@example.com", Password = "Password123" };

            _httpClientHelperMock
                .Setup(h => h.GetPrivateClientAsync())
                .ReturnsAsync(new HttpClient());

            _apiCallHelperMock
                .Setup(a => a.ApiCallTypeCall<LoginUser>(It.IsAny<ApiCall>()))
                .ReturnsAsync((HttpResponseMessage)null);

            // Act
            var result = await _authenticationService.LoginUser(user);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("An error occurred.", result.Message);
        }

        [Fact]
        public async Task LoginUser_Success_ReturnsLoginResponse()
        {
            // Arrange
            var user = new LoginUser { Email = "test@example.com", Password = "password" };
            var client = new HttpClient();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
            var expectedResponse = new LoginResponse { Token = "token" };

            _httpClientHelperMock.Setup(x => x.GetPrivateClientAsync()).ReturnsAsync(client);
            _apiCallHelperMock.Setup(x => x.ApiCallTypeCall<LoginUser>(It.IsAny<ApiCall>())).ReturnsAsync(httpResponse);
            _apiCallHelperMock.Setup(x => x.GetServiceResponse<LoginResponse>(httpResponse)).ReturnsAsync(expectedResponse);

            // Act
            var result = await _authenticationService.LoginUser(user);

            // Assert
            Assert.Equal(expectedResponse.Token, result.Token);
        }

        [Fact]
        public async Task LoginUser_NullResult_ReturnsErrorResponse()
        {
            // Arrange
            var user = new LoginUser { Email = "test@example.com", Password = "password" };
            var client = new HttpClient();

            _httpClientHelperMock.Setup(x => x.GetPrivateClientAsync()).ReturnsAsync(client);
            _apiCallHelperMock.Setup(x => x.ApiCallTypeCall<LoginUser>(It.IsAny<ApiCall>())).ReturnsAsync((HttpResponseMessage)null);

            // Act
            var result = await _authenticationService.LoginUser(user);

            // Assert
            Assert.Equal("An error occurred.", result.Message);
        }

        [Fact]
        public async Task LoginUser_UnsuccessfulStatusCode_WithErrorMessage_ReturnsErrorMessage()
        {
            // Arrange
            var user = new LoginUser { Email = "test@example.com", Password = "password" };
            var client = new HttpClient();
            var errorMessage = "Invalid credentials";
            var errorResponse = new LoginResponse { Message = errorMessage };
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(errorResponse)
            };

            _httpClientHelperMock.Setup(x => x.GetPrivateClientAsync()).ReturnsAsync(client);
            _apiCallHelperMock.Setup(x => x.ApiCallTypeCall<LoginUser>(It.IsAny<ApiCall>())).ReturnsAsync(httpResponse);

            // Act
            var result = await _authenticationService.LoginUser(user);

            // Assert
            Assert.Equal(errorMessage, result.Message);
        }

        [Fact]
        public async Task LoginUser_UnsuccessfulStatusCode_WithException_ReturnsContentAsErrorMessage()
        {
            // Arrange
            var user = new LoginUser { Email = "test@example.com", Password = "password" };
            var client = new HttpClient();
            var responseContent = "Error occurred";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(responseContent)
            };

            _httpClientHelperMock.Setup(x => x.GetPrivateClientAsync()).ReturnsAsync(client);
            _apiCallHelperMock.Setup(x => x.ApiCallTypeCall<LoginUser>(It.IsAny<ApiCall>())).ReturnsAsync(httpResponse);

            // Simulate exception during ReadFromJsonAsync
            _apiCallHelperMock.Setup(x => x.GetServiceResponse<LoginResponse>(httpResponse))
                .ThrowsAsync(new System.Exception());

            // Act
            var result = await _authenticationService.LoginUser(user);

            // Assert
            Assert.Equal(responseContent, result.Message);
        }

        [Fact]
        public async Task LoginUser_UnsuccessfulStatusCode_NoContent_ReturnsDefaultErrorMessage()
        {
            // Arrange
            var user = new LoginUser { Email = "test@example.com", Password = "password" };
            var client = new HttpClient();
            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);

            _httpClientHelperMock.Setup(x => x.GetPrivateClientAsync()).ReturnsAsync(client);
            _apiCallHelperMock.Setup(x => x.ApiCallTypeCall<LoginUser>(It.IsAny<ApiCall>())).ReturnsAsync(httpResponse);

            // Act
            var result = await _authenticationService.LoginUser(user);

            // Assert
            Assert.Equal("", result.Message);
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

        [Fact]
        public async Task ConfirmEmail_ShouldReturnSuccessResponse()
        {
            // Arrange
            var userId = "sample_user_id";
            var token = "sample_token";

            var client = new HttpClient();
            _httpClientHelperMock
                .Setup(h => h.GetPublicClient())
                .Returns(client);

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);

            _apiCallHelperMock
                .Setup(a => a.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(httpResponse);

            _apiCallHelperMock
                .Setup(a => a.GetServiceResponse<ServiceResponse>(httpResponse))
                .ReturnsAsync(new ServiceResponse { Success = true, Message = "Email confirmed successfully." });

            // Act
            var result = await _authenticationService.ConfirmEmail(userId, token);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Email confirmed successfully.", result.Message);
        }

        [Fact]
        public async Task ChangePassword_ShouldReturnSuccessResponse()
        {
            // Arrange
            var changePasswordDto = new PasswordChangeModel
            {
                CurrentPassword = "oldPassword",
                NewPassword = "newPassword",
                ConfirmPassword = "newPassword"
            };
            var client = new HttpClient();
            _httpClientHelperMock
                .Setup(h => h.GetPrivateClientAsync())
                .ReturnsAsync(client);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(a => a.ApiCallTypeCall<PasswordChangeModel>(It.IsAny<ApiCall>()))
                .ReturnsAsync(httpResponse);
            _apiCallHelperMock
                .Setup(a => a.GetServiceResponse<ServiceResponse>(httpResponse))
                .ReturnsAsync(new ServiceResponse { Success = true, Message = "Password changed successfully." });

            // Act
            var result = await _authenticationService.ChangePassword(changePasswordDto);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Password changed successfully.", result.Message);
        }
    }
}