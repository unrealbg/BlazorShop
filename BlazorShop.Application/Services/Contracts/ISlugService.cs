namespace BlazorShop.Application.Services.Contracts
{
    public interface ISlugService
    {
        string GenerateSlug(string sourceText);

        string NormalizeSlug(string slug);

        bool IsSlugSafe(string? slug);
    }
}