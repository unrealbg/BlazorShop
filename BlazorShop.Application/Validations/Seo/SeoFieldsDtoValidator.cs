namespace BlazorShop.Application.Validations.Seo
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Constants;

    using FluentValidation;

    public abstract class SeoFieldsDtoValidator<T> : AbstractValidator<T>
        where T : SeoFieldsDto
    {
        protected SeoFieldsDtoValidator(ISlugService slugService)
        {
            RuleFor(x => x.Slug)
                .NotEmpty()
                .When(x => x.IsPublished)
                .WithMessage("Slug is required for published SEO entries.");

            RuleFor(x => x.Slug)
                .MaximumLength(SeoConstraints.SlugMaxLength)
                .Must(slug => string.IsNullOrWhiteSpace(slug) || slugService.IsSlugSafe(slug))
                .WithMessage("Slug must already be normalized and URL-safe.");

            RuleFor(x => x.MetaTitle)
                .MaximumLength(SeoConstraints.MetaTitleMaxLength);

            RuleFor(x => x.MetaDescription)
                .MaximumLength(SeoConstraints.MetaDescriptionMaxLength);

            RuleFor(x => x.CanonicalUrl)
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeAbsoluteUrlOrRootRelativePath)
                .WithMessage("CanonicalUrl must be an absolute http/https URL or a root-relative path.");

            RuleFor(x => x.OgTitle)
                .MaximumLength(SeoConstraints.MetaTitleMaxLength);

            RuleFor(x => x.OgDescription)
                .MaximumLength(SeoConstraints.MetaDescriptionMaxLength);

            RuleFor(x => x.OgImage)
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeAbsoluteUrlOrRootRelativePath)
                .WithMessage("OgImage must be an absolute http/https URL or a root-relative path.");
        }
    }
}