namespace BlazorShop.Application
{
    using BlazorShop.Application.Mapping;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Services.Authentication;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Application.Services.Contracts.Authentication;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Application.Validations;
    using BlazorShop.Application.Validations.Authentication;

    using FluentValidation;
    using FluentValidation.AspNetCore;

    using Microsoft.Extensions.DependencyInjection;

    public static class DependencyInjection 
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(MappingConfig));
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICategoryService, CategoryService>();

            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<CreateUserValidator>();
            services.AddScoped<IValidationService, ValidationService>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();

            services.AddScoped<ICartService, CartService>();
            services.AddScoped<IPaymentMethodService, PaymentMethodService>();

            return services;
        }
    }
}
