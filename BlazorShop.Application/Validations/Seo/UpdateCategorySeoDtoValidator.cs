namespace BlazorShop.Application.Validations.Seo
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using FluentValidation;

    public class UpdateCategorySeoDtoValidator : SeoFieldsDtoValidator<UpdateCategorySeoDto>
    {
        public UpdateCategorySeoDtoValidator(ISlugService slugService)
            : base(slugService)
        {
            RuleFor(x => x.CategoryId)
                .NotEmpty();
        }
    }
}