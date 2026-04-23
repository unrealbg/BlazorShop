namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Domain.Contracts;

    using MailKit.Net.Smtp;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using MimeKit;

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_emailSettings.DisplayName, _emailSettings.From));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

            using var smtp = new SmtpClient();
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, _emailSettings.UseSsl, cts.Token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(_emailSettings.Username))
                {
                    await smtp.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password, cts.Token).ConfigureAwait(false);
                }
                await smtp.SendAsync(email, cts.Token).ConfigureAwait(false);
                await smtp.DisconnectAsync(true, cts.Token).ConfigureAwait(false);

                _logger.LogInformation("Email sent to {ToEmail} with subject {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail} with subject {Subject}", toEmail, subject);
                throw;
            }
        }
    }
}
