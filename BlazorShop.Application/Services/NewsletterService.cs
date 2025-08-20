namespace BlazorShop.Application.Services
{
    using System;
    using System.Linq;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;

    public class NewsletterService : INewsletterService
    {
        private readonly IGenericRepository<Domain.Entities.NewsletterSubscriber> _repo;
        private readonly IEmailService _emailService;

        public NewsletterService(IGenericRepository<Domain.Entities.NewsletterSubscriber> repo, IEmailService emailService)
        {
            _repo = repo;
            _emailService = emailService;
        }

        public async Task<ServiceResponse> SubscribeAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new ServiceResponse(false, "Email is required.");
            }

            email = email.Trim();

            var exists = (await _repo.GetAllAsync()).Any(x => x.Email == email);
            if (exists)
                return new ServiceResponse(true, "Already subscribed.");

            try
            {
                var added = await _repo.AddAsync(new Domain.Entities.NewsletterSubscriber { Email = email, CreatedOn = DateTime.UtcNow });
                if (added <= 0)
                    return new ServiceResponse(false, "Failed to subscribe.");
            }
            catch
            {
                return new ServiceResponse(false, "Subscription failed.");
            }

            _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendEmailAsync(
                            email,
                            "Welcome to BlazorShop Newsletter",
                            "<p>Thank you for subscribing!</p>");
                    }
                    catch
                    {
                        /* ignore */
                    }
                });

            return new ServiceResponse(true, "Subscribed successfully.");
        }
    }
}
