namespace BlazorShop.Application.Mapping
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.DTOs.Product.ProductVariant;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Domain.Contracts;
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
            this.CreateMap<Category, GetCategory>()
                .ForMember(dest => dest.MetaTitle, opt => opt.MapFrom(src => src.IsPublished ? src.MetaTitle : null))
                .ForMember(dest => dest.MetaDescription, opt => opt.MapFrom(src => src.IsPublished ? src.MetaDescription : null))
                .ForMember(dest => dest.CanonicalUrl, opt => opt.MapFrom(src => src.IsPublished ? src.CanonicalUrl : null))
                .ForMember(dest => dest.OgTitle, opt => opt.MapFrom(src => src.IsPublished ? src.OgTitle : null))
                .ForMember(dest => dest.OgDescription, opt => opt.MapFrom(src => src.IsPublished ? src.OgDescription : null))
                .ForMember(dest => dest.OgImage, opt => opt.MapFrom(src => src.IsPublished ? src.OgImage : null))
                .ForMember(dest => dest.RobotsIndex, opt => opt.MapFrom(src => src.IsPublished ? src.RobotsIndex : true))
                .ForMember(dest => dest.RobotsFollow, opt => opt.MapFrom(src => src.IsPublished ? src.RobotsFollow : true));

            this.CreateMap<CreateProduct, Product>();
            this.CreateMap<UpdateProduct, Product>();
            this.CreateMap<Product, GetProduct>()
                .ForMember(dest => dest.Variants, opt => opt.MapFrom(src => src.Variants));
            this.CreateMap<CatalogProductReadModel, GetCatalogProduct>();

            this.CreateMap<Product, SeoFieldsDto>();
            this.CreateMap<Category, SeoFieldsDto>();
            this.CreateMap<Product, ProductSeoDto>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.Id));
            this.CreateMap<UpdateProductSeoDto, Product>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ProductId));
            this.CreateMap<Category, CategorySeoDto>()
                .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.Id));
            this.CreateMap<UpdateCategorySeoDto, Category>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.CategoryId));
            this.CreateMap<SeoSettings, SeoSettingsDto>();
            this.CreateMap<SeoSettings, UpdateSeoSettingsDto>();
            this.CreateMap<UpdateSeoSettingsDto, SeoSettings>();
            this.CreateMap<SeoRedirect, SeoRedirectDto>();
            this.CreateMap<UpsertSeoRedirectDto, SeoRedirectDto>();
            this.CreateMap<UpsertSeoRedirectDto, SeoRedirect>();

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
