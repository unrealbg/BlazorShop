namespace BlazorShop.Tests.Application.Services.Authentication
{
    using System.Security.Claims;

    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Application.Services.Authentication;
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Application.Validations;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Entities.Identity;

    using FluentValidation;

    using Moq;

    using Xunit;

    public class AuthenticationServiceTests
    {
        private readonly Mock<IAppTokenManager> _tokenManagerMock;

        private readonly Mock<IAppUserManager> _userManagerMock;

        private readonly Mock<IAppRoleManager> _roleManagerMock;

        private readonly Mock<IAppLogger<AuthenticationService>> _loggerMock;

        private readonly Mock<IMapper> _mapperMock;

        private readonly Mock<IValidator<CreateUser>> _createUserValidatorMock;

        private readonly Mock<IValidator<LoginUser>> _loginUserValidatorMock;

        private readonly Mock<IValidator<ChangePassword>> _changePasswordValidatorMock;

        private readonly Mock<IValidationService> _validationServiceMock;

        private readonly AuthenticationService _authenticationService;

        public AuthenticationServiceTests()
        {
            _tokenManagerMock = new Mock<IAppTokenManager>();
            _userManagerMock = new Mock<IAppUserManager>();
            _roleManagerMock = new Mock<IAppRoleManager>();
            _loggerMock = new Mock<IAppLogger<AuthenticationService>>();
            _mapperMock = new Mock<IMapper>();
            _createUserValidatorMock = new Mock<IValidator<CreateUser>>();
            _loginUserValidatorMock = new Mock<IValidator<LoginUser>>();
            _changePasswordValidatorMock = new Mock<IValidator<ChangePassword>>();
            _validationServiceMock = new Mock<IValidationService>();

            this._authenticationService = new AuthenticationService(
                this._tokenManagerMock.Object,
                this._userManagerMock.Object,
                this._roleManagerMock.Object,
                this._loggerMock.Object,
                this._mapperMock.Object,
                this._createUserValidatorMock.Object,
                this._loginUserValidatorMock.Object,
                this._validationServiceMock.Object,
                this._changePasswordValidatorMock.Object);
        }

        [Fact]
        public async Task CreateUser_ShouldReturnSuccess_WhenUserIsCreated()
        {
            // Arrange
            var createUser = new CreateUser
                                 {
                                     Email = "test@example.com",
                                     Password = "Password123",
                                     ConfirmPassword = "Password123",
                                     FullName = "Test User"
                                 };
            var validationResult = new ServiceResponse { Success = true };
            var appUser = new AppUser
                              {
                                  Email = createUser.Email,
                                  UserName = createUser.Email,
                                  PasswordHash = createUser.Password
                              };

            this._validationServiceMock.Setup(v => v.ValidateAsync(createUser, this._createUserValidatorMock.Object))
                .ReturnsAsync(validationResult);
            this._mapperMock.Setup(m => m.Map<AppUser>(createUser)).Returns(appUser);
            this._userManagerMock.Setup(u => u.CreateUserAsync(appUser)).ReturnsAsync(true);
            this._userManagerMock.Setup(u => u.GetUserByEmailAsync(createUser.Email)).ReturnsAsync(appUser);
            this._userManagerMock.Setup(u => u.GetAllUsersAsync()).ReturnsAsync(new List<AppUser> { appUser });
            this._roleManagerMock.Setup(r => r.AddUserToRoleAsync(appUser, "Admin")).ReturnsAsync(true);

            // Act
            var result = await this._authenticationService.CreateUser(createUser);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("User created successfully.", result.Message);
        }

        [Fact]
        public async Task LoginUser_ShouldReturnSuccess_WhenCredentialsAreValid()
        {
            // Arrange
            var loginUser = new LoginUser { Email = "test@example.com", Password = "Password123" };
            var validationResult = new ServiceResponse { Success = true };
            var appUser = new AppUser { Email = loginUser.Email, PasswordHash = loginUser.Password };
            var claims = new List<Claim>();
            var accessToken = "accessToken";
            var refreshToken = "refreshToken";

            this._validationServiceMock.Setup(v => v.ValidateAsync(loginUser, this._loginUserValidatorMock.Object))
                .ReturnsAsync(validationResult);
            this._mapperMock.Setup(m => m.Map<AppUser>(loginUser)).Returns(appUser);
            this._userManagerMock.Setup(u => u.LoginUserAsync(appUser)).ReturnsAsync(true);
            this._userManagerMock.Setup(u => u.GetUserByEmailAsync(loginUser.Email)).ReturnsAsync(appUser);
            this._userManagerMock.Setup(u => u.GetUserClaimsAsync(loginUser.Email)).ReturnsAsync(claims);
            this._tokenManagerMock.Setup(t => t.GenerateAccessToken(claims)).Returns(accessToken);
            this._tokenManagerMock.Setup(t => t.GetReFreshToken()).Returns(refreshToken);
            this._tokenManagerMock.Setup(t => t.ValidateRefreshTokenAsync(refreshToken)).ReturnsAsync(true);
            this._tokenManagerMock.Setup(t => t.UpdateRefreshTokenAsync(appUser.Id, refreshToken)).ReturnsAsync(1);

            // Act
            var result = await this._authenticationService.LoginUser(loginUser);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Login successful.", result.Message);
            Assert.Equal(accessToken, result.Token);
            Assert.Equal(refreshToken, result.RefreshToken);
        }

        [Fact]
        public async Task ReviveToken_ShouldReturnSuccess_WhenTokenIsValid()
        {
            // Arrange
            var refreshToken = "refreshToken";
            var userId = "userId";
            var appUser = new AppUser { Id = userId, Email = "test@example.com" };
            var claims = new List<Claim>();
            var newAccessToken = "newAccessToken";
            var newRefreshToken = "newRefreshToken";

            this._tokenManagerMock.Setup(t => t.ValidateRefreshTokenAsync(refreshToken)).ReturnsAsync(true);
            this._tokenManagerMock.Setup(t => t.GetUserIdByRefreshTokenAsync(refreshToken)).ReturnsAsync(userId);
            this._userManagerMock.Setup(u => u.GetUserByIdAsync(userId)).ReturnsAsync(appUser);
            this._userManagerMock.Setup(u => u.GetUserClaimsAsync(appUser.Email)).ReturnsAsync(claims);
            this._tokenManagerMock.Setup(t => t.GenerateAccessToken(claims)).Returns(newAccessToken);
            this._tokenManagerMock.Setup(t => t.GetReFreshToken()).Returns(newRefreshToken);

            // Act
            var result = await this._authenticationService.ReviveToken(refreshToken);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Token revived successfully.", result.Message);
            Assert.Equal(newAccessToken, result.Token);
            Assert.Equal(newRefreshToken, result.RefreshToken);
        }

        [Fact]
        public async Task ChangePassword_ShouldReturnSuccess_WhenPasswordIsChanged()
        {
            // Arrange
            var userId = "1";
            var changePasswordDto = new ChangePassword
                                        {
                                            CurrentPassword = "OldPassword123",
                                            NewPassword = "NewPassword123",
                                            ConfirmPassword = "NewPassword123"
                                        };
            var user = new AppUser { Id = userId, Email = "test@example.com" };
            var validationResult = new ServiceResponse { Success = true };
            _userManagerMock.Setup(um => um.GetUserByIdAsync(userId)).ReturnsAsync(user);
            _validationServiceMock.Setup(vs => vs.ValidateAsync(changePasswordDto, _changePasswordValidatorMock.Object)).ReturnsAsync(validationResult);
            _userManagerMock.Setup(um => um.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword)).ReturnsAsync(true);

            // Act
            var result = await _authenticationService.ChangePassword(changePasswordDto, userId);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Password changed successfully.", result.Message);
        }

        [Fact]
        public async Task CheckPasswordAsync_ShouldReturnTrue_WhenPasswordIsCorrect()
        {
            // Arrange
            var user = new AppUser { Email = "test@example.com" };
            var password = "Password123";
            _userManagerMock.Setup(um => um.CheckPasswordAsync(user, password)).ReturnsAsync(true);

            // Act
            var result = await _authenticationService.CheckPasswordAsync(user, password);

            // Assert
            Assert.True(result);
        }
    }
}