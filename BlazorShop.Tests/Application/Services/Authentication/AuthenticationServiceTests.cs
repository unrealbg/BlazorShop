namespace BlazorShop.Tests.Application.Services.Authentication
{
    using System.Security.Claims;

    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Application.Services.Authentication;
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Application.Validations;
    using BlazorShop.Domain.Contracts;
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
        private readonly Mock<IEmailService> _emailServiceMock;

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
            _emailServiceMock = new Mock<IEmailService>();

            _authenticationService = new AuthenticationService(
                _tokenManagerMock.Object,
                _userManagerMock.Object,
                _roleManagerMock.Object,
                _loggerMock.Object,
                _mapperMock.Object,
                _createUserValidatorMock.Object,
                _loginUserValidatorMock.Object,
                _validationServiceMock.Object,
                _changePasswordValidatorMock.Object,
                _emailServiceMock.Object);
        }

        [Fact]
        public async Task CreateUser_ShouldReturnSuccess_WhenUserIsCreated()
        {
            // Arrange
            var createUser = new CreateUser { Email = "test@example.com", Password = "Password123", ConfirmPassword = "Password123", FullName = "Test User" };
            var mappedUser = new AppUser { Email = createUser.Email, PasswordHash = createUser.Password };

            _validationServiceMock.Setup(v => v.ValidateAsync(createUser, _createUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = true });
            _mapperMock.Setup(m => m.Map<AppUser>(createUser)).Returns(mappedUser);
            _userManagerMock.Setup(u => u.CreateUserAsync(mappedUser)).ReturnsAsync(true);
            _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(mappedUser))
                .ReturnsAsync("confirmation-token");
            _userManagerMock.Setup(u => u.GetUserByEmailAsync(createUser.Email))
                .ReturnsAsync(mappedUser);
            _userManagerMock.Setup(u => u.GetAllUsersAsync())
                .ReturnsAsync(new List<AppUser> { mappedUser });
            _roleManagerMock.Setup(r => r.AddUserToRoleAsync(mappedUser, "Admin"))
                .ReturnsAsync(true);

            // Act
            var result = await _authenticationService.CreateUser(createUser);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("User created successfully.", result.Message);
            _emailServiceMock.Verify(e => e.SendEmailAsync(createUser.Email, "Confirm your email", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateUser_ShouldReturnValidationError_WhenValidationFails()
        {
            // Arrange
            var createUser = new CreateUser { Email = "invalid@example.com", Password = "123", ConfirmPassword = "123", FullName = "Invalid User" };

            _validationServiceMock
                .Setup(v => v.ValidateAsync(createUser, _createUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = false, Message = "Validation failed" });

            // Act
            var result = await _authenticationService.CreateUser(createUser);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Validation failed", result.Message);
        }

        [Fact]
        public async Task CreateUser_ShouldReturnError_WhenUserAlreadyExists()
        {
            // Arrange
            var createUser = new CreateUser { Email = "test@example.com", Password = "Password123", ConfirmPassword = "Password123", FullName = "Test User" };
            var mappedUser = new AppUser { Email = createUser.Email, PasswordHash = createUser.Password };

            _validationServiceMock
                .Setup(v => v.ValidateAsync(createUser, _createUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = true });
            _mapperMock.Setup(m => m.Map<AppUser>(createUser)).Returns(mappedUser);
            _userManagerMock.Setup(u => u.CreateUserAsync(mappedUser)).ReturnsAsync(false);

            // Act
            var result = await _authenticationService.CreateUser(createUser);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("User already exists.", result.Message);
        }

        [Fact]
        public async Task CreateUser_ShouldReturnError_WhenEmailConfirmationFails()
        {
            // Arrange
            var createUser = new CreateUser { Email = "test@example.com", Password = "Password123", ConfirmPassword = "Password123", FullName = "Test User" };
            var mappedUser = new AppUser { Email = createUser.Email, PasswordHash = createUser.Password };

            _validationServiceMock
                .Setup(v => v.ValidateAsync(createUser, _createUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = true });
            _mapperMock.Setup(m => m.Map<AppUser>(createUser)).Returns(mappedUser);
            _userManagerMock.Setup(u => u.CreateUserAsync(mappedUser)).ReturnsAsync(true);
            _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(mappedUser)).ReturnsAsync("confirmation-token");
            _emailServiceMock.Setup(e => e.SendEmailAsync(createUser.Email, It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Email service error"));

            // Act
            var result = await _authenticationService.CreateUser(createUser);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Failed to send confirmation email.", result.Message);
            _loggerMock.Verify(l => l.LogError(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateUser_ShouldReturnError_WhenRoleAssignmentFails()
        {
            // Arrange
            var createUser = new CreateUser { Email = "test@example.com", Password = "Password123", ConfirmPassword = "Password123", FullName = "Test User" };
            var mappedUser = new AppUser { Email = createUser.Email, PasswordHash = createUser.Password };

            _validationServiceMock
                .Setup(v => v.ValidateAsync(createUser, _createUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = true });
            _mapperMock.Setup(m => m.Map<AppUser>(createUser)).Returns(mappedUser);
            _userManagerMock.Setup(u => u.CreateUserAsync(mappedUser)).ReturnsAsync(true);
            _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(mappedUser)).ReturnsAsync("confirmation-token");
            _userManagerMock.Setup(u => u.GetUserByEmailAsync(createUser.Email)).ReturnsAsync(mappedUser);
            _userManagerMock.Setup(u => u.GetAllUsersAsync()).ReturnsAsync(new List<AppUser> { mappedUser });
            _roleManagerMock.Setup(r => r.AddUserToRoleAsync(mappedUser, "Admin")).ReturnsAsync(false);
            _userManagerMock.Setup(u => u.RemoveUserByEmail(createUser.Email)).ReturnsAsync(0);

            // Act
            var result = await _authenticationService.CreateUser(createUser);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Error occurred in creating account.", result.Message);
            _loggerMock.Verify(l => l.LogError(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateUser_ReturnsValidationError_WhenValidationFails()
        {
            // Arrange
            var createUser = new CreateUser { Email = "invalid@example.com", Password = "123", ConfirmPassword = "123", FullName = "Invalid User" };

            _validationServiceMock
                .Setup(v => v.ValidateAsync(createUser, _createUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = false, Message = "Validation failed" });

            // Act
            var result = await _authenticationService.CreateUser(createUser);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Validation failed", result.Message);
        }

        [Fact]
        public async Task CreateUser_ReturnsError_WhenEmailConfirmationFails()
        {
            // Arrange
            var createUser = new CreateUser { Email = "test@example.com", Password = "Password123", ConfirmPassword = "Password123", FullName = "Test User" };
            var mappedUser = new AppUser { Email = createUser.Email, PasswordHash = createUser.Password };

            _validationServiceMock
                .Setup(v => v.ValidateAsync(createUser, _createUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = true });
            _mapperMock.Setup(m => m.Map<AppUser>(createUser)).Returns(mappedUser);
            _userManagerMock.Setup(u => u.CreateUserAsync(mappedUser)).ReturnsAsync(true);
            _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(mappedUser)).ReturnsAsync("confirmation-token");
            _emailServiceMock
                .Setup(e => e.SendEmailAsync(createUser.Email, It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Email service error"));

            // Act
            var result = await _authenticationService.CreateUser(createUser);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Failed to send confirmation email.", result.Message);
            _loggerMock.Verify(l => l.LogError(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateUser_ReturnsError_WhenRoleAssignmentFails()
        {
            // Arrange
            var createUser = new CreateUser { Email = "test@example.com", Password = "Password123", ConfirmPassword = "Password123", FullName = "Test User" };
            var mappedUser = new AppUser { Email = createUser.Email, PasswordHash = createUser.Password };

            _validationServiceMock
                .Setup(v => v.ValidateAsync(createUser, _createUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = true });
            _mapperMock.Setup(m => m.Map<AppUser>(createUser)).Returns(mappedUser);
            _userManagerMock.Setup(u => u.CreateUserAsync(mappedUser)).ReturnsAsync(true);
            _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(mappedUser)).ReturnsAsync("confirmation-token");
            _userManagerMock.Setup(u => u.GetUserByEmailAsync(createUser.Email)).ReturnsAsync(mappedUser);
            _userManagerMock.Setup(u => u.GetAllUsersAsync()).ReturnsAsync(new List<AppUser> { mappedUser });
            _roleManagerMock.Setup(r => r.AddUserToRoleAsync(mappedUser, "Admin")).ReturnsAsync(false);

            // Act
            var result = await _authenticationService.CreateUser(createUser);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Error occurred in creating account.", result.Message);
            _loggerMock.Verify(l => l.LogError(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task LoginUser_ShouldReturnSuccess_WhenCredentialsAreValid()
        {
            // Arrange
            var loginUser = new LoginUser { Email = "test@example.com", Password = "Password123" };
            var validationResult = new ServiceResponse { Success = true };
            var appUser = new AppUser
            {
                Id = "userId",
                Email = loginUser.Email,
                PasswordHash = loginUser.Password,
                EmailConfirmed = true
            };
            var claims = new List<Claim> { new Claim(ClaimTypes.Name, "Test User") };
            var accessToken = "accessToken";
            var refreshToken = "refreshToken";

            _validationServiceMock
                .Setup(v => v.ValidateAsync(loginUser, this._loginUserValidatorMock.Object))
                .ReturnsAsync(validationResult);

            _mapperMock
                .Setup(m => m.Map<AppUser>(loginUser))
                .Returns(appUser);

            _userManagerMock
                .Setup(u => u.LoginUserAsync(appUser))
                .ReturnsAsync(true);

            _userManagerMock
                .Setup(u => u.GetUserByEmailAsync(loginUser.Email))
                .ReturnsAsync(appUser);

            _userManagerMock
                .Setup(u => u.GetUserClaimsAsync(loginUser.Email))
                .ReturnsAsync(claims);

            _tokenManagerMock
                .Setup(t => t.GenerateAccessToken(claims))
                .Returns(accessToken);

            _tokenManagerMock
                .Setup(t => t.GetReFreshToken())
                .Returns(refreshToken);

            _tokenManagerMock
                .Setup(t => t.ValidateRefreshTokenAsync(refreshToken))
                .ReturnsAsync(true);

            _tokenManagerMock
                .Setup(t => t.UpdateRefreshTokenAsync(appUser.Id, refreshToken))
                .ReturnsAsync(1);

            // Act
            var result = await _authenticationService.LoginUser(loginUser);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("Login successful.", result.Message);
            Assert.Equal(accessToken, result.Token);
            Assert.Equal(refreshToken, result.RefreshToken);
        }


        [Fact]
        public async Task LoginUser_ReturnsError_WhenValidationFails()
        {
            // Arrange
            var user = new LoginUser { Email = "test@example.com", Password = "wrongpassword" };
            _validationServiceMock
                .Setup(v => v.ValidateAsync(user, _loginUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = false, Message = "Validation failed." });

            // Act
            var result = await _authenticationService.LoginUser(user);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Validation failed.", result.Message);
        }

        [Fact]
        public async Task LoginUser_ReturnsError_WhenLoginFails()
        {
            // Arrange
            var user = new LoginUser { Email = "test@example.com", Password = "wrongpassword" };
            var appUser = new AppUser { Email = user.Email, PasswordHash = user.Password };

            _validationServiceMock
                .Setup(v => v.ValidateAsync(user, _loginUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = true });

            _mapperMock
                .Setup(m => m.Map<AppUser>(It.IsAny<LoginUser>()))
                .Returns(appUser);

            _userManagerMock
                .Setup(u => u.LoginUserAsync(It.IsAny<AppUser>()))
                .ReturnsAsync(false);

            // Act
            var result = await _authenticationService.LoginUser(user);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Invalid credentials.", result.Message);
        }

        [Fact]
        public async Task LoginUser_AddsNewRefreshToken_WhenTokenIsInvalid()
        {
            // Arrange
            var user = new LoginUser { Email = "test@example.com", Password = "Password123" };
            var appUser = new AppUser { Id = "userId", Email = user.Email, EmailConfirmed = false };

            _validationServiceMock
                .Setup(v => v.ValidateAsync(user, _loginUserValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = true });

            _mapperMock
                .Setup(m => m.Map<AppUser>(It.IsAny<LoginUser>()))
                .Returns(appUser);

            _userManagerMock.Setup(u => u.LoginUserAsync(It.IsAny<AppUser>())).ReturnsAsync(true);
            _userManagerMock.Setup(u => u.GetUserByEmailAsync(user.Email)).ReturnsAsync(appUser);
            _userManagerMock.Setup(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<AppUser>())).ReturnsAsync("valid_token");

            _tokenManagerMock.Setup(t => t.ValidateRefreshTokenAsync(It.IsAny<string>())).ReturnsAsync(false);
            _tokenManagerMock.Setup(t => t.AddRefreshTokenAsync(appUser.Id, It.IsAny<string>())).ReturnsAsync(1);

            // Act
            var result = await _authenticationService.LoginUser(user);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Email not confirmed. A new confirmation link has been sent to your email.", result.Message);
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

            _tokenManagerMock.Setup(t => t.ValidateRefreshTokenAsync(refreshToken)).ReturnsAsync(true);
            _tokenManagerMock.Setup(t => t.GetUserIdByRefreshTokenAsync(refreshToken)).ReturnsAsync(userId);
            _userManagerMock.Setup(u => u.GetUserByIdAsync(userId)).ReturnsAsync(appUser);
            _userManagerMock.Setup(u => u.GetUserClaimsAsync(appUser.Email)).ReturnsAsync(claims);
            _tokenManagerMock.Setup(t => t.GenerateAccessToken(claims)).Returns(newAccessToken);
            _tokenManagerMock.Setup(t => t.GetReFreshToken()).Returns(newRefreshToken);

            // Act
            var result = await _authenticationService.ReviveToken(refreshToken);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Token revived successfully.", result.Message);
            Assert.Equal(newAccessToken, result.Token);
            Assert.Equal(newRefreshToken, result.RefreshToken);
        }

        [Fact]
        public async Task ReviveToken_ReturnsError_WhenTokenIsInvalid()
        {
            // Arrange
            var invalidToken = "invalid_refresh_token";

            _tokenManagerMock
                .Setup(t => t.ValidateRefreshTokenAsync(invalidToken))
                .ReturnsAsync(false);

            // Act
            var result = await _authenticationService.ReviveToken(invalidToken);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Invalid token.", result.Message);
        }

        [Fact]
        public async Task ReviveToken_ReturnsError_WhenUserIdIsNullOrEmpty()
        {
            // Arrange
            var refreshToken = "invalid_refresh_token";

            _tokenManagerMock
                .Setup(t => t.ValidateRefreshTokenAsync(refreshToken))
                .ReturnsAsync(true);

            _tokenManagerMock
                .Setup(t => t.GetUserIdByRefreshTokenAsync(refreshToken))
                .ReturnsAsync((string)null);

            // Act
            var result = await _authenticationService.ReviveToken(refreshToken);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Invalid token.", result.Message);
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
        public async Task ChangePassword_ReturnsError_WhenUserNotFound()
        {
            // Arrange
            var changePasswordDto = new ChangePassword { CurrentPassword = "oldPassword", NewPassword = "newPassword", ConfirmPassword = "newPassword" };

            _userManagerMock
                .Setup(u => u.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((AppUser)null);

            // Act
            var result = await _authenticationService.ChangePassword(changePasswordDto, "invalid-user-id");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("User not found.", result.Message);
        }


        [Fact]
        public async Task ChangePassword_ReturnsValidationError_WhenValidationFails()
        {
            // Arrange
            var changePasswordDto = new ChangePassword
                                        {
                                            CurrentPassword = "oldPassword",
                                            NewPassword = "newPassword",
                                            ConfirmPassword = "newPassword"
                                        };
            var user = new AppUser { Id = "userId", Email = "test@example.com" };

            _userManagerMock.Setup(u => u.GetUserByIdAsync("userId")).ReturnsAsync(user);

            _validationServiceMock
                .Setup(v => v.ValidateAsync(changePasswordDto, _changePasswordValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = false, Message = "Validation failed" });

            // Act
            var result = await _authenticationService.ChangePassword(changePasswordDto, "userId");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Validation failed", result.Message);
        }

        [Fact]
        public async Task ChangePassword_ReturnsError_WhenChangePasswordFails()
        {
            // Arrange
            var changePasswordDto = new ChangePassword
                                        {
                                            CurrentPassword = "oldPassword",
                                            NewPassword = "newPassword",
                                            ConfirmPassword = "newPassword"
                                        };
            var user = new AppUser { Id = "userId", Email = "test@example.com" };

            _userManagerMock.Setup(u => u.GetUserByIdAsync("userId")).ReturnsAsync(user);

            _validationServiceMock
                .Setup(v => v.ValidateAsync(changePasswordDto, _changePasswordValidatorMock.Object))
                .ReturnsAsync(new ServiceResponse { Success = true });

            _userManagerMock
                .Setup(u => u.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword))
                .ReturnsAsync(false);

            // Act
            var result = await _authenticationService.ChangePassword(changePasswordDto, "userId");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Error occurred while changing password.", result.Message);
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

        [Fact]
        public async Task ConfirmEmail_WithValidToken_ReturnsSuccess()
        {
            // Arrange
            var userId = "user-id";
            var token = "valid-token";
            var user = new AppUser { Id = userId, Email = "test@example.com" };

            _userManagerMock.Setup(u => u.GetUserByIdAsync(userId))
                .ReturnsAsync(user);

            _userManagerMock.Setup(u => u.ConfirmEmailAsync(user, token))
                .ReturnsAsync(true);

            // Act
            var result = await _authenticationService.ConfirmEmail(userId, token);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Email confirmed successfully.", result.Message);
        }

        [Fact]
        public async Task ConfirmEmail_ReturnsError_WhenUserNotFound()
        {
            // Arrange
            var userId = "invalid_user_id";
            var token = "sample_token";

            _userManagerMock
                .Setup(u => u.GetUserByIdAsync(userId))
                .ReturnsAsync((AppUser)null);

            // Act
            var result = await _authenticationService.ConfirmEmail(userId, token);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Invalid user ID.", result.Message);
        }

        [Fact]
        public async Task ConfirmEmail_ReturnsError_WhenConfirmationFails()
        {
            // Arrange
            var userId = "valid_user_id";
            var token = "valid_token";
            var user = new AppUser { Id = userId, Email = "test@example.com" };

            _userManagerMock
                .Setup(u => u.GetUserByIdAsync(userId))
                .ReturnsAsync(user);

            _userManagerMock
                .Setup(u => u.ConfirmEmailAsync(user, token))
                .ReturnsAsync(false);

            // Act
            var result = await _authenticationService.ConfirmEmail(userId, token);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Email confirmation failed.", result.Message);
        }

        [Fact]
        public async Task SendConfirmationEmail_SendsEmail()
        {
            // Arrange
            var email = "test@example.com";
            var confirmationLink = "http://example.com/confirm";

            // Act
            await _authenticationService.SendConfirmationEmail(email, confirmationLink);

            // Assert
            _emailServiceMock.Verify(e => e.SendEmailAsync(email, "Confirm your email", confirmationLink), Times.Once);
        }
    }
}