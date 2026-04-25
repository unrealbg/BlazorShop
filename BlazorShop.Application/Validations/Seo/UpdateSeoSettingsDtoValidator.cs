namespace BlazorShop.Application.Validations.Seo
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Domain.Constants;

    using FluentValidation;

    public class UpdateSeoSettingsDtoValidator : AbstractValidator<UpdateSeoSettingsDto>
    {
        public UpdateSeoSettingsDtoValidator()
        {
            RuleFor(x => x.SiteName)
                .MaximumLength(SeoConstraints.SiteNameMaxLength);

            RuleFor(x => x.DefaultTitleSuffix)
                .MaximumLength(SeoConstraints.TitleSuffixMaxLength);

            RuleFor(x => x.DefaultMetaDescription)
                .MaximumLength(SeoConstraints.MetaDescriptionMaxLength);

            RuleFor(x => x.DefaultOgImage)
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeAbsoluteUrlOrRootRelativePath)
                .WithMessage("DefaultOgImage must be an absolute http/https URL or a root-relative path.");

            RuleFor(x => x.BaseCanonicalUrl)
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeAbsoluteUrl)
                .WithMessage("BaseCanonicalUrl must be an absolute http/https URL.");

            RuleFor(x => x.CompanyName)
                .MaximumLength(SeoConstraints.CompanyNameMaxLength);

            RuleFor(x => x.CompanyLogoUrl)
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeAbsoluteUrlOrRootRelativePath)
                .WithMessage("CompanyLogoUrl must be an absolute http/https URL or a root-relative path.");

            RuleFor(x => x.CompanyPhone)
                .MaximumLength(SeoConstraints.CompanyPhoneMaxLength);

            RuleFor(x => x.CompanyEmail)
                .MaximumLength(SeoConstraints.CompanyEmailMaxLength)
                .EmailAddress()
                .When(x => !string.IsNullOrWhiteSpace(x.CompanyEmail));

            RuleFor(x => x.CompanyAddress)
                .MaximumLength(SeoConstraints.CompanyAddressMaxLength);

            RuleFor(x => x.FacebookUrl)
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeAbsoluteUrl)
                .WithMessage("FacebookUrl must be an absolute http/https URL.");

            RuleFor(x => x.InstagramUrl)
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeAbsoluteUrl)
                .WithMessage("InstagramUrl must be an absolute http/https URL.");

            RuleFor(x => x.XUrl)
                .MaximumLength(SeoConstraints.UrlMaxLength)
                .Must(SeoValidationRules.BeAbsoluteUrl)
                .WithMessage("XUrl must be an absolute http/https URL.");
        }
    }
}