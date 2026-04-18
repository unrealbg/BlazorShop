namespace BlazorShop.Application.Validations.Seo
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using FluentValidation;

    public class UpdateProductSeoDtoValidator : SeoFieldsDtoValidator<UpdateProductSeoDto>
    {
        public UpdateProductSeoDtoValidator(ISlugService slugService)
            : base(slugService)
        {
            RuleFor(x => x.ProductId)
                .NotEmpty();
        }
    }
}