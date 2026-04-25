namespace BlazorShop.Application.Validations.Seo
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Domain.Constants;

    using FluentValidation;

    public class SeoRedirectDtoValidator : AbstractValidator<SeoRedirectDto>
    {
        public SeoRedirectDtoValidator()
        {
            RuleFor(x => x.OldPath)
                .NotEmpty()
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeRootRelativePath)
                .WithMessage("OldPath must be a root-relative path.");

            RuleFor(x => x.NewPath)
                .NotEmpty()
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeRootRelativePath)
                .WithMessage("NewPath must be a root-relative path.");

            RuleFor(x => x.StatusCode)
                .Must(statusCode => statusCode == SeoConstraints.PermanentRedirectStatusCode || statusCode == SeoConstraints.TemporaryRedirectStatusCode)
                .WithMessage("StatusCode must be 301 or 302.");

            RuleFor(x => x)
                .Must(x => !string.Equals(x.OldPath, x.NewPath, StringComparison.OrdinalIgnoreCase))
                .WithMessage("OldPath and NewPath must be different.");
        }
    }
}