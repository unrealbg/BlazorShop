namespace BlazorShop.Application.Mapping
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.DTOs.Product.ProductVariant;
    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Domain.Entities.Payment;

    public class MappingConfig : Profile
    {
        public MappingConfig()
        {
            // CreateMap<Source, Destination>();
            this.CreateMap<CreateCategory, Category>();
            this.CreateMap<UpdateCategory, Category>();
            this.CreateMap<Category, GetCategory>();

            this.CreateMap<CreateProduct, Product>();
            this.CreateMap<UpdateProduct, Product>();
            this.CreateMap<Product, GetProduct>()
                .ForMember(dest => dest.Variants, opt => opt.MapFrom(src => src.Variants));

            this.CreateMap<Product, GetProductRecommendation>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null));

            this.CreateMap<CreateProductVariant, ProductVariant>();
            this.CreateMap<UpdateProductVariant, ProductVariant>();
            this.CreateMap<ProductVariant, GetProductVariant>();

            this.CreateMap<CreateUser, AppUser>();
            this.CreateMap<LoginUser, AppUser>();

            this.CreateMap<PaymentMethod, GetPaymentMethod>();
            this.CreateMap<CreateOrderItem, OrderItem>();
        }
    }
}
