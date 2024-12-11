namespace BlazorShop.Application.Mapping
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Domain.Entities;

    public class MappingConfig : Profile
    {
        public MappingConfig()
        {
            // CreateMap<Source, Destination>();
            this.CreateMap<CreateCategory, Category>();
            //this.CreateMap<UpdateCategory, Category>();
            this.CreateMap<Category, GetCategory>();

            this.CreateMap<CreateProduct, Product>();
            //this.CreateMap<UpdateProduct, Product>();
            this.CreateMap<Product, GetProduct>();
        }
    }
}
