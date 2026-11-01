namespace BlazorShop.Application.Services.Authentication
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Authentication;
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Application.Validations;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Entities.Identity;

    using FluentValidation;

    using Microsoft.Extensions.Options;

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IAppTokenManager _tokenManager;
        private readonly IAppUserManager _userManager;
        private readonly IAppRoleManager _roleManager;
        private readonly IAppLogger<AuthenticationService> _logger;
        private readonly IMapper _mapper;
        private readonly IValidator<CreateUser> _createUserValidator;
        private readonly IValidator<LoginUser> _loginUserValidator;
        private readonly IValidator<ChangePassword> _changePasswordValidator;
        private readonly IValidationService _validationService;
        private readonly IEmailService _emailService;
        private readonly IRegistrationConfirmationStore _confirmationStore;
        private readonly IdentityConfirmationOptions _identityConfirmationOptions;

        public AuthenticationService(
            IAppTokenManager tokenManager,
            IAppUserManager userManager,
            IAppRoleManager roleManager,
            IAppLogger<AuthenticationService> logger,
            IMapper mapper,
            IValidator<CreateUser> createUserValidator,
            IValidator<LoginUser> loginUserValidator,
            IValidationService validationService,
            IValidator<ChangePassword> changePasswordValidator,
            IEmailService emailService,
            IRegistrationConfirmationStore confirmationStore,
            IOptions<IdentityConfirmationOptions> identityConfirmationOptions)
        {
            _tokenManager = tokenManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _mapper = mapper;
            _createUserValidator = createUserValidator;
            _loginUserValidator = loginUserValidator;
            _validationService = validationService;
            _changePasswordValidator = changePasswordValidator;
            _emailService = emailService;
            _confirmationStore = confirmationStore;
            _identityConfirmationOptions = identityConfirmationOptions.Value;
        }

        public async Task<ServiceResponse> CreateUser(CreateUser user)
        {
            var validationResult = await _validationService.ValidateAsync(user, _createUserValidator);

            if (!validationResult.Success)
            {
                return validationResult;
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return new ServiceResponse { Message = "Email is required." };
            }

            var mappedUser = _mapper.Map<AppUser>(user);
            mappedUser.UserName = user.Email;
            mappedUser.PasswordHash = user.Password;

            if (RequiresConfirmedEmail())
            {
                var committed = await _confirmationStore.RegisterCustomerAsync(mappedUser);
                return committed switch
                {
                    RegistrationCommitResult.Created => new ServiceResponse(true, "User created successfully."),
                    RegistrationCommitResult.Duplicate => new ServiceResponse(false, "User already exists."),
                    RegistrationCommitResult.DemoNotSupported => new ServiceResponse(false, "Strict registration is unavailable in an isolated demo workspace."),
                    _ => new ServiceResponse(false, "Registration could not be confirmed. Try signing in or retrying later."),
                };
            }

            var result = await _userManager.CreateUserAsync(mappedUser);

            if (!result)
            {
                return new ServiceResponse { Message = "User already exists." };
            }

            var currentUser = await _userManager.GetUserByEmailAsync(user.Email);
            if (currentUser == null)
            {
                await RollbackCreatedUserAsync(user.Email, "Unable to remove user after create because the newly created user could not be loaded.");
                return new ServiceResponse { Message = "Unable to load the newly created user." };
            }

            bool assignedResult = await _roleManager.AddUserToRoleAsync(currentUser, "User");

            if (!assignedResult)
            {
                await RollbackCreatedUserAsync(currentUser.Email,
                    $"User with Email {currentUser.Email} failed to be removed after failed role assignment.");
                return new ServiceResponse { Message = "Error occurred in creating account." };
            }

            await TryConfirmUserForNonStrictRegistrationAsync(currentUser);

            return new ServiceResponse { Success = true, Message = "User created successfully." };
        }

        public async Task<LoginResponse> LoginUser(LoginUser user, string? ipAddress = null, string? userAgent = null)
        {
            var validationResult = await _validationService.ValidateAsync(user, _loginUserValidator);

            if (!validationResult.Success)
            {
                return new LoginResponse { Message = validationResult.Message ?? "Validation failed." };
            }

            var mappedUser = _mapper.Map<AppUser>(user);
            mappedUser.PasswordHash = user.Password;

            var currentUser = await _userManager.GetUserByEmailAsync(user.Email);
            if (currentUser == null)
            {
                return new LoginResponse { Message = "Invalid credentials." };
            }

            var loginResult = await _userManager.LoginUserAsync(mappedUser);

            if (!loginResult.Success)
            {
                if (loginResult.IsLockedOut)
                {
                    return new LoginResponse { Message = "Account is temporarily locked. Please try again later." };
                }

                if (loginResult.IsNotAllowed && RequiresConfirmedSignIn() && !currentUser.EmailConfirmed)
                {
                    return await CreateConfirmationRequiredResponseAsync(currentUser, user.Password);
                }

                return new LoginResponse { Message = "Invalid credentials." };
            }

            if (RequiresConfirmedSignIn() && !currentUser.EmailConfirmed)
            {
                return await CreateConfirmationRequiredResponseAsync(currentUser, user.Password);
            }


            var confirmedEmail = currentUser.Email;
            if (string.IsNullOrWhiteSpace(confirmedEmail))
            {
                return new LoginResponse { Message = "Email not available for user." };
            }

            var claims = await _userManager.GetUserClaimsAsync(confirmedEmail);

            var accessToken = _tokenManager.GenerateAccessToken(claims);
            var refreshToken = _tokenManager.GetReFreshToken();

            var refreshTokenEntry = _tokenManager.CreateRefreshToken(currentUser.Id, refreshToken, ipAddress, userAgent);
            var saveTokenResult = await _tokenManager.AddRefreshTokenAsync(refreshTokenEntry);

            return saveTokenResult <= 0
                ? new LoginResponse { Message = "Error occurred in login." }
                : new LoginResponse { Success = true, Message = "Login successful.", Token = accessToken, RefreshToken = refreshToken };

        }

        public async Task<LoginResponse> ReviveToken(string refreshToken, string? ipAddress = null, string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            var rotation = await _tokenManager.RotateRefreshTokenAsync(refreshToken, ipAddress, userAgent, cancellationToken);
            return rotation is null || cancellationToken.IsCancellationRequested
                ? new LoginResponse { Message = "Invalid token." }
                : new LoginResponse { Success = true, Message = "Token revived successfully.",
                    Token = _tokenManager.GenerateAccessToken(rotation.Claims), RefreshToken = rotation.RefreshToken };
        }

        public async Task<ServiceResponse> Logout(string refreshToken, string? ipAddress = null)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return new ServiceResponse(true, "Logged out successfully.");
            var revoked = await _tokenManager.RevokeRefreshSessionAsync(refreshToken, ipAddress);
            return revoked ? new ServiceResponse(true, "Logged out successfully.")
                : new ServiceResponse(false, "Unable to confirm logout.");
        }

        public async Task<ServiceResponse> ChangePassword(ChangePassword changePasswordDto, string userId)
        {
            var user = await _userManager.GetUserByIdAsync(userId);
            if (user == null)
            {
                return new ServiceResponse { Message = "User not found." };
            }

            var validationResult = await this._validationService.ValidateAsync(
                                       changePasswordDto,
                                       _changePasswordValidator);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            var result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
            if (!result)
            {
                return new ServiceResponse { Message = "Error occurred while changing password." };
            }

            _logger.LogInformation($"Password changed successfully for user with ID: {userId}");

            return new ServiceResponse { Success = true, Message = "Password changed successfully." };
        }

        public async Task<bool> CheckPasswordAsync(AppUser user, string password)
        {
            return await _userManager.CheckPasswordAsync(user, password);
        }

        public async Task<ServiceResponse> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.GetUserByIdAsync(userId);
            if (user == null)
            {
                return new ServiceResponse { Message = "Invalid user ID." };
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result)
            {
                return new ServiceResponse { Message = "Email confirmation failed." };
            }

            return new ServiceResponse { Success = true, Message = "Email confirmed successfully." };
        }

        public async Task SendConfirmationEmail(string email, string confirmationLink)
        {
            await _emailService.SendEmailAsync(email, "Confirm your email", confirmationLink);
        }

        public async Task<ServiceResponse> UpdateProfile(string userId, UpdateProfile dto)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new ServiceResponse(false, "Invalid user id.");
            }

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.FullName))
            {
                return new ServiceResponse(false, "Full name and email are required.");
            }

            var ok = await _userManager.UpdateUserAsync(userId, dto.FullName, dto.Email, dto.PhoneNumber);
            return ok
                ? new ServiceResponse(true, "Profile updated successfully.")
                : new ServiceResponse(false, "Failed to update profile.");
        }

        private bool RequiresConfirmedSignIn()
        {
            return _identityConfirmationOptions.RequireConfirmedAccount || _identityConfirmationOptions.RequireConfirmedEmail;
        }

        private bool RequiresConfirmedEmail()
        {
            return _identityConfirmationOptions.RequireConfirmedEmail;
        }

        private async Task<LoginResponse> CreateConfirmationRequiredResponseAsync(AppUser currentUser, string password)
        {
            await _confirmationStore.RequestDeliveryAsync(currentUser.Id, password);
            _logger.LogWarning($"Unconfirmed sign-in was refused for account {currentUser.Id}.");

            return new LoginResponse
                       {
                           Success = false,
                           // Same public response for eligibility, coalescing, rate limiting
                           // and storage failure; do not expose a password/resend oracle.
                           Message = "Email not confirmed. If eligible, confirmation delivery has been queued; requests are subject to delivery limits."
                       };
        }

        private async Task TryConfirmUserForNonStrictRegistrationAsync(AppUser currentUser)
        {
            if (currentUser.EmailConfirmed)
            {
                return;
            }

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(currentUser);
                var confirmationResult = await _userManager.ConfirmEmailAsync(currentUser, token);
                if (!confirmationResult)
                {
                    _logger.LogWarning($"User was created without confirmation requirements, but local email confirmation could not be completed. Email: {currentUser.Email}, UserId: {currentUser.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to locally confirm email for user created without confirmation requirements. Email: {currentUser.Email}, UserId: {currentUser.Id}");
            }
        }

        private async Task RollbackCreatedUserAsync(string? email, string rollbackFailureMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogError(
                    new InvalidOperationException("Cannot remove user because email is missing."),
                    "Cannot remove user because email is missing.");
                return;
            }

            var removeUserResult = await _userManager.RemoveUserByEmail(email);
            if (removeUserResult <= 0)
            {
                _logger.LogError(new Exception(rollbackFailureMessage), rollbackFailureMessage);
            }
        }
    }
}
