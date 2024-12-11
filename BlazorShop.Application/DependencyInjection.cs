namespace BlazorShop.Application
{
    using BlazorShop.Application.Mapping;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.Extensions.DependencyInjection;

    public static class DependencyInjection 
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(MappingConfig));
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICategoryService, CategoryService>();

            return services;
        }
    }
}
