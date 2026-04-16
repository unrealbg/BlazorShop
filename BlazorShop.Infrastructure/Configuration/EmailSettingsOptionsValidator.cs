namespace BlazorShop.Infrastructure.Configuration
{
    using System.Net.Mail;

    using BlazorShop.Application.DTOs;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;

    public sealed class EmailSettingsOptionsValidator : IValidateOptions<EmailSettings>
    {
        private readonly IHostEnvironment _hostEnvironment;

        public EmailSettingsOptionsValidator(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public ValidateOptionsResult Validate(string? name, EmailSettings options)
        {
            if (_hostEnvironment.IsDevelopment())
            {
                return ValidateOptionsResult.Success;
            }

            List<string> failures = [];

            if (IsMissingOrPlaceholder(options.From) || !IsValidEmailAddress(options.From))
            {
                failures.Add("EmailSettings:From is required and must be a valid email address outside Development.");
            }

            if (IsMissingOrPlaceholder(options.SmtpServer))
            {
                failures.Add("EmailSettings:SmtpServer is required outside Development.");
            }

            if (options.Port <= 0 || options.Port > 65535)
            {
                failures.Add("EmailSettings:Port must be between 1 and 65535.");
            }

            if (IsMissingOrPlaceholder(options.Username))
            {
                failures.Add("EmailSettings:Username is required outside Development.");
            }

            if (IsMissingOrPlaceholder(options.Password))
            {
                failures.Add("EmailSettings:Password is required outside Development.");
            }

            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }

        private static bool IsMissingOrPlaceholder(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                || (value.StartsWith('<') && value.EndsWith('>'));
        }

        private static bool IsValidEmailAddress(string value)
        {
            try
            {
                _ = new MailAddress(value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}