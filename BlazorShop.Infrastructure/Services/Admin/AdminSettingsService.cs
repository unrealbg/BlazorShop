namespace BlazorShop.Infrastructure.Services.Admin
{
    using System.Globalization;
    using System.Net.Mail;
    using System.Runtime.InteropServices;
    using System.Security.Claims;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Admin.Settings;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;

    public class AdminSettingsService : IAdminSettingsService
    {
        private static readonly Regex CurrencyRegex = new("^[A-Z]{3}$", RegexOptions.Compiled);
        private static readonly Regex PrefixRegex = new("^[A-Za-z0-9-]{1,16}$", RegexOptions.Compiled);
        private static readonly HashSet<string> ShippingStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "PendingShipment",
            "Shipped",
            "InTransit",
            "OutForDelivery",
            "Delivered",
        };

        private readonly AppDbContext _db;
        private readonly EmailSettings _emailSettings;
        private readonly IHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAdminAuditService _auditService;

        public AdminSettingsService(
            AppDbContext db,
            IOptions<EmailSettings> emailSettings,
            IHostEnvironment environment,
            IHttpContextAccessor httpContextAccessor,
            IAdminAuditService auditService)
        {
            _db = db;
            _emailSettings = emailSettings.Value;
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;
            _auditService = auditService;
        }

        public async Task<AdminSettingsDto> GetAsync()
        {
            var settings = await GetOrCreateAsync();
            return Map(settings);
        }

        public async Task<ServiceResponse<StoreSettingsDto>> UpdateStoreAsync(UpdateStoreSettingsDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var validationMessage = ValidateStore(request);
            if (validationMessage is not null)
            {
                return Failure<StoreSettingsDto>(validationMessage, ServiceResponseType.ValidationError);
            }

            var settings = await GetOrCreateAsync();
            settings.StoreName = request.StoreName.Trim();
            settings.StoreSupportEmail = Normalize(request.StoreSupportEmail);
            settings.StoreSupportPhone = Normalize(request.StoreSupportPhone);
            settings.DefaultCurrency = request.DefaultCurrency.Trim().ToUpperInvariant();
            settings.DefaultCulture = request.DefaultCulture.Trim();
            settings.MaintenanceModeEnabled = request.MaintenanceModeEnabled;
            settings.MaintenanceMessage = Normalize(request.MaintenanceMessage);
            Touch(settings);

            await _db.SaveChangesAsync();
            await LogAsync("AdminSettings.StoreUpdated", "Store settings updated.", settings);

            return Success(MapStore(settings), "Store settings updated successfully.");
        }

        public async Task<ServiceResponse<OrderSettingsDto>> UpdateOrdersAsync(UpdateOrderSettingsDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var validationMessage = ValidateOrders(request);
            if (validationMessage is not null)
            {
                return Failure<OrderSettingsDto>(validationMessage, ServiceResponseType.ValidationError);
            }

            var settings = await GetOrCreateAsync();
            settings.AllowGuestCheckout = false;
            settings.DefaultShippingStatus = request.DefaultShippingStatus.Trim();
            settings.AutoConfirmPaidOrders = request.AutoConfirmPaidOrders;
            settings.OrderReferencePrefix = request.OrderReferencePrefix.Trim().ToUpperInvariant();
            Touch(settings);

            await _db.SaveChangesAsync();
            await LogAsync("AdminSettings.OrdersUpdated", "Order settings updated.", settings);

            return Success(MapOrders(settings), "Order settings updated successfully.");
        }

        public async Task<ServiceResponse<NotificationSettingsDto>> UpdateNotificationsAsync(UpdateNotificationSettingsDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var validationMessage = ValidateNotifications(request);
            if (validationMessage is not null)
            {
                return Failure<NotificationSettingsDto>(validationMessage, ServiceResponseType.ValidationError);
            }

            var settings = await GetOrCreateAsync();
            settings.SmtpHost = Normalize(request.SmtpHost);
            settings.SmtpFromEmail = Normalize(request.SmtpFromEmail);
            settings.SmtpFromDisplayName = Normalize(request.SmtpFromDisplayName);
            Touch(settings);

            await _db.SaveChangesAsync();
            await LogAsync("AdminSettings.NotificationsUpdated", "Notification settings updated.", settings);

            return Success(MapNotifications(settings), "Notification settings updated successfully.");
        }

        private async Task<AdminSettings> GetOrCreateAsync()
        {
            var settings = await _db.AdminSettings.FirstOrDefaultAsync();
            if (settings is not null)
            {
                return settings;
            }

            settings = new AdminSettings
            {
                SmtpHost = _emailSettings.SmtpServer,
                SmtpFromEmail = _emailSettings.From,
                SmtpFromDisplayName = _emailSettings.DisplayName,
                UpdatedOn = DateTime.UtcNow,
            };

            _db.AdminSettings.Add(settings);
            await _db.SaveChangesAsync();

            return settings;
        }

        private AdminSettingsDto Map(AdminSettings settings)
        {
            return new AdminSettingsDto
            {
                Store = MapStore(settings),
                Orders = MapOrders(settings),
                Notifications = MapNotifications(settings),
                System = new SystemSettingsDto
                {
                    UpdatedOn = settings.UpdatedOn,
                    UpdatedByUserId = settings.UpdatedByUserId,
                    RuntimeEnvironment = _environment.EnvironmentName,
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                },
            };
        }

        private StoreSettingsDto MapStore(AdminSettings settings)
        {
            return new StoreSettingsDto
            {
                StoreName = settings.StoreName,
                StoreSupportEmail = settings.StoreSupportEmail,
                StoreSupportPhone = settings.StoreSupportPhone,
                DefaultCurrency = settings.DefaultCurrency,
                DefaultCulture = settings.DefaultCulture,
                MaintenanceModeEnabled = settings.MaintenanceModeEnabled,
                MaintenanceMessage = settings.MaintenanceMessage,
            };
        }

        private static OrderSettingsDto MapOrders(AdminSettings settings)
        {
            return new OrderSettingsDto
            {
                AllowGuestCheckout = false,
                GuestCheckoutSupported = false,
                DefaultShippingStatus = settings.DefaultShippingStatus,
                AutoConfirmPaidOrders = settings.AutoConfirmPaidOrders,
                OrderReferencePrefix = settings.OrderReferencePrefix,
            };
        }

        private NotificationSettingsDto MapNotifications(AdminSettings settings)
        {
            return new NotificationSettingsDto
            {
                SmtpHost = string.IsNullOrWhiteSpace(settings.SmtpHost) ? _emailSettings.SmtpServer : settings.SmtpHost,
                SmtpFromEmail = string.IsNullOrWhiteSpace(settings.SmtpFromEmail) ? _emailSettings.From : settings.SmtpFromEmail,
                SmtpFromDisplayName = string.IsNullOrWhiteSpace(settings.SmtpFromDisplayName) ? _emailSettings.DisplayName : settings.SmtpFromDisplayName,
                SecretsConfigured = !string.IsNullOrWhiteSpace(_emailSettings.Password) || !string.IsNullOrWhiteSpace(_emailSettings.Username),
            };
        }

        private void Touch(AdminSettings settings)
        {
            settings.UpdatedOn = DateTime.UtcNow;
            settings.UpdatedByUserId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        private async Task LogAsync(string action, string summary, AdminSettings settings)
        {
            var metadata = JsonSerializer.Serialize(new
            {
                settings.StoreName,
                settings.DefaultCurrency,
                settings.DefaultCulture,
                settings.DefaultShippingStatus,
                settings.OrderReferencePrefix,
                settings.MaintenanceModeEnabled,
            });

            await _auditService.LogAsync(new CreateAdminAuditLogDto
            {
                Action = action,
                EntityType = "AdminSettings",
                EntityId = settings.Id.ToString(),
                Summary = summary,
                MetadataJson = metadata,
            });
        }

        private static string? ValidateStore(UpdateStoreSettingsDto request)
        {
            if (string.IsNullOrWhiteSpace(request.StoreName))
            {
                return "Store name is required.";
            }

            if (!string.IsNullOrWhiteSpace(request.StoreSupportEmail) && !IsEmail(request.StoreSupportEmail))
            {
                return "Store support email is invalid.";
            }

            if (string.IsNullOrWhiteSpace(request.DefaultCurrency) || !CurrencyRegex.IsMatch(request.DefaultCurrency.Trim().ToUpperInvariant()))
            {
                return "Default currency must be a three-letter ISO currency code.";
            }

            if (string.IsNullOrWhiteSpace(request.DefaultCulture))
            {
                return "Default culture is required.";
            }

            try
            {
                CultureInfo.GetCultureInfo(request.DefaultCulture.Trim());
            }
            catch (CultureNotFoundException)
            {
                return "Default culture is invalid.";
            }

            return null;
        }

        private static string? ValidateOrders(UpdateOrderSettingsDto request)
        {
            if (request.AllowGuestCheckout)
            {
                return "Guest checkout is not currently supported by this storefront.";
            }

            if (string.IsNullOrWhiteSpace(request.DefaultShippingStatus) || !ShippingStatuses.Contains(request.DefaultShippingStatus.Trim()))
            {
                return "Default shipping status is invalid.";
            }

            if (string.IsNullOrWhiteSpace(request.OrderReferencePrefix) || !PrefixRegex.IsMatch(request.OrderReferencePrefix.Trim()))
            {
                return "Order reference prefix must contain only letters, numbers, or hyphens.";
            }

            return null;
        }

        private static string? ValidateNotifications(UpdateNotificationSettingsDto request)
        {
            if (!string.IsNullOrWhiteSpace(request.SmtpFromEmail) && !IsEmail(request.SmtpFromEmail))
            {
                return "SMTP from email is invalid.";
            }

            return null;
        }

        private static bool IsEmail(string email)
        {
            try
            {
                _ = new MailAddress(email.Trim());
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static ServiceResponse<TPayload> Success<TPayload>(TPayload payload, string message)
        {
            return new ServiceResponse<TPayload>(true, message)
            {
                Payload = payload,
                ResponseType = ServiceResponseType.Success,
            };
        }

        private static ServiceResponse<TPayload> Failure<TPayload>(string message, ServiceResponseType responseType)
        {
            return new ServiceResponse<TPayload>(false, message)
            {
                ResponseType = responseType,
            };
        }
    }
}
