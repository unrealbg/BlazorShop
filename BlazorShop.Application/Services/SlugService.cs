namespace BlazorShop.Application.Services
{
    using System.Globalization;
    using System.Text;

    using BlazorShop.Application.Services.Contracts;

    public class SlugService : ISlugService
    {
        public string GenerateSlug(string sourceText)
        {
            var slug = this.NormalizeSlug(sourceText);

            if (slug.Length == 0)
            {
                throw new ArgumentException("A URL-safe slug could not be generated from the provided text.", nameof(sourceText));
            }

            return slug;
        }

        public string NormalizeSlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return string.Empty;
            }

            var decomposed = slug.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var asciiBuilder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    asciiBuilder.Append(character);
                }
            }

            var normalized = asciiBuilder.ToString().Normalize(NormalizationForm.FormC);
            var slugBuilder = new StringBuilder(normalized.Length);
            var previousWasSeparator = false;

            foreach (var character in normalized)
            {
                if (char.IsLetterOrDigit(character))
                {
                    slugBuilder.Append(character);
                    previousWasSeparator = false;
                    continue;
                }

                if (slugBuilder.Length > 0 && !previousWasSeparator)
                {
                    slugBuilder.Append('-');
                    previousWasSeparator = true;
                }
            }

            return slugBuilder
                .ToString()
                .Trim('-');
        }

        public bool IsSlugSafe(string? slug)
        {
            return !string.IsNullOrWhiteSpace(slug)
                && string.Equals(this.NormalizeSlug(slug), slug, StringComparison.Ordinal);
        }
    }
}