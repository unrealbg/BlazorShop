﻿namespace BlazorShop.Application.Services.Authentication
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Application.Services.Contracts.Authentication;
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Application.Validations;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Entities.Identity;

    using FluentValidation;

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IAppTokenManager _tokenManager;
        private readonly IAppUserManager _userManager;
        private readonly IAppRoleManager _roleManager;
        private readonly IAppLogger<AuthenticationService> _logger;
        private readonly IMapper _mapper;
        private readonly IValidator<CreateUser> _createUserValidator;
        private readonly IValidator<LoginUser> _loginUserValidator;
        private readonly IValidationService _validationService;

        public AuthenticationService(
            IAppTokenManager tokenManager,
            IAppUserManager userManager,
            IAppRoleManager roleManager,
            IAppLogger<AuthenticationService> logger,
            IMapper mapper,
            IValidator<CreateUser> createUserValidator,
            IValidator<LoginUser> loginUserValidator,
            IValidationService validationService)
        {
            _tokenManager = tokenManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _mapper = mapper;
            _createUserValidator = createUserValidator;
            _loginUserValidator = loginUserValidator;
            _validationService = validationService;
        }

        // TODO: Email Confirmation
        public async Task<ServiceResponse> CreateUser(CreateUser user)
        {
            var validationResult = await _validationService.ValidateAsync(user, _createUserValidator);

            if (!validationResult.Success)
            {
                return validationResult;
            }

            var mappedUser = _mapper.Map<AppUser>(user);
            mappedUser.UserName = user.Email;
            mappedUser.PasswordHash = user.Password;

            var result = await _userManager.CreateUserAsync(mappedUser);

            if (!result)
            {
                return new ServiceResponse { Message = "User already exists." };
            }

            var currentUser = await _userManager.GetUserByEmailAsync(user.Email);
            var users = await _userManager.GetAllUsersAsync();

            bool assignedResult = await _roleManager.AddUserToRoleAsync(currentUser!, users!.Count() > 1 ? "User" : "Admin");

            if (!assignedResult)
            {
                var removeUserResult = await _userManager.RemoveUserByEmail(currentUser!.Email!);

                if (removeUserResult <= 0)
                {
                    this._logger.LogError(new Exception($"User with Email as {currentUser!.Email} failed to be removed as a result of failed role assignment."), "User could not be assigned a role and could not be removed.");
                    return new ServiceResponse { Message = "Error occurred in creating account." };
                }

                return new ServiceResponse { Message = "Error occurred in creating account." };
            }

            return new ServiceResponse { Success = true, Message = "User created successfully." };
        }

        public async Task<LoginResponse> LoginUser(LoginUser user)
        {
            var validationResult = await _validationService.ValidateAsync(user, _loginUserValidator);

            if (!validationResult.Success)
            {
                return new LoginResponse { Message = validationResult.Message };
            }

            var mappedUser = _mapper.Map<AppUser>(user);
            mappedUser.PasswordHash = user.Password;

            var loginResult = await _userManager.LoginUserAsync(mappedUser);

            if (!loginResult)
            {
                return new LoginResponse { Message = "Invalid credentials." };
            }

            var currentUser = await _userManager.GetUserByEmailAsync(user.Email);
            var claims = await _userManager.GetUserClaimsAsync(currentUser!.Email!);

            var accessToken = _tokenManager.GenerateAccessToken(claims);
            var refreshToken = _tokenManager.GetReFreshToken();

            var saveTokenResult = 0;
            var isRefreshTokenValid = await _tokenManager.ValidateRefreshTokenAsync(refreshToken);

            if (isRefreshTokenValid)
            {
                saveTokenResult = await _tokenManager.UpdateRefreshTokenAsync(currentUser.Id, refreshToken);
            }
            else
            {
                saveTokenResult = await _tokenManager.AddRefreshTokenAsync(currentUser.Id, refreshToken);
            }

            return saveTokenResult <= 0
                ? new LoginResponse { Message = "Error occurred in login." }
                : new LoginResponse { Success = true, Message = "Login successful.", Token = accessToken, RefreshToken = refreshToken };

        }

        public async Task<LoginResponse> ReviveToken(string refreshToken)
        {
            var validateTokenResult = await _tokenManager.ValidateRefreshTokenAsync(refreshToken);

            if (!validateTokenResult)
            {
                return new LoginResponse { Message = "Invalid token." };
            }

            var userId = await _tokenManager.GetUserIdByRefreshTokenAsync(refreshToken);

            if (string.IsNullOrEmpty(userId))
            {
                return new LoginResponse { Message = "Invalid token." };
            }

            var currentUser = await _userManager.GetUserByIdAsync(userId);
            var claims = await _userManager.GetUserClaimsAsync(currentUser!.Email!);
            var newAccessToken = _tokenManager.GenerateAccessToken(claims);
            var newRefreshToken = _tokenManager.GetReFreshToken();
            //var saveTokenResult = await _tokenManager.UpdateRefreshTokenAsync(userId, newRefreshToken);

            return new LoginResponse { Success = true, Message = "Token revived successfully.", Token = newAccessToken, RefreshToken = newRefreshToken };
        }
    }
}
